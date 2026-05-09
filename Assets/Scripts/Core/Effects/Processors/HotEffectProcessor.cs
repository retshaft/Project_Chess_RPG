using System.Collections.Generic;
using CheckmateRPG.Components;
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

        public bool CanProcess(EffectRuntimeState effect)
        {
            return effect != null && _healRatioPerTick.ContainsKey(effect.EffectId);
        }

        public void OnApplied(EffectSystemContext context, EffectRuntimeState effect)
        {
            _ = context;
            _ = effect;
        }

        public int OnTick(EffectSystemContext context, EffectRuntimeState effect)
        {
            if (!context.TryGetUnit(effect.TargetId, out var target) ||
                target == null ||
                !target.TryGetComponent(out HealthComponent health) ||
                health.IsDead)
            {
                return 0;
            }

            if (!_healRatioPerTick.TryGetValue(effect.EffectId, out float ratio) || ratio <= 0f)
                return 0;

            float stackScaledRatio = ratio * Mathf.Max(1, effect.StackCount) * Mathf.Max(0f, effect.Magnitude);
            float healAmount = health.MaxHealth * stackScaledRatio;
            if (healAmount <= 0f)
                return 0;

            health.Heal(healAmount);
            return Mathf.RoundToInt(healAmount);
        }

        public void OnExpired(EffectSystemContext context, EffectRuntimeState effect)
        {
            _ = context;
            _ = effect;
        }
    }
}
