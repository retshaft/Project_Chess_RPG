using System;

namespace CheckmateRPG.Core.Replay
{
    public readonly record struct ActionJournalEntry(
        int Tick,
        int Sequence,
        ActionJournalEntryType EntryType,
        Guid ActionId,
        Guid ActorId,
        int ScheduledTick,
        string ReservationResult,
        int ResolveOrder,
        string Details);
}
