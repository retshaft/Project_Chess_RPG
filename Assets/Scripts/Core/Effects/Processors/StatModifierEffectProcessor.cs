using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.StatModifiers;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Effects.Processors
{
    public sealed class StatModifierEffectProcessor : IEffectProcessor
    {
        private readonly Dictionary<string, StatModifierProfileSO> _profiles = new();

        public StatModifierEffectProcessor()
        {
            // Resources 폴더에서 모든 StatModifierProfileSO 로드
            var loadedProfiles = Resources.LoadAll<StatModifierProfileSO>("StatModifiers");
            foreach (var profile in loadedProfiles)
            {
                if (!string.IsNullOrWhiteSpace(profile.EffectId))
                {
                    _profiles[profile.EffectId] = profile;
                }
            }
        }

        public bool CanProcess(IReadOnlyEffectRuntimeState effect)
        {
            return effect != null && _profiles.ContainsKey(effect.EffectId);
        }

        public void OnApplied(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            if (context.TryGetUnit(effect.TargetId, out UnitBrain unit))
            {
                var modComp = unit.GetComponent<UnitStatModifierComponent>();
                if (modComp != null && _profiles.TryGetValue(effect.EffectId, out var profile))
                {
                    var finalModifiers = profile.Modifiers;
                    
                    if (effect.StatOverrides != null && effect.StatOverrides.Count > 0)
                    {
                        finalModifiers = new System.Collections.Generic.List<StatModifierEntry>(profile.Modifiers.Count);
                        for (int i = 0; i < profile.Modifiers.Count; i++)
                        {
                            var entry = profile.Modifiers[i];
                            float val = entry.Value;
                            
                            foreach (var over in effect.StatOverrides)
                            {
                                if (over.TargetType == entry.Type)
                                {
                                    val = over.OverriddenValue;
                                    break;
                                }
                            }
                            
                            finalModifiers.Add(new StatModifierEntry
                            {
                                Type = entry.Type,
                                Value = val,
                                ConsumptionPolicy = entry.ConsumptionPolicy
                            });
                        }
                    }
                    
                    modComp.ApplyModifiers(effect.EffectId, finalModifiers, effect.StackCount);
                }
            }
        }

        public EffectProcessorResult OnTick(
            EffectSystemContext context,
            EffectMutationContext mutationContext,
            EffectMutationFactory mutationFactory,
            IReadOnlyEffectRuntimeState effect)
        {
            return EffectProcessorResult.Empty;
        }

        public void OnExpired(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            if (context.TryGetUnit(effect.TargetId, out UnitBrain unit))
            {
                var modComp = unit.GetComponent<UnitStatModifierComponent>();
                modComp?.RemoveModifiers(effect.EffectId);
            }
        }
    }
}
