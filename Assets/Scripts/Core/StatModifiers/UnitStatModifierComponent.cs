using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Core;

namespace CheckmateRPG.Core.StatModifiers
{
    public class ActiveModifier
    {
        public StatModifierType Type;
        public float Value;
        public ModifierConsumptionPolicy Policy;
        public int Stacks;
    }

    public class UnitStatModifierComponent : MonoBehaviour
    {
        // EffectId -> 활성 수정자 목록
        private readonly Dictionary<string, List<ActiveModifier>> _activeModifiers = new();

        public void ApplyModifiers(string effectId, List<StatModifierEntry> entries, int stacks)
        {
            if (!_activeModifiers.TryGetValue(effectId, out var activeList))
            {
                activeList = new List<ActiveModifier>();
                _activeModifiers[effectId] = activeList;
            }

            activeList.Clear();
            foreach (var entry in entries)
            {
                activeList.Add(new ActiveModifier
                {
                    Type = entry.Type,
                    Value = entry.Value,
                    Policy = entry.ConsumptionPolicy,
                    Stacks = stacks
                });
            }
        }

        public void RemoveModifiers(string effectId)
        {
            _activeModifiers.Remove(effectId);
        }

        // ── 집계 API ──
        public float GetAttackDamageMultiplier()
        {
            float multiplier = 1f;
            foreach (var kvp in _activeModifiers)
            {
                foreach (var mod in kvp.Value)
                {
                    if (mod.Stacks > 0 && mod.Type == StatModifierType.AttackDamageMultiplier)
                        multiplier *= mod.Value;
                }
            }
            return multiplier;
        }

        public int GetAttackCountOverride()
        {
            int maxOverride = -1;
            foreach (var kvp in _activeModifiers)
            {
                foreach (var mod in kvp.Value)
                {
                    if (mod.Stacks > 0 && mod.Type == StatModifierType.AttackCountOverride)
                        maxOverride = Mathf.Max(maxOverride, Mathf.RoundToInt(mod.Value));
                }
            }
            return maxOverride;
        }

        public float GetAttackDamageRatioOverride()
        {
            float ratio = -1f;
            foreach (var kvp in _activeModifiers)
            {
                foreach (var mod in kvp.Value)
                {
                    if (mod.Stacks > 0 && mod.Type == StatModifierType.AttackDamageRatioOverride)
                        ratio = Mathf.Max(ratio, mod.Value);
                }
            }
            return ratio;
        }

        public float GetDefPenetration()
        {
            float total = 0f;
            foreach (var kvp in _activeModifiers)
            {
                foreach (var mod in kvp.Value)
                {
                    if (mod.Stacks > 0 && mod.Type == StatModifierType.DefPenetrationFlat)
                        total += mod.Value;
                }
            }
            return total;
        }

        public float GetAPCostFlat()
        {
            float total = 0f;
            foreach (var kvp in _activeModifiers)
            {
                foreach (var mod in kvp.Value)
                {
                    if (mod.Stacks > 0 && mod.Type == StatModifierType.APCostFlat)
                        total += mod.Value;
                }
            }
            return total;
        }

        public float GetAPCostMultiplier()
        {
            float multiplier = 1f;
            foreach (var kvp in _activeModifiers)
            {
                foreach (var mod in kvp.Value)
                {
                    if (mod.Stacks > 0 && mod.Type == StatModifierType.APCostMultiplier)
                        multiplier *= mod.Value;
                }
            }
            return multiplier;
        }

        public float GetAttackCooldownMultiplier()
        {
            float multiplier = 1f;
            foreach (var kvp in _activeModifiers)
            {
                foreach (var mod in kvp.Value)
                {
                    if (mod.Stacks > 0 && mod.Type == StatModifierType.AttackCooldownMultiplier)
                        multiplier *= mod.Value;
                }
            }
            return multiplier;
        }

        public bool HasOnHitEffect(StatModifierType type, out int outValue)
        {
            outValue = 0;
            bool found = false;
            foreach (var kvp in _activeModifiers)
            {
                foreach (var mod in kvp.Value)
                {
                    if (mod.Stacks > 0 && mod.Type == type)
                    {
                        outValue += Mathf.RoundToInt(mod.Value);
                        found = true;
                    }
                }
            }
            return found;
        }

        // ── 소모 처리 ──
        public void ConsumeOnAttack()
        {
            ConsumeByPolicy(ModifierConsumptionPolicy.ConsumeOnAttack);
        }

        public void ConsumeOnAction()
        {
            ConsumeByPolicy(ModifierConsumptionPolicy.ConsumeOnAction);
        }

        private void ConsumeByPolicy(ModifierConsumptionPolicy policy)
        {
            List<string> emptyEffects = new List<string>();

            foreach (var kvp in _activeModifiers)
            {
                bool hasModOfPolicy = false;
                foreach (var mod in kvp.Value)
                {
                    if (mod.Policy == policy)
                    {
                        hasModOfPolicy = true;
                        mod.Stacks--;
                    }
                }

                // 해당 정책의 모디파이어가 있으면서 스택이 0이하라면 해당 EffectId 전체 만료 (버프 소모 처리)
                // 만약 이펙트 시스템과의 연동이 필요하다면 EffectSystem 쪽으로 Expire 메시지를 보내는 것이 맞으나,
                // 스탯 컴포넌트 내에서 로컬하게 무시되도록 스택을 0으로 만들어도 동작함.
                if (hasModOfPolicy)
                {
                    bool allExpired = true;
                    foreach (var mod in kvp.Value)
                    {
                        if (mod.Policy == policy && mod.Stacks > 0)
                        {
                            allExpired = false;
                        }
                    }
                    if (allExpired)
                    {
                        emptyEffects.Add(kvp.Key);
                    }
                }
            }

            // Note: 실제 이펙트 타이머(Duration)는 EffectSystem에서 돌아가므로,
            // 여기서 RemoveModifiers를 하면 EffectSystem과 상태 불일치가 날 수 있습니다.
            // 하지만 Get 연산에서 Stacks > 0 조건을 추가하여 논리적으로 소모시킬 수 있습니다.
            
            // 현재 구조에서는 간단하게 Remove 처리하거나, EffectSystem에 만료를 요청해야 합니다.
            // 가장 안전한 방법은 ActionRuntimeController.SimulationRuntime.TryGetMutableEffect를 찾아
            // RemainingTick을 0으로 만드는 것입니다. 하지만 Component에서 접근하기 어려우므로,
            // ActionRuntimeController의 이벤트 버스를 타거나, Get 연산에서 Stacks 확인을 추가하겠습니다.
            
            // 이 구현에서는 Remove해버리는 방식을 채택합니다.
            foreach (var effectId in emptyEffects)
            {
                // 로컬에서 삭제
                _activeModifiers.Remove(effectId);

                // TODO: ActionRuntimeController.Instance.EffectSystem.RemoveEffect(effectId)
                // EffectSystem은 AdvanceTick 등에서 자연스럽게 만료시키지만 즉시 만료가 필요할 수 있음.
                // 여기서는 EffectSystem에 직접 접근하는 결합도를 낮추기 위해 상태 지우기로만 처리
            }
        }
    }
}
