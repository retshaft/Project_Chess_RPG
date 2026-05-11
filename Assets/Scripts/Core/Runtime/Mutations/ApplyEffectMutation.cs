using System;
using CheckmateRPG.Core;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct ApplyEffectMutation(
        Guid MutationId,
        string EffectId,
        Guid SourceId,
        Guid TargetId,
        int DurationTicks,
        int TickInterval,
        int InitialTickIn,
        int StackCount,
        float Magnitude,
        CheckmateRPG.Core.Effects.EffectTimingPhase TimingPhase = CheckmateRPG.Core.Effects.EffectTimingPhase.OnTickEnd,
        ActionSpeedTier ActionSpeedLevel = ActionSpeedTier.Normal,
        bool IsReaction = false) : IRuntimeMutation;
}
