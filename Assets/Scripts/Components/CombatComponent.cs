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
        public bool CanAttack => _cooldownRemaining <= 0f && !_isDead &&
                                 (_statusEffects == null || _statusEffects.CanAttack);

        // ─── Private State ────────────────────────────────────────────────────────

        private float _attackDamage;
        private float _attackCooldown;
        private int   _attackRange;
        private float _attackAPCost;
        private float _cooldownRemaining;
        private bool  _isDead;
        private StatusEffectComponent _statusEffects;

        private void Awake()
        {
            _statusEffects = GetComponent<StatusEffectComponent>();
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
            _attackAPCost    = Mathf.Max(0f, data.AttackAPCost);
            _cooldownRemaining = 0f;
            _isDead          = false;
        }

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Update()
        {
            if (_cooldownRemaining > 0f)
                _cooldownRemaining -= Time.deltaTime;
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
                          $"(cooldown: {_cooldownRemaining:F2}s).");
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

            float apCost = GetAttackApCost();
            if (!TrySpendAP(apCost))
                return;

            float damage = _attackDamage;
            if (_statusEffects != null)
                damage *= _statusEffects.AttackMultiplier;

            if (target.TryGetComponent(out HealthComponent health))
                health.ApplyDamage(damage, DamageType.Physical);
            else
                damageable.TakeDamage(damage);

            float actionSpeed = _statusEffects != null ? _statusEffects.ActionSpeedMultiplier : 1f;
            _cooldownRemaining = _attackCooldown / Mathf.Max(0.1f, actionSpeed);

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

        private float GetAttackApCost()
        {
            float multiplier = _statusEffects != null ? _statusEffects.ActionCostMultiplier : 1f;
            return Mathf.Max(0f, _attackAPCost * multiplier);
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

            if (!APManager.Instance.TrySpend(cost))
            {
                Debug.LogWarning($"[CombatComponent] {gameObject.name} has insufficient AP to attack.");
                return false;
            }

            return true;
        }
    }
}
