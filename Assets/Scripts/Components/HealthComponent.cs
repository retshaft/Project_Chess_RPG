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

        // ─── IDamageable ──────────────────────────────────────────────────────────

        public float CurrentHealth => _currentHealth;
        public float MaxHealth     => _maxHealth;
        public bool  IsDead        => _isDead;

        public void SetDamageTakenMultiplier(float multiplier)
        {
            _damageTakenMultiplier = Mathf.Max(0f, multiplier);
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
        }

        // ─── IDamageable Implementation ───────────────────────────────────────────

        /// <summary>
        /// Applies damage that ignores the normal damage-taken multiplier.
        /// </summary>
        public void ApplyTrueDamage(float amount)
        {
            if (_isDead) return;

            amount = Mathf.Max(0f, amount);
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0f)
                Die();
        }

        /// <summary>
        /// Reduce health by <paramref name="amount"/>. Clamps to [0, MaxHealth].
        /// Triggers <see cref="OnDeath"/> if health reaches 0.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (_isDead) return;

            amount = Mathf.Max(0f, amount);
            amount *= _damageTakenMultiplier;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0f)
                Die();
        }

        /// <summary>
        /// Restore health by <paramref name="amount"/>. Clamps to [0, MaxHealth].
        /// </summary>
        public void Heal(float amount)
        {
            if (_isDead) return;

            amount = Mathf.Max(0f, amount);
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        // ─── Private Helpers ──────────────────────────────────────────────────────

        private void Die()
        {
            _isDead = true;
            OnDeath?.Invoke();
        }
    }
}
