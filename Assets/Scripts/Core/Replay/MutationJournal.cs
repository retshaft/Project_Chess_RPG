using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Replay
{
    public sealed class MutationJournal
    {
        private readonly List<MutationJournalEntry> _entries = new();
        private readonly Dictionary<int, int> _commitOrderByTick = new();
        private int _nextSequence;

        public int Count => _entries.Count;

        public void RecordCommit(int tick, IReadOnlyList<IRuntimeMutation> mutations, MutationJournalResult result = MutationJournalResult.Applied)
        {
            if (mutations == null || mutations.Count == 0)
                return;

            int commitOrder = GetNextCommitOrder(tick);
            for (int i = 0; i < mutations.Count; i++)
            {
                IRuntimeMutation mutation = mutations[i];
                if (mutation == null)
                    continue;

                _entries.Add(new MutationJournalEntry(
                    tick,
                    commitOrder,
                    _nextSequence++,
                    i,
                    mutation.MutationId,
                    mutation.TargetId,
                    mutation.GetType().Name,
                    result));
            }
        }

        public IReadOnlyList<MutationJournalEntry> GetEntries()
        {
            return _entries;
        }

        public IReadOnlyList<MutationJournalEntry> GetEntriesForTick(int tick)
        {
            var matches = new List<MutationJournalEntry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                MutationJournalEntry entry = _entries[i];
                if (entry.Tick == tick)
                    matches.Add(entry);
            }

            return matches;
        }

        public void Clear()
        {
            _entries.Clear();
            _commitOrderByTick.Clear();
            _nextSequence = 0;
        }

        private int GetNextCommitOrder(int tick)
        {
            if (_commitOrderByTick.TryGetValue(tick, out int currentOrder))
            {
                int nextOrder = currentOrder + 1;
                _commitOrderByTick[tick] = nextOrder;
                return nextOrder;
            }

            _commitOrderByTick[tick] = 0;
            return 0;
        }
    }
}
