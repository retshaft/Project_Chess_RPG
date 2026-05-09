using System;

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
        float Magnitude) : IRuntimeMutation;
}
