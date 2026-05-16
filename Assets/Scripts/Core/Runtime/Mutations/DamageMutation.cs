using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct DamageMutation(
        Guid MutationId,
        Guid TargetId,
        Guid SourceId,
        int Amount,
        bool IsCritical,
        MutationContext Context = default,
        DamageType DamageType = DamageType.True,
        bool IsTrueDamage = false) : IRuntimeMutation;
}
