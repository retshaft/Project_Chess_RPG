using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Runtime.Mutations;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class EffectMutationProcessor
    {
        private readonly Func<EffectRuntimeState, bool> _applyEffect;

        public EffectMutationProcessor(Func<EffectRuntimeState, bool> applyEffect)
        {
            _applyEffect = applyEffect ?? throw new ArgumentNullException(nameof(applyEffect));
        }

        public IReadOnlyList<IGameEvent> Apply(ApplyEffectMutation mutation)
        {
            if (string.IsNullOrWhiteSpace(mutation.EffectId) || mutation.TargetId == Guid.Empty)
                return Array.Empty<IGameEvent>();

            var state = new EffectRuntimeState
            {
                EffectId = mutation.EffectId,
                SourceId = mutation.SourceId,
                TargetId = mutation.TargetId,
                RemainingTick = Mathf.Max(1, mutation.DurationTicks),
                TickInterval = Mathf.Max(1, mutation.TickInterval),
                NextTickIn = Mathf.Clamp(mutation.InitialTickIn, 1, Mathf.Max(1, mutation.TickInterval)),
                StackCount = Mathf.Max(1, mutation.StackCount),
                Magnitude = Mathf.Max(0f, mutation.Magnitude)
            };

            _ = _applyEffect(state);
            return Array.Empty<IGameEvent>();
        }
    }
}
