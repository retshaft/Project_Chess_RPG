using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;
using UnityEngine;

namespace CheckmateRPG.Core.Effects.Processors
{
    public sealed class HotEffectProcessor : IEffectProcessor
    {
        private readonly IReadOnlyDictionary<string, float> _healRatioPerTick;

        public HotEffectProcessor(IReadOnlyDictionary<string, float> healRatioPerTick)
        {
            _healRatioPerTick = healRatioPerTick ?? new Dictionary<string, float>();
        }

        public bool CanProcess(IReadOnlyEffectRuntimeState effect)
        {
            return effect != null && _healRatioPerTick.ContainsKey(effect.EffectId);
        }

        public void OnApplied(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            _ = context;
            _ = effect;
        }

        public EffectProcessorResult OnTick(
            EffectSystemContext context,
            EffectMutationContext mutationContext,
            EffectMutationFactory mutationFactory,
            IReadOnlyEffectRuntimeState effect)
        {
            if (!_healRatioPerTick.TryGetValue(effect.EffectId, out float ratio) || ratio <= 0f)
                return EffectProcessorResult.Empty;

            if (!mutationContext.TargetUnit.Exists || mutationContext.TargetUnit.IsDead || mutationContext.TargetUnit.MaxHp <= 0)
                return EffectProcessorResult.Empty;

            float stackScaledRatio = ratio * Mathf.Max(1, effect.StackCount) * Mathf.Max(0f, effect.Magnitude);
            float healAmount = mutationContext.TargetUnit.MaxHp * stackScaledRatio;
            if (healAmount <= 0f)
                return EffectProcessorResult.Empty;

            int amount = Mathf.RoundToInt(healAmount);
            IRuntimeMutation mutation = mutationFactory?.CreateHeal(mutationContext, amount);
            if (mutation == null)
                return EffectProcessorResult.Empty;

            return new EffectProcessorResult(
                new[] { mutation },
                amount);
        }

        public void OnExpired(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            _ = context;
            _ = effect;
        }
    }
}
