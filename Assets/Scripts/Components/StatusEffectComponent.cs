using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;

namespace CheckmateRPG.Components
{
    public class StatusEffectComponent : MonoBehaviour
    {
        [Header("General Durations")]
        [SerializeField] private float _elementalAuraDuration = 6f;
        [SerializeField] private float _staggerDuration = 4f;
        [SerializeField] private float _woundDuration = 6f;
        [SerializeField] private float _grabVulnerabilityDuration = 4f;

        [Header("Elemental Durations")]
        [SerializeField] private float _burnDuration = 6f;
        [SerializeField] private float _igniteDuration = 6f;
        [SerializeField] private float _overloadDuration = 6f;
        [SerializeField] private float _superconductDuration = 6f;
        [SerializeField] private float _paralysisDuration = 4f;
        [SerializeField] private float _chillDuration = 6f;
        [SerializeField] private float _freezeDuration = 2f;
        [SerializeField] private float _necrosisDuration = 6f;
        [SerializeField] private float _poisonDuration = 6f;
        [SerializeField] private float _virusDuration = 4f;
        [SerializeField] private float _bossFreezeDebuffDuration = 4f;

        [Header("Damage Values")]
        [SerializeField] private float _bleedDamagePercentPerStack = 0.02f;
        [SerializeField] private float _burnDamagePercentPerTick = 0.02f;
        [SerializeField] private float _igniteDamagePercentPerTick = 0.03f;
        [SerializeField] private float _poisonDamagePercentPerTick = 0.02f;
        [SerializeField] private float _reactionMagicDamagePercent = 0.1f;
        [SerializeField] private float _shatterBonusMultiplier = 0.5f;
        [SerializeField] private float _grabCriticalBonus = 0.4f;

        [Header("Tick Settings")]
        [SerializeField] private float _dotTickInterval = 1f;

        [Header("Knockback Settings")]
        [SerializeField] private int _explosionKnockbackForce = 2;

        [Header("Stack Settings")]
        [SerializeField] private int _bleedStacksToWound = 3;

        [Header("Shock Settings")]
        [SerializeField] private float _shockSpDrainPercent = 0.2f;

        private class StatusEffectInstance
        {
            public float Duration;
            public float Remaining;
            public int Stacks;
            public bool IsSecondary;
        }

        private readonly Dictionary<StatusEffectType, StatusEffectInstance> _activeEffects = new();

        private HealthComponent _health;
        private MovementComponent _movement;
        private UnitBrain _unitBrain;

        private ElementType _currentAura = ElementType.None;
        private float _auraRemaining;

        private float _attackMultiplier = 1f;
        private float _defenseMultiplier = 1f;
        private float _resistanceMultiplier = 1f;
        private float _actionSpeedMultiplier = 1f;
        private float _actionCostMultiplier = 1f;
        private float _healReceivedMultiplier = 1f;
        private bool _canMove = true;
        private bool _canAttack = true;

        private float _maxSp;
        private float _currentSp;

        public bool CanMove => _canMove;
        public bool CanAttack => _canAttack;
        public float AttackMultiplier => _attackMultiplier;
        public float DefenseMultiplier => _defenseMultiplier;
        public float ResistanceMultiplier => _resistanceMultiplier;
        public float ActionSpeedMultiplier => _actionSpeedMultiplier;
        public float ActionCostMultiplier => _actionCostMultiplier;
        public float HealReceivedMultiplier => _healReceivedMultiplier;
        public float MaxSp => _maxSp;
        public float CurrentSp => _currentSp;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _movement = GetComponent<MovementComponent>();
            _unitBrain = GetComponent<UnitBrain>();
        }

        public void Initialise(UnitData data)
        {
            _maxSp = Mathf.Max(0f, data.MaxSP);
            _currentSp = _maxSp;
        }

        private void Update()
        {
            UpdateAura(Time.deltaTime);
            UpdateEffects(Time.deltaTime);
            RecalculateModifiers();
        }

        public bool HasStatus(StatusEffectType type) => _activeEffects.ContainsKey(type);

