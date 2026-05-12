using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct DeathMutation(
        Guid MutationId,
        Guid TargetId,
        Guid SourceId,
        int Tick) : IRuntimeMutation;
}
