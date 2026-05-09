using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct DamageMutation(Guid TargetId, int Amount) : IRuntimeMutation;
}
