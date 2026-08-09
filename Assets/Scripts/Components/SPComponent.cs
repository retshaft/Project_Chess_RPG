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

        public void Initialise(UnitData data)
        {
            _maxSP = data.MaxSP;
            _currentSP = Mathf.Min(data.InitSP, _maxSP);
            _chargeType = data.ChargeType;

            var activeSkill = data.GetSelectedActiveSkill();
            if (activeSkill != null && activeSkill.Levels != null && activeSkill.Levels.Count > 0)
            {
                var lvlData = activeSkill.GetLevelData(1); // 기본 1레벨 참조
                if (lvlData.SPCost > 0) _maxSP = lvlData.SPCost;
                if (lvlData.InitSP > 0) _currentSP = Mathf.Min(lvlData.InitSP, _maxSP);
                _chargeType = lvlData.ChargeType != SPChargeType.Auto ? lvlData.ChargeType : activeSkill.DefaultChargeType;
            }

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
            if (_isSkillActive || _maxSP <= 0f) return;
            if (_health != null && _health.IsDead) return;

            if (_chargeType == SPChargeType.Auto)
            {
                AddSP(_autoChargePerSecond * Time.deltaTime);
            }
        }

        private void HandleAttackPerformed(GameObject target)
        {
            if (_isSkillActive || _maxSP <= 0f) return;
            if (_chargeType == SPChargeType.OnAttack)
            {
                AddSP(_chargePerHitOrAttack);
            }
        }

        private void HandleDamageTaken(float amount, Core.DamageType type)
        {
            if (_isSkillActive || _maxSP <= 0f) return;
            if (_chargeType == SPChargeType.OnHit && amount > 0)
            {
                AddSP(_chargePerHitOrAttack);
            }
        }

        public void AddSP(float amount)
        {
            if (_isSkillActive || amount <= 0f) return;

            _currentSP = Mathf.Min(_currentSP + amount, _maxSP);
            OnSPChanged?.Invoke(_currentSP, _maxSP);

            if (_currentSP >= _maxSP)
            {
                _currentSP = 0f; // Reset SP when full
                OnSPChanged?.Invoke(_currentSP, _maxSP);
                _isSkillActive = true;
                OnSPFull?.Invoke();
            }
        }

        public void SetSkillActive(bool active)
        {
            _isSkillActive = active;
        }

        public void SetCurrentSP(float value)
        {
            _currentSP = Mathf.Clamp(value, 0f, _maxSP);
            OnSPChanged?.Invoke(_currentSP, _maxSP);
        }

        public void ResetSP(float val)
        {
            _isSkillActive = false;
            _currentSP = Mathf.Clamp(val, 0f, _maxSP);
            OnSPChanged?.Invoke(_currentSP, _maxSP);
        }
    }
}
