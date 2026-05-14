using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Replay
{
    public delegate IReadOnlyList<IRuntimeMutation> ReplayEntryExecutor(ActionJournalEntry entry);

    public sealed class ReplayRuntime
    {
        private readonly ActionJournal _actionJournal;

        public ReplayRuntime(ActionJournal actionJournal)
        {
            _actionJournal = actionJournal ?? throw new ArgumentNullException(nameof(actionJournal));
        }

        public MutationJournal Execute(ReplayEntryExecutor executor)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            var replayMutationJournal = new MutationJournal();
            IReadOnlyList<ActionJournalEntry> entries = _actionJournal.GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                ActionJournalEntry entry = entries[i];
                if (entry.EntryType == ActionJournalEntryType.TickAdvance)
                    continue;

                IReadOnlyList<IRuntimeMutation> resolvedMutations = executor(entry);
                replayMutationJournal.RecordCommit(entry.Tick, resolvedMutations);
            }

            return replayMutationJournal;
        }
    }
}
