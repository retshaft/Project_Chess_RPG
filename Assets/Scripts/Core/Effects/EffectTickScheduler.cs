using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Effects
{
    public sealed class EffectTickScheduler
    {
        private readonly EffectTimingPipeline _timingPipeline;

        public EffectTickScheduler(EffectTimingPipeline timingPipeline)
        {
            _timingPipeline = timingPipeline ?? throw new ArgumentNullException(nameof(timingPipeline));
        }

        public IReadOnlyList<EffectTickResult> ExecuteTick(
            int currentTick,
            IReadOnlyDictionary<string, EffectRuntimeState> activeEffects,
            Func<IReadOnlyEffectRuntimeState, IEffectProcessor> processorResolver,
            EffectSystemContext effectContext)
        {
            return _timingPipeline.RunPhase(
                EffectTimingPhase.OnTickEnd,
                currentTick,
                activeEffects,
                processorResolver,
                effectContext);
        }
    }
}
