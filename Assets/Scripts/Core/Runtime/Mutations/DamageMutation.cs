using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct DamageMutation(
        Guid MutationId,
        Guid TargetId,
        Guid SourceId,
        int Amount,
        bool IsCritical) : IRuntimeMutation;
}
