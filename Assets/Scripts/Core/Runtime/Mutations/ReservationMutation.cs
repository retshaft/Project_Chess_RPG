using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public enum ReservationMutationOperation
    {
        Reserve = 0,
        Release = 1,
        Confirm = 2,
        Cancel = 3
    }

    public readonly record struct ReservationMutation(
        Guid MutationId,
        Guid TargetId,
        string ReservationKey,
        ReservationMutationOperation Operation,
        int Tick,
        string Scope = "") : IRuntimeMutation;
}
