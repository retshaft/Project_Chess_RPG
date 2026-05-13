using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct AbilityActionCompleteMutation(
        Guid MutationId,
        Guid TargetId,
        Guid ActionId,
        string AbilityId,
        int Tick,
        MutationContext Context = default) : IRuntimeMutation;
}
