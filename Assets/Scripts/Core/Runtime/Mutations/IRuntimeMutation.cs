using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public interface IRuntimeMutation
    {
        Guid MutationId { get; }
        Guid TargetId { get; }
    }
}
