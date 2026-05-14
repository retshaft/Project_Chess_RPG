using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Effects
{
    public readonly record struct EffectProcessorResult(
        IReadOnlyList<IRuntimeMutation> Mutations,
        int DeltaHp = 0)
    {
        public static EffectProcessorResult Empty =>
            new(Array.Empty<IRuntimeMutation>(), 0);
    }

    public interface IEffectProcessor
    {
        bool CanProcess(IReadOnlyEffectRuntimeState effect);
        void OnApplied(EffectSystemContext context, IReadOnlyEffectRuntimeState effect);
        EffectProcessorResult OnTick(
            EffectSystemContext context,
            EffectMutationContext mutationContext,
            EffectMutationFactory mutationFactory,
            IReadOnlyEffectRuntimeState effect);
        void OnExpired(EffectSystemContext context, IReadOnlyEffectRuntimeState effect);
    }
}
