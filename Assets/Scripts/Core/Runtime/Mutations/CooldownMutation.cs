using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct CooldownMutation(
        Guid MutationId,
        Guid TargetUnitId,
        string TargetAbilityId,
        float DurationChange,
        MutationContext Context = default) : IRuntimeMutation
    {
        public Guid TargetId => TargetUnitId;
    }
}
