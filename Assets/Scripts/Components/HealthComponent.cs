// HealthComponent.cs
// Manages a unit's hit points.
// Implements IDamageable so other systems can interact through the interface
// without knowing about the concrete component.

using System;
using UnityEngine;
using CheckmateRPG.Core;
using CheckmateRPG.Data;

namespace CheckmateRPG.Components
{
    /// <summary>
    /// Tracks current and maximum health for a unit.
    /// Raises events that other components (e.g. UnitBrain) can listen to.
    /// </summary>
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        // ─── Events ───────────────────────────────────────────────────────────────

        /// <summary>Raised whenever health changes. Parameters: (currentHp, maxHp).</summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>Raised once when health reaches 0.</summary>
        public event Action OnDeath;

        // ─── State ────────────────────────────────────────────────────────────────

        private float _currentHealth;
        private float _maxHealth;
        private bool  _isDead;
        private float _damageTakenMultiplier = 1f;
        private float _defense;
        private float _resistance;
        private float _defenseBonus;
        private StatusEffectComponent _statusEffects;

        private void Awake()
        {
            _statusEffects = GetComponent<StatusEffectComponent>();
        }

        // ─── IDamageable ──────────────────────────────────────────────────────────

        public float CurrentHealth => _currentHealth;
        public float MaxHealth     => _maxHealth;
        public bool  IsDead        => _isDead;

        public void SetDamageTakenMultiplier(float multiplier)
        {
            _damageTakenMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetDefenseBonus(float bonus)
        {
            _defenseBonus = Mathf.Max(0f, bonus);
        }

        // ─── Initialisation ───────────────────────────────────────────────────────

        /// <summary>
        /// Initialise from a UnitData asset. Called by UnitBrain during setup.
        /// </summary>
        public void Initialise(UnitData data)
        {
            _maxHealth     = data.MaxHealth;
            _currentHealth = _maxHealth;
            _isDead        = false;
            _defense       = Mathf.Clamp01(data.Defense);
            _resistance    = Mathf.Clamp01(data.Resistance);
            _defenseBonus  = 0f;
        }

        // ─── IDamageable Implementation ───────────────────────────────────────────

        /// <summary>
        /// Applies damage that ignores the normal damage-taken multiplier.
        /// </summary>
        public void ApplyTrueDamage(float amount)
        {
            ApplyDamageInternal(amount, DamageType.True, ignoreDefense: true, ignoreDamageMultiplier: true, ignoreStatusModifiers: true);
        }

        /// <summary>
        /// Reduce health by <paramref name="amount"/>. Clamps to [0, MaxHealth].
        /// Triggers <see cref="OnDeath"/> if health reaches 0.
        /// </summary>
        public void TakeDamage(float amount)
        {
            ApplyDamageInternal(amount, DamageType.Physical);
        }

        public void ApplyMagicDamage(float amount)
        {
            ApplyDamageInternal(amount, DamageType.Magical);
        }

        public void ApplyDamage(float amount, DamageType damageType)
        {
            ApplyDamageInternal(amount, damageType);
        }

        /// <summary>
        /// Restore health by <paramref name="amount"/>. Clamps to [0, MaxHealth].
        /// </summary>
        public void Heal(float amount)
        {
            if (_isDead) return;

            amount = Mathf.Max(0f, amount);
            if (_statusEffects != null)
                amount *= _statusEffects.HealReceivedMultiplier;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        // ─── Private Helpers ──────────────────────────────────────────────────────

        private void Die()
        {
            _isDead = true;
            OnDeath?.Invoke();
        }

        private void ApplyDamageInternal(float amount, DamageType damageType, bool ignoreDefense = false, bool ignoreDamageMultiplier = false, bool ignoreStatusModifiers = false)
        {
            if (_isDead) return;

            amount = Mathf.Max(0f, amount);

            if (!ignoreStatusModifiers && _statusEffects != null)
                amount = _statusEffects.ModifyIncomingDamage(amount, damageType);

            if (!ignoreDefense)
                amount = ApplyDefense(amount, damageType);

            if (!ignoreDamageMultiplier)
                amount *= _damageTakenMultiplier;

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0f)
                Die();
        }

        private float ApplyDefense(float amount, DamageType damageType)
        {
            if (damageType == DamageType.True)
                return amount;

            float reduction = damageType == DamageType.Physical
                ? Mathf.Clamp01(_defense + _defenseBonus)
                : _resistance;
            if (_statusEffects != null)
                reduction *= damageType == DamageType.Physical ? _statusEffects.DefenseMultiplier : _statusEffects.ResistanceMultiplier;

            reduction = Mathf.Clamp01(reduction);
            return amount * (1f - reduction);
        }
    }
}