        public void NotifyAction(UnitActionType actionType)
        {
            if (!TryGetEffect(StatusEffectType.Bleed, out StatusEffectInstance bleed))
                return;

            if (_health != null && bleed.Stacks > 0)
            {
                float damage = _health.MaxHealth * _bleedDamagePercentPerStack * bleed.Stacks;
                if (damage > 0f)
                    _health.ApplyTrueDamage(damage);
            }

            bleed.Stacks = Mathf.Max(bleed.Stacks - 1, 0);
            if (bleed.Stacks <= 0)
                _activeEffects.Remove(StatusEffectType.Bleed);

            UpdateWoundState();
        }

        public float ModifyIncomingDamage(float amount, DamageType damageType)
        {
            bool changed = false;
            if (TryGetEffect(StatusEffectType.GrabVulnerability, out StatusEffectInstance grab))
            {
                amount *= 1f + _grabCriticalBonus;
                _activeEffects.Remove(StatusEffectType.GrabVulnerability);
                changed = true;
            }

            if (damageType == DamageType.Physical && HasStatus(StatusEffectType.Freeze))
            {
                amount *= 1f + _shatterBonusMultiplier;
                _activeEffects.Remove(StatusEffectType.Freeze);
                changed = true;
            }

            if (changed)
                RecalculateModifiers();

            return amount;
        }

        public void ApplyStatusEffect(StatusEffectType type, float duration = 0f, int stacks = 0, bool isSecondary = false, Guid? sourceActorId = null)
        {
            if (type == StatusEffectType.Bleed)
            {
                ApplyBleed(stacks > 0 ? stacks : 1);
                return;
            }

            float finalDuration = duration > 0f ? duration : GetDefaultDuration(type);
            if (finalDuration <= 0f)
                return;

            if (!_activeEffects.TryGetValue(type, out StatusEffectInstance instance))
            {
                instance = new StatusEffectInstance();
                _activeEffects[type] = instance;
            }

            instance.Duration = finalDuration;
            instance.Remaining = finalDuration;
            instance.IsSecondary = isSecondary;
            instance.Stacks = Mathf.Max(instance.Stacks, stacks);

            if (type == StatusEffectType.Burn || type == StatusEffectType.Ignite || type == StatusEffectType.Poison)
                ApplyRuntimeDot(type, finalDuration, Mathf.Max(1, stacks), sourceActorId ?? GetActorId());
        }

        public void ApplyGrabVulnerability()
        {
            ApplyStatusEffect(StatusEffectType.GrabVulnerability, _grabVulnerabilityDuration);
        }

        public void ApplyElement(ElementType element)
        {
            if (element == ElementType.None)
                return;

            if (element == ElementType.Nature && TryGetEffect(StatusEffectType.Poison, out StatusEffectInstance poison))
            {
                if (!poison.IsSecondary)
                    TriggerVirus();
                return;
            }

            if (element == ElementType.Cold && HasStatus(StatusEffectType.Chill))
            {
                TriggerFreeze();
                _activeEffects.Remove(StatusEffectType.Chill);
                _currentAura = ElementType.None;
                return;
            }

            if (element == ElementType.Lightning && HasStatus(StatusEffectType.Superconduct))
            {
                ApplyStatusEffect(StatusEffectType.Stagger, _staggerDuration);
                _activeEffects.Remove(StatusEffectType.Superconduct);
                RecalculateModifiers();
            }

            if (_currentAura == ElementType.None)
            {
                _currentAura = element;
                _auraRemaining = _elementalAuraDuration;
                return;
            }

            if (_currentAura == element)
            {
                TriggerSameElementReaction(element);
                _currentAura = ElementType.None;
                return;
            }

            TriggerComboReaction(_currentAura, element);
            _currentAura = ElementType.None;
        }

        public void DrainSpPercent(float percent)
        {
            if (_maxSp <= 0f)
                return;

            percent = Mathf.Clamp01(percent);
            _currentSp = Mathf.Max(0f, _currentSp - _maxSp * percent);
        }

        private void UpdateAura(float deltaTime)
        {
            if (_currentAura == ElementType.None)
                return;

            _auraRemaining -= deltaTime;
            if (_auraRemaining <= 0f)
                _currentAura = ElementType.None;
        }

