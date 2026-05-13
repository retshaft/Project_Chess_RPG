using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct HealMutation(
        Guid MutationId,
        Guid TargetId,
        Guid SourceId,
        int Amount,
        MutationContext Context = default) : IRuntimeMutation;
}
