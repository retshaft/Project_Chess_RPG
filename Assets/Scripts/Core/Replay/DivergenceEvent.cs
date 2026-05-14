namespace CheckmateRPG.Core.Replay
{
    public enum DivergenceKind
    {
        Unknown = 0,
        ActionJournal = 1,
        MutationJournal = 2
    }

    public readonly record struct DivergenceEvent(
        DivergenceKind Kind,
        int Tick,
        string Message);
}
