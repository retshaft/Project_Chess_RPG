using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct GrabMutation(
        Guid MutationId,
        Guid TargetId,
        Guid SourceId,
        int Force,
        MutationContext Context = default) : IRuntimeMutation;
}
