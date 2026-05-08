// CombatComponent.cs
// Handles cooldown-based attacks against IDamageable targets.
// Implements IAttackable so the UnitBrain and AI can operate through a stable interface.

using System;
using UnityEngine;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;

namespace CheckmateRPG.Components
{
    /// <summary>
    /// Provides attack capability governed by a configurable cooldown.
    /// Damage is applied through the <see cref="IDamageable"/> interface,
    /// keeping this component decoupled from concrete unit types.
    /// </summary>
    public class CombatComponent : MonoBehaviour, IAttackable
    {
        // ─── Events ───────────────────────────────────────────────────────────────

        /// <summary>Raised after a successful attack. Parameter: the target GameObject.</summary>
        public event Action<GameObject> OnAttackPerformed;

        // ─── IAttackable ──────────────────────────────────────────────────────────

        /// <summary>True when the cooldown has expired and the unit is alive.</summary>
        public bool CanAttack => _cooldownRemainingTicks <= 0 && !_isDead &&
                                 (_statusEffects == null || _statusEffects.CanAttack);
        public AbilityRuntimeState BasicAttackRuntimeState { get; private set; } = new AbilityRuntimeState { Charges = 1 };

        // ─── Private State ────────────────────────────────────────────────────────

        private float _attackDamage;
        private float _attackCooldown;
        private int   _attackRange;
        private float _attackAPCost;
        private float _actionSpeed = 1f;
        private int   _cooldownRemainingTicks;
        private bool  _isDead;
        private StatusEffectComponent _statusEffects;
        private TickScheduler _tickScheduler;
        private bool _isTickSubscribed;

        private void Awake()
        {
            _statusEffects = GetComponent<StatusEffectComponent>();
        }

        private void OnEnable()
        {
            SetTickSubscription(true);
        }

        private void OnDisable()
        {
            SetTickSubscription(false);
        }

        // ─── Initialisation ───────────────────────────────────────────────────────

        /// <summary>
        /// Initialise from a UnitData asset. Called by UnitBrain during setup.
        /// </summary>
        public void Initialise(UnitData data)
        {
            _attackDamage    = data.AttackDamage;
            _attackCooldown  = data.AttackCooldown;
            _attackRange     = data.AttackRange;
            _attackAPCost    = Mathf.Max(0f, data.AttackCostAP);
            _actionSpeed     = Mathf.Max(0.1f, data.ActionSpeed);
            _cooldownRemainingTicks = 0;
            _isDead          = false;
            BasicAttackRuntimeState.Charges = 1;
            UpdateAbilityRuntimeState();
        }

        // ─── IAttackable Implementation ───────────────────────────────────────────

        /// <summary>
        /// Attempt to attack <paramref name="target"/>.
        /// Attack is skipped if the cooldown has not expired or the target is out of range.
        /// </summary>
        public void Attack(GameObject target)
        {
            if (!CanAttack)
            {
                Debug.Log($"[CombatComponent] {gameObject.name} cannot attack yet " +
                          $"(cooldown: {TickScheduler.TicksToSeconds(_cooldownRemainingTicks):F2}s).");
                return;
            }

            if (_statusEffects != null && !_statusEffects.CanAttack)
            {
                Debug.Log($"[CombatComponent] {gameObject.name} is unable to attack due to status effects.");
                return;
            }

            if (target == null)
            {
                Debug.LogWarning("[CombatComponent] Attack called with null target.");
                return;
            }

            if (!IsInRange(target))
            {
                Debug.Log($"[CombatComponent] {target.name} is out of attack range.");
                return;
            }

            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable == null)
            {
                Debug.LogWarning($"[CombatComponent] {target.name} does not implement IDamageable.");
                return;
            }

            float apCost = GetAttackAPCost();
            if (!TrySpendAP(apCost))
                return;

            float damage = _attackDamage;
            if (_statusEffects != null)
                damage *= _statusEffects.AttackMultiplier;

            if (target.TryGetComponent(out HealthComponent health))
                health.ApplyDamage(damage, DamageType.Physical);
            else
                damageable.TakeDamage(damage);

            float actionSpeed = _actionSpeed * (_statusEffects != null ? _statusEffects.ActionSpeedMultiplier : 1f);
            _cooldownRemainingTicks = TickScheduler.SecondsToTicks(_attackCooldown / Mathf.Max(0.1f, actionSpeed));
            UpdateAbilityRuntimeState();

            OnAttackPerformed?.Invoke(target);

            _statusEffects?.NotifyAction(UnitActionType.Attack);
        }

        // ─── Public Helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Called by HealthComponent.OnDeath to stop the unit from attacking after death.
        /// </summary>
        public void OnOwnerDied()
        {
            _isDead = true;
        }

        // ─── Private Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if <paramref name="target"/> is within Chebyshev attack range.
        /// Falls back to world-distance when either unit is not on the grid.
        /// The world-distance fallback uses 1.5× tile-size per range step to allow
        /// diagonal adjacency (√2 ≈ 1.41) while still excluding anything farther away.
        /// </summary>
        private bool IsInRange(GameObject target)
        {
            // Prefer grid distance when both units are tracked on the grid
            MovementComponent targetMovement = target.GetComponent<MovementComponent>();
            MovementComponent selfMovement   = GetComponent<MovementComponent>();

            if (targetMovement != null && selfMovement != null)
            {
                int distance = ChebyshevDistance(selfMovement.GridPosition, targetMovement.GridPosition);
                return distance <= _attackRange;
            }

            // Fallback: world-space distance (1.5 per range step covers diagonal tiles at √2 distance)
            float tileSize   = GridSystem.Instance != null ? 1f : 1f; // reserved for future tile-size injection
            float worldRange = _attackRange * tileSize * 1.5f;
            return Vector3.Distance(transform.position, target.transform.position) <= worldRange;
        }

        private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        private float GetAttackAPCost()
        {
            float multiplier = _statusEffects != null ? _statusEffects.ActionCostMultiplier : 1f;
            return Mathf.Max(0f, _attackAPCost * multiplier);
        }

        private void UpdateAbilityRuntimeState()
        {
            BasicAttackRuntimeState.CooldownRemaining = _cooldownRemainingTicks;
            BasicAttackRuntimeState.Locked = !CanAttack;
        }

        private void HandleTick(int tick)
        {
            if (_cooldownRemainingTicks > 0)
                _cooldownRemainingTicks--;

            UpdateAbilityRuntimeState();
        }

        private void SetTickSubscription(bool shouldSubscribe)
        {
            if (shouldSubscribe)
            {
                if (_isTickSubscribed)
                    return;

                _tickScheduler = TickScheduler.EnsureExists();
                _tickScheduler.OnTick += HandleTick;
                _isTickSubscribed = true;
                return;
            }

            if (!_isTickSubscribed || _tickScheduler == null)
                return;

            _tickScheduler.OnTick -= HandleTick;
            _isTickSubscribed = false;
        }

        private bool TrySpendAP(float cost)
        {
            if (cost <= 0f)
                return true;

            if (APManager.Instance == null)
            {
                Debug.LogWarning("[CombatComponent] APManager not found. Attack cancelled.");
                return false;
            }

            if (!APManager.Instance.TrySpend(new ActionPointCost(cost, APActionReason.Attack), out _))
                return false;

            return true;
        }
    }
}
