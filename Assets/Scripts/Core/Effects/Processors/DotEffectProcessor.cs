using System.Collections.Generic;
using CheckmateRPG.Components;
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

        public int OnTick(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            if (!context.TryGetUnit(effect.TargetId, out var target) ||
                target == null ||
                !target.TryGetComponent(out HealthComponent health) ||
                health.IsDead)
            {
                return 0;
            }

            if (!_damageRatioPerTick.TryGetValue(effect.EffectId, out float ratio) || ratio <= 0f)
                return 0;

            float stackScaledRatio = ratio * Mathf.Max(1, effect.StackCount) * Mathf.Max(0f, effect.Magnitude);
            float damage = health.MaxHealth * stackScaledRatio;
            if (damage <= 0f)
                return 0;

            health.ApplyMagicDamage(damage);
            return -Mathf.RoundToInt(damage);
        }

        public void OnExpired(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            _ = context;
            _ = effect;
        }
    }
}
