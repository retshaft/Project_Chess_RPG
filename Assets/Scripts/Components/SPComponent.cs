using System;
using UnityEngine;
using CheckmateRPG.Data;

namespace CheckmateRPG.Components
{
    public class SPComponent : MonoBehaviour
    {
        public event Action<float, float> OnSPChanged;
        public event Action OnSPFull;

        private float _currentSP;
        private float _maxSP;
        private SPChargeType _chargeType;
        private bool _isSkillActive;

        private CombatComponent _combat;
        private HealthComponent _health;

        // Auto charge rate
        private float _autoChargePerSecond = 1f;
        // Hit/Attack charge amount (Arknights standard: 1 SP per attack/hit)
        private float _chargePerHitOrAttack = 1f;

        public float CurrentSP => _currentSP;
        public float MaxSP => _maxSP;
        public bool IsReady => _currentSP >= _maxSP;
        private Units.UnitBrain _brain;

        public void Initialise(UnitData data)
        {
            OnDestroy(); // Reinitialisation must not duplicate combat listeners.
            _brain = GetComponent<Units.UnitBrain>();
            _maxSP = Mathf.Max(0, data.SkillMaxSP);
            _currentSP = Mathf.Clamp(data.SkillInitSP, 0, _maxSP);
            _chargeType = data.SkillChargeType;

            _isSkillActive = false;

            _combat = GetComponent<CombatComponent>();
            _health = GetComponent<HealthComponent>();

            if (_combat != null)
                _combat.OnAttackPerformed += HandleAttackPerformed;
            
            if (_health != null)
                _health.OnDamageTaken += HandleDamageTaken;

            OnSPChanged?.Invoke(_currentSP, _maxSP);
        }

        private void OnDestroy()
        {
            if (_combat != null)
                _combat.OnAttackPerformed -= HandleAttackPerformed;
            
            if (_health != null)
                _health.OnDamageTaken -= HandleDamageTaken;
        }

        private void Update()
        {
            if (_brain != null || (Core.TickScheduler.Instance != null && Core.TickScheduler.Instance.IsPaused)) return;
            if (_isSkillActive || _maxSP <= 0f) return;
            if (_health != null && _health.IsDead) return;

            if (_chargeType == SPChargeType.Auto)
            {
                AddSP(_autoChargePerSecond * Time.deltaTime);
            }
        }

        private void HandleAttackPerformed(GameObject target)
        {
            if (_brain != null) return; // Runtime mutations own battle SP.
            if (_isSkillActive || _maxSP <= 0f) return;
            if (_chargeType == SPChargeType.OnAttack)
            {
                AddSP(_chargePerHitOrAttack);
            }
        }

        private void HandleDamageTaken(float amount, Core.DamageType type)
        {
            if (_brain != null) return;
            if (_isSkillActive || _maxSP <= 0f) return;
            if (_chargeType == SPChargeType.OnHit && amount > 0)
            {
                AddSP(_chargePerHitOrAttack);
            }
        }

        public void AddSP(float amount)
        {
            if (_isSkillActive || amount <= 0f || _maxSP <= 0) return;
            SetCurrentSP(_currentSP + amount);
        }

        public void SetSkillActive(bool active)
        {
            _isSkillActive = active;
        }

        public void SetCurrentSP(float value)
        {
            bool wasReady = IsReady;
            if (Mathf.Approximately(_currentSP, Mathf.Clamp(value, 0f, _maxSP))) return;
            _currentSP = Mathf.Clamp(value, 0f, _maxSP);
            OnSPChanged?.Invoke(_currentSP, _maxSP);
            if (!wasReady && IsReady) OnSPFull?.Invoke();
        }

        public void ResetSP(float val)
        {
            _isSkillActive = false;
            _currentSP = Mathf.Clamp(val, 0f, _maxSP);
            OnSPChanged?.Invoke(_currentSP, _maxSP);
        }
    }
}