        private void UpdateEffects(float deltaTime)
        {
            if (_activeEffects.Count == 0)
                return;

            List<StatusEffectType> expired = null;

            foreach (KeyValuePair<StatusEffectType, StatusEffectInstance> pair in _activeEffects)
            {
                StatusEffectInstance instance = pair.Value;
                if (instance.Duration <= 0f)
                    continue;

                instance.Remaining -= deltaTime;

                if (instance.Remaining <= 0f)
                {
                    expired ??= new List<StatusEffectType>();
                    expired.Add(pair.Key);
                }
            }

            if (expired == null)
                return;

            foreach (StatusEffectType type in expired)
                _activeEffects.Remove(type);

            UpdateWoundState();
        }

        private void ApplyBleed(int stacks)
        {
            if (!_activeEffects.TryGetValue(StatusEffectType.Bleed, out StatusEffectInstance instance))
            {
                instance = new StatusEffectInstance { Duration = -1f, Remaining = -1f };
                _activeEffects[StatusEffectType.Bleed] = instance;
            }

            instance.Stacks += stacks;
            UpdateWoundState();
        }

        private void UpdateWoundState()
        {
            if (TryGetEffect(StatusEffectType.Bleed, out StatusEffectInstance bleed) && bleed.Stacks >= _bleedStacksToWound)
            {
                ApplyStatusEffect(StatusEffectType.Wound, _woundDuration);
                return;
            }

            _activeEffects.Remove(StatusEffectType.Wound);
        }

