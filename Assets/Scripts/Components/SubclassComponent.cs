using System.Collections;
using UnityEngine;
using CheckmateRPG.Data;
using CheckmateRPG.Core;
using CheckmateRPG.Units;

namespace CheckmateRPG.Components
{
    public class SubclassComponent : MonoBehaviour
    {
        private UnitBrain _unitBrain;
        private SPComponent _sp;
        private HealthComponent _health;
        private CombatComponent _combat;
        
        private UnitSubclassType _subclass;

        public void Initialise(UnitData data)
        {
            _subclass = data.Subclass;
            _unitBrain = GetComponent<UnitBrain>();
            _sp = GetComponent<SPComponent>();
            _health = GetComponent<HealthComponent>();
            _combat = GetComponent<CombatComponent>();

            // Vanguard Passive: Generate AP over time
            if (_subclass == UnitSubclassType.Vanguard)
            {
                StartCoroutine(VanguardPassiveRoutine());
            }

            if (_combat != null)
            {
                _combat.OnAttackPerformed += HandleAttackPerformed;
                _combat.OnDamageDealtWithHitCounter += HandleHitPerformed;
            }

            if (_sp != null)
            {
                _sp.OnSPFull += HandleSPFull;
            }
        }

        public bool IsDefender()
        {
            return _subclass == UnitSubclassType.Defender;
        }

        private void OnDestroy()
        {
            if (_combat != null)
            {
                _combat.OnAttackPerformed -= HandleAttackPerformed;
                _combat.OnDamageDealtWithHitCounter -= HandleHitPerformed;
            }
            if (_sp != null)
            {
                _sp.OnSPFull -= HandleSPFull;
            }
        }

        private void HandleAttackPerformed(GameObject target)
        {
            if (_subclass == UnitSubclassType.Charger && target != null)
            {
                if (target.TryGetComponent(out HealthComponent targetHealth) && targetHealth.IsDead)
                {
                    if (APManager.Instance != null && !IsEnemy())
                    {
                        APManager.Instance.AddAP(15f, APSource.Refund);
                    }
                }
            }
        }

        private void HandleHitPerformed(GameObject target, float dmg, int hitCount)
        {
            // 키아라(Kiara) 및 출혈 패시브 처리: 매 3회 타격마다 출혈 1회 부여 (Kiara.md 기준)
            if (_unitBrain != null && _unitBrain.UnitData != null)
            {
                string uName = _unitBrain.UnitData.UnitName;
                if (uName.Contains("Kiara") || uName.Contains("키아라"))
                {
                    if (hitCount > 0 && hitCount % 3 == 0 && target != null)
                    {
                        if (target.TryGetComponent(out StatusEffectComponent status))
                        {
                            status.ApplyStatusEffect(StatusEffectType.Bleed, 10f, 1);
                            Debug.Log($"[SubclassComponent] Kiara Passive 1 triggered! Applied Bleed to {target.name} on hit #{hitCount}.");
                        }
                    }
                }
            }
        }

        private void HandleSPFull()
        {
            if (_subclass == UnitSubclassType.Flagbearer)
            {
                StartCoroutine(FlagbearerSkillRoutine());
            }
            else if (_unitBrain != null && _unitBrain.UnitData != null)
            {
                var activeSkill = _unitBrain.UnitData.GetSelectedActiveSkill();
                if (activeSkill != null)
                {
                    StartCoroutine(GenericActiveSkillRoutine(activeSkill));
                    return;
                }
                if (_sp != null) _sp.SetSkillActive(false);
            }
            else
            {
                if (_sp != null) _sp.SetSkillActive(false);
            }
        }

        private IEnumerator GenericActiveSkillRoutine(AbilityDefinition skill)
        {
            Debug.Log($"[SubclassComponent] {_unitBrain.gameObject.name} casting {skill.SkillName}!");
            
            // Phase 6: EX Skill Cut-In 연출 트리거 (존재하는 경우)
            var cutIn = UnityEngine.Object.FindAnyObjectByType<CheckmateRPG.UI.SkillCutInUI>();
            if (cutIn != null)
            {
                cutIn.ShowCutIn(_unitBrain.UnitData.UnitName, skill.SkillName, _unitBrain.UnitData.Icon);
            }

            var modComp = GetComponent<CheckmateRPG.Core.StatModifiers.UnitStatModifierComponent>();
            if (modComp == null) modComp = gameObject.AddComponent<CheckmateRPG.Core.StatModifiers.UnitStatModifierComponent>();

            // 스킬 프로필 적용 (예: 키아라 3스킬 폼 전환 24초 유지)
            string profileId = $"{skill.AbilityId}_Buff";
            var profile = Resources.Load<CheckmateRPG.Core.StatModifiers.StatModifierProfileSO>($"StatModifiers/{profileId}");
            if (profile != null && modComp != null)
            {
                modComp.ApplyModifiers(profileId, profile.Modifiers, 1);
                Debug.Log($"[SubclassComponent] Applied modifier profile {profileId} to {_unitBrain.gameObject.name}.");
            }

            // 지속 시간 로직 (키아라 3스킬은 24초, 일반 버프는 10초 또는 1회성)
            float duration = skill.AbilityId.Contains("Active3") ? 24f : (skill.AbilityId.Contains("Active1") ? 5f : 12f);
            yield return new WaitForSeconds(duration);

            if (profile != null && modComp != null)
            {
                modComp.RemoveModifiers(profileId);
                Debug.Log($"[SubclassComponent] Removed modifier profile {profileId} from {_unitBrain.gameObject.name}.");
            }

            if (_sp != null) _sp.SetSkillActive(false);
        }

        private IEnumerator FlagbearerSkillRoutine()
        {
            Debug.Log($"[SubclassComponent] {_unitBrain.gameObject.name} (Flagbearer) activating skill!");

            // 1. Lock movement and attack
            if (_statusEffects == null) _statusEffects = GetComponent<StatusEffectComponent>();
            
            // Temporary way to disable actions (usually handled by a "Disarmed" and "Rooted" status or ActionScheduler lock)
            // We'll apply a conceptual lock via StatusEffect or custom flag. 
            // For this skeleton, we assume UnitBrain respects some state.
            
            float duration = 18f;
            float elapsed = 0f;
            float tickRate = 1f;
            float nextTick = 1f;
            float totalAPToGive = 65f;
            float apPerTick = totalAPToGive / duration;

            while (elapsed < duration)
            {
                if (_health != null && _health.IsDead) break;

                elapsed += Time.deltaTime;
                if (elapsed >= nextTick)
                {
                    nextTick += tickRate;
                    if (APManager.Instance != null && !IsEnemy())
                    {
                        APManager.Instance.AddAP(apPerTick, APSource.Bonus);
                    }
                }
                yield return null;
            }

            Debug.Log($"[SubclassComponent] {_unitBrain.gameObject.name} (Flagbearer) skill finished.");
            if (_sp != null) _sp.SetSkillActive(false);
        }

        private StatusEffectComponent _statusEffects;

        private IEnumerator VanguardPassiveRoutine()
        {
            while (true)
            {
                if (_health != null && _health.IsDead) yield break;
                
                yield return new WaitForSeconds(1f);
                
                if (APManager.Instance != null && !IsEnemy())
                {
                    APManager.Instance.AddAP(0.8f, APSource.Bonus);
                }
            }
        }

        private bool IsEnemy()
        {
            if (TryGetComponent(out TeamComponent team))
                return team.IsEnemy;
            return false;
        }
    }
}
