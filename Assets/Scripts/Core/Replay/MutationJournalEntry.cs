using System;

namespace CheckmateRPG.Core.Replay
{
    public readonly record struct MutationJournalEntry(
        int Tick,
        int CommitOrder,
        int Sequence,
        int SequenceInCommit,
        Guid MutationId,
        Guid TargetId,
        string MutationType,
        MutationJournalResult Result);
}
