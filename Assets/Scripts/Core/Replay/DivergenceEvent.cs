namespace CheckmateRPG.Core.Replay
{
    public enum DivergenceKind
    {
        Unknown = 0,
        ActionOrdering = 1,
        MutationSequence = 2,
        RuntimeSnapshot = 3,
        ReservationState = 4,
        OccupancyState = 5,
        EffectState = 6,
        DeterministicRule = 7,
        RuntimeInvariant = 8
    }

    public readonly record struct DivergenceEvent(
        DivergenceKind Kind,
        int Tick,
        DivergenceReason Reason,
        string Message);
}