        private void TriggerSameElementReaction(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire:
                    ApplyStatusEffect(StatusEffectType.Burn, _burnDuration);
                    break;
                case ElementType.Lightning:
                    TriggerShock();
                    break;
                case ElementType.Cold:
                    ApplyStatusEffect(StatusEffectType.Chill, _chillDuration);
                    break;
                case ElementType.Nature:
                    ApplyStatusEffect(StatusEffectType.Poison, _poisonDuration);
                    break;
            }
        }

        private void TriggerComboReaction(ElementType first, ElementType second)
        {
            if ((first == ElementType.Fire && second == ElementType.Cold) ||
                (first == ElementType.Cold && second == ElementType.Fire))
            {
                TriggerExplosion();
                return;
            }

            if ((first == ElementType.Fire && second == ElementType.Nature) ||
                (first == ElementType.Nature && second == ElementType.Fire))
            {
                ApplyStatusEffect(StatusEffectType.Ignite, _igniteDuration);
                return;
            }

            if ((first == ElementType.Fire && second == ElementType.Lightning) ||
                (first == ElementType.Lightning && second == ElementType.Fire))
            {
                TriggerOverload();
                return;
            }

            if ((first == ElementType.Lightning && second == ElementType.Cold) ||
                (first == ElementType.Cold && second == ElementType.Lightning))
            {
                ApplyStatusEffect(StatusEffectType.Superconduct, _superconductDuration);
                return;
            }

            if ((first == ElementType.Lightning && second == ElementType.Nature) ||
                (first == ElementType.Nature && second == ElementType.Lightning))
            {
                ApplyStatusEffect(StatusEffectType.Paralysis, _paralysisDuration);
                return;
            }

            if ((first == ElementType.Cold && second == ElementType.Nature) ||
                (first == ElementType.Nature && second == ElementType.Cold))
            {
                ApplyStatusEffect(StatusEffectType.Necrosis, _necrosisDuration);
            }
        }

        private void TriggerShock()
        {
            ApplyMagicDamageToCross(_reactionMagicDamagePercent);
            DrainSpPercent(_shockSpDrainPercent);
            ApplySpDrainToCross(_shockSpDrainPercent);
        }

        private void TriggerExplosion()
        {
            ApplyMagicDamageToCross(_reactionMagicDamagePercent);
            ApplyExplosionKnockback();
        }

        private void TriggerOverload()
        {
            ApplyMagicDamageToSelf(_reactionMagicDamagePercent);
            ApplyStatusEffect(StatusEffectType.Overload, _overloadDuration);
        }

        private void TriggerFreeze()
        {
            if (_movement != null && _movement.IsBoss)
            {
                ApplyStatusEffect(StatusEffectType.FrozenBossDebuff, _bossFreezeDebuffDuration);
                return;
            }

            float weightFactor = _movement != null ? Mathf.Max(1f, _movement.Weight + 1f) : 1f;
            float duration = _freezeDuration / weightFactor;
            ApplyStatusEffect(StatusEffectType.Freeze, duration);
        }

        private void TriggerVirus()
        {
            ApplyStatusEffect(StatusEffectType.Virus, _virusDuration);
            ApplyStatusEffect(StatusEffectType.Poison, _poisonDuration);
            ApplyPoisonToCross(isSecondary: true);
        }

        private void ApplyMagicDamageToSelf(float percent)
        {
            if (_health == null || _health.IsDead)
                return;

            float damage = _health.MaxHealth * percent;
            if (damage > 0f)
                _health.ApplyMagicDamage(damage);
        }

        private void ApplyMagicDamageToCross(float percent)
        {
            if (_health == null || _health.IsDead)
                return;

            ApplyMagicDamageToSelf(percent);

            foreach (GameObject unit in EnumerateCrossUnits())
                ApplyMagicDamageToUnit(unit, percent);
        }

        private void ApplyMagicDamageToUnit(GameObject unit, float percent)
        {
            if (unit == null)
                return;

            if (!unit.TryGetComponent(out HealthComponent targetHealth) || targetHealth.IsDead)
                return;

            float damage = targetHealth.MaxHealth * percent;
            if (damage > 0f)
                targetHealth.ApplyMagicDamage(damage);
        }

        private void ApplySpDrainToCross(float percent)
        {
            foreach (GameObject unit in EnumerateCrossUnits())
            {
                if (unit == null)
                    continue;

                if (unit.TryGetComponent(out StatusEffectComponent status))
                    status.DrainSpPercent(percent);
            }
        }

        private void ApplyPoisonToCross(bool isSecondary)
        {
            foreach (GameObject unit in EnumerateCrossUnits())
            {
                if (unit == null)
                    continue;

                if (unit.TryGetComponent(out StatusEffectComponent status))
                    status.ApplyStatusEffect(StatusEffectType.Poison, _poisonDuration, isSecondary: isSecondary, sourceActorId: GetActorId());
            }
        }

        private void ApplyExplosionKnockback()
        {
            if (_movement == null)
                return;

            Vector2Int origin = GetGridPosition();
            if (origin.x < 0)
                return;

            foreach (Vector2Int offset in GetCrossOffsets())
            {
                Vector2Int cell = origin + offset;
                if (GridSystem.Instance == null || !GridSystem.Instance.IsValidCell(cell))
                    continue;

                GameObject occupant = GridSystem.Instance.GetOccupant(cell);
                if (occupant == null)
                    continue;

                if (!occupant.TryGetComponent(out MovementComponent targetMovement))
                    continue;

                targetMovement.ApplyKnockback(offset, _explosionKnockbackForce, applySplatDamage: false);
            }
        }

        private IEnumerable<GameObject> EnumerateCrossUnits()
        {
            if (GridSystem.Instance == null)
                yield break;

            Vector2Int origin = GetGridPosition();
            if (!GridSystem.Instance.IsValidCell(origin))
                yield break;

            foreach (Vector2Int offset in GetCrossOffsets())
            {
                Vector2Int cell = origin + offset;
                if (!GridSystem.Instance.IsValidCell(cell))
                    continue;

                GameObject occupant = GridSystem.Instance.GetOccupant(cell);
                if (occupant != null)
                    yield return occupant;
            }
        }

        private static Vector2Int[] GetCrossOffsets()
        {
            return new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };
        }

        private Vector2Int GetGridPosition()
        {
            if (_movement != null)
                return _movement.GridPosition;

            if (GridSystem.Instance == null)
                return new Vector2Int(-1, -1);

            return GridSystem.Instance.WorldToGrid(transform.position);
        }

        private bool TryGetEffect(StatusEffectType type, out StatusEffectInstance instance)
        {
            return _activeEffects.TryGetValue(type, out instance);
        }

        private void ApplyRuntimeDot(StatusEffectType type, float durationSeconds, int stacks, Guid sourceActorId)
        {
            if (ActionRuntimeController.Instance == null)
                return;

            Guid targetId = GetActorId();
            if (targetId == Guid.Empty)
                return;

            int durationTicks = SecondsToTicks(durationSeconds);
            int tickIntervalTicks = SecondsToTicks(_dotTickInterval);
            if (durationTicks <= 0 || tickIntervalTicks <= 0)
                return;

            ActionRuntimeController.Instance.ApplyEffectRuntime(new EffectRuntimeState
            {
                EffectId = type.ToString(),
                SourceId = sourceActorId == Guid.Empty ? targetId : sourceActorId,
                TargetId = targetId,
                RemainingTick = durationTicks,
                StackCount = Mathf.Max(1, stacks),
                TickInterval = tickIntervalTicks,
                NextTickIn = tickIntervalTicks,
                Magnitude = GetDotTickMagnitude(type)
            });
        }

        private static int SecondsToTicks(float seconds)
        {
            float tickDurationSeconds = ActionTimelineFormula.TickMilliseconds / 1000f;
            return Mathf.CeilToInt(Mathf.Max(0f, seconds) / tickDurationSeconds);
        }

        private float GetDotTickMagnitude(StatusEffectType type)
        {
            return type switch
            {
                StatusEffectType.Burn => _burnDamagePercentPerTick,
                StatusEffectType.Ignite => _igniteDamagePercentPerTick,
                StatusEffectType.Poison => _poisonDamagePercentPerTick,
                _ => 0f
            };
        }

        private Guid GetActorId()
        {
            return _unitBrain != null ? _unitBrain.ActorId : Guid.Empty;
        }

        private float GetDefaultDuration(StatusEffectType type)
        {
            return type switch
            {
                StatusEffectType.Stagger => _staggerDuration,
                StatusEffectType.Wound => _woundDuration,
                StatusEffectType.Burn => _burnDuration,
                StatusEffectType.Ignite => _igniteDuration,
                StatusEffectType.Overload => _overloadDuration,
                StatusEffectType.Superconduct => _superconductDuration,
                StatusEffectType.Paralysis => _paralysisDuration,
                StatusEffectType.Chill => _chillDuration,
                StatusEffectType.Freeze => _freezeDuration,
                StatusEffectType.Necrosis => _necrosisDuration,
                StatusEffectType.Poison => _poisonDuration,
                StatusEffectType.Virus => _virusDuration,
                StatusEffectType.GrabVulnerability => _grabVulnerabilityDuration,
                StatusEffectType.FrozenBossDebuff => _bossFreezeDebuffDuration,
                _ => 0f
            };
        }

        private void RecalculateModifiers()
        {
            _attackMultiplier = 1f;
            _defenseMultiplier = 1f;
            _resistanceMultiplier = 1f;
            _actionSpeedMultiplier = 1f;
            _actionCostMultiplier = 1f;
            _healReceivedMultiplier = 1f;
            _canMove = true;
            _canAttack = true;

            if (HasStatus(StatusEffectType.Overload))
                _attackMultiplier *= 0.75f;

            if (HasStatus(StatusEffectType.Superconduct))
                _defenseMultiplier *= 0.6f;

            if (HasStatus(StatusEffectType.Burn))
                _resistanceMultiplier *= 0.8f;

            if (HasStatus(StatusEffectType.FrozenBossDebuff))
            {
                _defenseMultiplier *= 0.75f;
                _resistanceMultiplier *= 0.75f;
            }

            if (HasStatus(StatusEffectType.Chill))
                _actionCostMultiplier *= 1.2f;

            if (HasStatus(StatusEffectType.Stagger))
                _actionCostMultiplier *= 1.2f;

            if (HasStatus(StatusEffectType.Wound))
            {
                _actionCostMultiplier *= 1.2f;
                _actionSpeedMultiplier *= 0.9f;
            }

            if (HasStatus(StatusEffectType.Necrosis))
                _healReceivedMultiplier *= 0.4f;

            if (HasStatus(StatusEffectType.Freeze))
            {
                _canMove = false;
                _canAttack = false;
            }

            if (TryGetEffect(StatusEffectType.Paralysis, out StatusEffectInstance paralysis))
            {
                _canAttack = false;
                if (paralysis.Remaining >= paralysis.Duration * 0.5f)
                    _canMove = false;
            }
        }
    }
}
