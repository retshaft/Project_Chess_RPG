using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct MutationContext(
        int Tick,
        Guid SourceAction,
        Guid TargetRuntime,
        string MutationReason)
    {
        public static MutationContext Empty => new(0, Guid.Empty, Guid.Empty, string.Empty);
    }
}
