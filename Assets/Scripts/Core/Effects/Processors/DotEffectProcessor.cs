using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;
using UnityEngine;

namespace CheckmateRPG.Core.Effects.Processors
{
    public sealed class DotEffectProcessor : IEffectProcessor
    {
        private readonly IReadOnlyDictionary<string, float> _damageRatioPerTick;

        public DotEffectProcessor(IReadOnlyDictionary<string, float> damageRatioPerTick)
        {
            _damageRatioPerTick = damageRatioPerTick ?? new Dictionary<string, float>();
        }

        public bool CanProcess(IReadOnlyEffectRuntimeState effect)
        {
            return effect != null && _damageRatioPerTick.ContainsKey(effect.EffectId);
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
            if (!_damageRatioPerTick.TryGetValue(effect.EffectId, out float ratio) || ratio <= 0f)
                return EffectProcessorResult.Empty;

            if (!mutationContext.TargetUnit.Exists || mutationContext.TargetUnit.IsDead || mutationContext.TargetUnit.MaxHp <= 0)
                return EffectProcessorResult.Empty;

            float stackScaledRatio = ratio * Mathf.Max(1, effect.StackCount) * Mathf.Max(0f, effect.Magnitude);
            float damage = mutationContext.TargetUnit.MaxHp * stackScaledRatio;
            if (damage <= 0f)
                return EffectProcessorResult.Empty;

            int amount = Mathf.RoundToInt(damage);
            IRuntimeMutation mutation = mutationFactory?.CreateDamage(mutationContext, amount, DamageType.Magical);
            if (mutation == null)
                return EffectProcessorResult.Empty;

            return new EffectProcessorResult(
                new[] { mutation },
                -amount);
        }

        public void OnExpired(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            _ = context;
            _ = effect;
        }
    }
}
