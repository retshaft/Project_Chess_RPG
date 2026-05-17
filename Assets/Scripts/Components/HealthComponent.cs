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
        private bool _isDead;
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
        public float MaxHealth => _maxHealth;
        public bool IsDead => _isDead;

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
            _maxHealth = data.MaxHealth;
            _currentHealth = _maxHealth;
            _isDead = false;
            // 기획 변경에 따라 물리 방어력은 0~1(%)이 아닌 실제 절대값 수치를 사용할 수 있도록 제한(Clamp01)을 해제하거나 기획 수치에 맞게 조정해야 합니다.
            // 일단 기존 코드 형태를 유지하되, 차감 연산이 정상 동작하도록 수정합니다. (실제 데이터 에셋의 스탯 값이 0~1 사이인지, 절대값인지 확인 필요)
            _defense = data.Defense;
            _resistance = Mathf.Clamp01(data.Resistance); // 저항력은 % 감소이므로 0~1 유지
            _defenseBonus = 0f;
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

        public void TakeDamage(float amount, float defPenetrationRatio)
        {
            ApplyDamageInternal(amount, DamageType.Physical, defensePenetrationRatio: defPenetrationRatio);
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

        private void ApplyDamageInternal(
            float amount,
            DamageType damageType,
            bool ignoreDefense = false,
            bool ignoreDamageMultiplier = false,
            bool ignoreStatusModifiers = false,
            float defensePenetrationRatio = 0f)
        {
            if (_isDead) return;

            amount = Mathf.Max(0f, amount);

            if (!ignoreStatusModifiers && _statusEffects != null)
                amount = _statusEffects.ModifyIncomingDamage(amount, damageType);

            if (!ignoreDefense)
                amount = ApplyDefense(amount, damageType, defensePenetrationRatio);

            if (!ignoreDamageMultiplier)
                amount *= _damageTakenMultiplier;

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0f)
                Die();
        }

        private float ApplyDefense(float amount, DamageType damageType, float defensePenetrationRatio = 0f)
        {
            if (damageType == DamageType.True)
                return amount;

            if (damageType == DamageType.Physical)
            {
                // 물리 방어력 연산: 피해량 - 방어력 (감산)
                float currentDefense = _defense + _defenseBonus;

                // 상태이상 등에 의한 방어력 비율 증감 적용 (예: 초전도에 의한 방어력 40% 감소)
                if (_statusEffects != null)
                    currentDefense *= _statusEffects.DefenseMultiplier;

                // 방어력이 0 미만이 되지 않도록 처리
                currentDefense = Mathf.Max(0f, currentDefense);
                float effectiveDefense = currentDefense * (1f - Mathf.Clamp01(defensePenetrationRatio));

                // 피해량에서 방어력을 차감하되, 피해량이 0 이하로 떨어지지 않도록 보정 (최소 1의 피해는 줄지, 완전히 막을지 기획 확인 필요. 여기선 최소 0으로 보정)
                return Mathf.Max(0f, amount - effectiveDefense);
            }
            else // DamageType.Magical
            {
                // 마법 저항력 연산: 피해량 * (1 - 저항력) (비율 감소)
                float currentResistance = _resistance;

                if (_statusEffects != null)
                    currentResistance *= _statusEffects.ResistanceMultiplier;

                currentResistance = Mathf.Clamp01(currentResistance);
                return amount * (1f - currentResistance);
            }
        }
    }
}
