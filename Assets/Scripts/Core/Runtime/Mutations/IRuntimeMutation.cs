using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public interface IMutation
    {
        Guid MutationId { get; }
        Guid TargetId { get; }
    }

    public interface IRuntimeMutation : IMutation
    {
    }
}
