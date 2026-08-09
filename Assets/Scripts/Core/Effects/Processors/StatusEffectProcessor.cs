using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Effects.Processors
{
    public sealed class StatusEffectProcessor : IEffectProcessor
    {
        public bool CanProcess(IReadOnlyEffectRuntimeState effect)
        {
            if (effect == null) return false;
            return effect.EffectId == "Burn" ||
                   effect.EffectId == "Ignite" ||
                   effect.EffectId == "Poison" ||
                   effect.EffectId == "Bleed" ||
                   effect.EffectId == "Wound";
        }

        public void OnApplied(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            // 시각적 처리는 EventBus를 통해 StatusEffectComponent가 담당
        }

        public EffectProcessorResult OnTick(
            EffectSystemContext context,
            EffectMutationContext mutationContext,
            EffectMutationFactory mutationFactory,
            IReadOnlyEffectRuntimeState effect)
        {
            var mutations = new List<IRuntimeMutation>();

            if (effect.EffectId == "Bleed" || effect.EffectId == "Wound")
            {
                // 출혈이나 상처는 틱당 데미지를 주지 않고,
                // 스탯 페널티(상처의 경우)를 주거나 출혈 중첩을 통해 상처로 변환되는 로직을 외부나 OnApplied에서 처리
                return EffectProcessorResult.Empty;
            }

            if (context.TryGetUnit(effect.TargetId, out UnitBrain unit))
            {
                if (unit.Health != null && !unit.IsDead)
                {
                    // Magnitude는 M4에서 "기본 2% 틱 데미지의 배수"로 사용되었음.
                    // 원래 로직:
                    // Burn: MaxHP * 0.02 * Magnitude
                    // Ignite: MaxHP * 0.03 * Magnitude
                    // Poison: MaxHP * 0.02 * Magnitude

                    float baseRatio = 0f;
                    if (effect.EffectId == "Burn") baseRatio = 0.02f;
                    else if (effect.EffectId == "Ignite") baseRatio = 0.03f;
                    else if (effect.EffectId == "Poison") baseRatio = 0.02f;

                    float damagePercent = baseRatio * effect.Magnitude;
                    int damageAmount = Mathf.FloorToInt(unit.Health.MaxHealth * damagePercent);

                    if (damageAmount > 0)
                    {
                        var contextInfo = new MutationContext(
                            mutationContext.Tick,
                            Guid.Empty,
                            effect.TargetId,
                            effect.EffectId + "Damage");

                        mutations.Add(new DamageMutation(
                            SeededRandomProvider.Shared.NextGuid(),
                            effect.TargetId,
                            effect.SourceId,
                            damageAmount,
                            IsCritical: false,
                            Context: contextInfo,
                            DamageType: DamageType.Magical, // 원소 도트딜은 마법 피해
                            IsTrueDamage: effect.EffectId == "Poison" // 독은 트루뎀(선택적)
                        ));
                    }
                }
            }

            return new EffectProcessorResult(mutations);
        }

        public void OnExpired(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
        }
    }
}
