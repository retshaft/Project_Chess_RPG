using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct SPMutation(
        Guid MutationId,
        Guid TargetUnitId,
        int Amount,
        MutationContext Context = default) : IRuntimeMutation
    {
        public Guid TargetId => TargetUnitId;
    }
}
