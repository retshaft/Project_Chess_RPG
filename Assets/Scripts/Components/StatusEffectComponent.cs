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

        [Header("CC Durations")]
        [SerializeField] private float _stunDuration = 3f;
        [SerializeField] private float _rootDuration = 3f;
        [SerializeField] private float _silenceDuration = 4f;
        [SerializeField] private float _disarmDuration = 4f;
        [SerializeField] private float _tauntDuration = 4f;
        [SerializeField] private float _stealthDuration = 5f;

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

        public event Action<StatusEffectType> OnStatusApplied;
        public event Action<StatusEffectType> OnStatusRemoved;

        public void RemoveStatusEffect(StatusEffectType type)
        {
            if (_activeEffects.Remove(type))
            {
                OnStatusRemoved?.Invoke(type);
            }
        }

        private HealthComponent _health;
        private MovementComponent _movement;
        private UnitBrain _unitBrain;

        private ElementType _currentAura = ElementType.None;
        private float _auraRemaining;

        public bool CanMove => !HasStatus(StatusEffectType.Freeze) && 
                               !(TryGetEffect(StatusEffectType.Paralysis, out var paralysis) && paralysis.RemainingTick >= paralysis.RemainingDuration * 0.5f) &&
                               !HasStatus(StatusEffectType.Stun) && 
                               !HasStatus(StatusEffectType.Root);
        public bool CanAttack => !HasStatus(StatusEffectType.Freeze) && !HasStatus(StatusEffectType.Paralysis) && 
                                 !HasStatus(StatusEffectType.Stun) && !HasStatus(StatusEffectType.Disarm);
        public bool CanUseSkills => !HasStatus(StatusEffectType.Freeze) && !HasStatus(StatusEffectType.Stun) && !HasStatus(StatusEffectType.Silence);
        
        public bool IsStealthed => HasStatus(StatusEffectType.Stealth);
        public bool IsTaunted => HasStatus(StatusEffectType.Taunt);
        
        public float AttackMultiplier => HasStatus(StatusEffectType.Overload) ? 0.75f : 1f;
        
        public float DefenseMultiplier {
            get {
                float mult = 1f;
                if (HasStatus(StatusEffectType.Superconduct)) mult *= 0.6f;
                if (HasStatus(StatusEffectType.FrozenBossDebuff)) mult *= 0.75f;
                return mult;
            }
        }
        
        public float ResistanceMultiplier {
            get {
                float mult = 1f;
                if (HasStatus(StatusEffectType.Burn)) mult *= 0.8f;
                if (HasStatus(StatusEffectType.FrozenBossDebuff)) mult *= 0.75f;
                return mult;
            }
        }
        
        public float ActionSpeedMultiplier => HasStatus(StatusEffectType.Wound) ? 0.9f : 1f;
        
        public float ActionCostMultiplier {
            get {
                float mult = 1f;
                if (HasStatus(StatusEffectType.Chill)) mult *= 1.2f;
                if (HasStatus(StatusEffectType.Stagger)) mult *= 1.2f;
                if (HasStatus(StatusEffectType.Wound)) mult *= 1.2f;
                return mult;
            }
        }
        
        public float HealReceivedMultiplier => HasStatus(StatusEffectType.Necrosis) ? 0.4f : 1f;
        
        private float _maxSp;
        private float _currentSp;
        
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
            // Update 로직 제거: 틱 기반 EffectSystem으로 완전히 위임됨.
            // UI를 위해 필요 시 여기서 최신 값을 갱신하거나 EventBus를 구독해 처리합니다.
            // 현재 프레임은 시각적 Presentation에만 사용해야 합니다.
        }

        public bool HasStatus(StatusEffectType type)
        {
            if (ActionRuntimeController.Instance == null || ActionRuntimeController.Instance.SimulationRuntime == null)
                return false;

            return ActionRuntimeController.Instance.SimulationRuntime.TryGetEffect(
                Core.Simulation.SimulationRuntime.BuildEffectKey(GetActorId(), type.ToString()), out _);
        }

        public void NotifyAction(UnitActionType actionType)
        {
            // M9 에서는 NotifyAction에 의존하지 않고 ActionRuntimeController의 Event나 Mutation Processor에서 처리합니다.
        }

        public float ModifyIncomingDamage(float amount, DamageType damageType)
        {
            if (TryGetEffect(StatusEffectType.GrabVulnerability, out Core.Effects.IReadOnlyEffectRuntimeState grab))
            {
                amount *= 1f + _grabCriticalBonus;
                // Note: Effect removal should be handled by the mutation processor now, but for legacy compatibility we just read it.
            }

            if (damageType == DamageType.Physical && HasStatus(StatusEffectType.Freeze))
            {
                amount *= 1f + _shatterBonusMultiplier;
            }

            return amount;
        }

        public void ApplyStatusEffect(StatusEffectType type, float duration = 0f, int stacks = 0, bool isSecondary = false, Guid? sourceActorId = null)
        {
            float finalDuration = duration > 0f ? duration : GetDefaultDuration(type);
            if (finalDuration <= 0f || ActionRuntimeController.Instance == null)
                return;

            ActionRuntimeController.Instance.CommitMutation(new Core.Runtime.Mutations.ApplyEffectMutation(
                SeededRandomProvider.Shared.NextGuid(),
                type.ToString(),
                sourceActorId ?? GetActorId(),
                GetActorId(),
                SecondsToTicks(finalDuration),
                SecondsToTicks(_dotTickInterval),
                SecondsToTicks(_dotTickInterval),
                Mathf.Max(1, stacks),
                GetDotRuntimeMagnitude(type),
                Context: new Core.Runtime.Mutations.MutationContext(0, Guid.Empty, GetActorId(), "ApplyStatus")
            ));
        }

        public void ApplyGrabVulnerability()
        {
            ApplyStatusEffect(StatusEffectType.GrabVulnerability, _grabVulnerabilityDuration);
        }

        public void ApplyElement(ElementType element)
        {
            if (element == ElementType.None || ActionRuntimeController.Instance == null)
                return;

            string auraId = element switch {
                ElementType.Fire => "FireAura",
                ElementType.Cold => "ColdAura",
                ElementType.Lightning => "LightningAura",
                ElementType.Nature => "NatureAura",
                _ => null
            };

            if (auraId != null) {
                ActionRuntimeController.Instance.CommitMutation(new Core.Runtime.Mutations.ApplyEffectMutation(
                    SeededRandomProvider.Shared.NextGuid(),
                    auraId,
                    GetActorId(),
                    GetActorId(),
                    SecondsToTicks(_elementalAuraDuration),
                    1, 1, 1, 1f,
                    Context: new Core.Runtime.Mutations.MutationContext(0, Guid.Empty, GetActorId(), "ApplyAura")
                ));
            }
        }

        public void DrainSpPercent(float percent)
        {
            if (_maxSp <= 0f)
                return;

            percent = Mathf.Clamp01(percent);
            _currentSp = Mathf.Max(0f, _currentSp - _maxSp * percent);
        }

        private static int SecondsToTicks(float seconds)
        {
            float tickDurationSeconds = Core.TickScheduler.DefaultTickDurationSeconds;
            return Mathf.CeilToInt(Mathf.Max(0f, seconds) / tickDurationSeconds);
        }

        public bool TryGetEffect(StatusEffectType type, out Core.Effects.IReadOnlyEffectRuntimeState effectState)
        {
            effectState = null;
            if (ActionRuntimeController.Instance == null || ActionRuntimeController.Instance.SimulationRuntime == null)
                return false;

            return ActionRuntimeController.Instance.SimulationRuntime.TryGetEffect(
                Core.Simulation.SimulationRuntime.BuildEffectKey(GetActorId(), type.ToString()), out effectState);
        }

        private Guid GetActorId()
        {
            return _unitBrain != null ? _unitBrain.ActorId : Guid.Empty;
        }

        private float GetDotRuntimeMagnitude(StatusEffectType type)
        {
            const float burnBaseRatio = 0.02f;
            const float igniteBaseRatio = 0.03f;
            const float poisonBaseRatio = 0.02f;

            return type switch
            {
                StatusEffectType.Burn => burnBaseRatio > 0f ? _burnDamagePercentPerTick / burnBaseRatio : 1f,
                StatusEffectType.Ignite => igniteBaseRatio > 0f ? _igniteDamagePercentPerTick / igniteBaseRatio : 1f,
                StatusEffectType.Poison => poisonBaseRatio > 0f ? _poisonDamagePercentPerTick / poisonBaseRatio : 1f,
                _ => 1f
            };
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
                StatusEffectType.Stun => _stunDuration,
                StatusEffectType.Root => _rootDuration,
                StatusEffectType.Silence => _silenceDuration,
                StatusEffectType.Disarm => _disarmDuration,
                StatusEffectType.Taunt => _tauntDuration,
                StatusEffectType.Stealth => _stealthDuration,
                _ => 0f
            };
        }


    }
}
