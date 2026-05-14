using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Replay
{
    public sealed class ActionJournal
    {
        private readonly List<ActionJournalEntry> _entries = new();
        private int _nextSequence;

        public int Count => _entries.Count;

        public void RecordTick(int tick)
        {
            AddEntry(tick, ActionJournalEntryType.TickAdvance, Guid.Empty, Guid.Empty, -1, string.Empty, -1, string.Empty);
        }

        public void RecordActionRequest(int tick, Guid actionId, Guid actorId, string details, int scheduledTick = -1)
        {
            AddEntry(tick, ActionJournalEntryType.ActionRequest, actionId, actorId, scheduledTick, string.Empty, -1, details);
        }

        public void RecordReservation(int tick, Guid actionId, Guid actorId, string details, string reservationResult = "")
        {
            AddEntry(tick, ActionJournalEntryType.Reservation, actionId, actorId, -1, reservationResult ?? string.Empty, -1, details);
        }

        public void RecordResolveOrder(int tick, Guid actionId, Guid actorId, int resolveOrder, string details)
        {
            AddEntry(tick, ActionJournalEntryType.ResolveOrder, actionId, actorId, -1, string.Empty, resolveOrder, details);
        }

        public void RecordActionResult(int tick, Guid actionId, Guid actorId, string details)
        {
            AddEntry(tick, ActionJournalEntryType.ActionResult, actionId, actorId, -1, string.Empty, -1, details);
        }

        public IReadOnlyList<ActionJournalEntry> GetEntries()
        {
            return _entries;
        }

        public IReadOnlyList<ActionJournalEntry> GetEntriesForTick(int tick)
        {
            var matches = new List<ActionJournalEntry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                ActionJournalEntry entry = _entries[i];
                if (entry.Tick == tick)
                    matches.Add(entry);
            }

            return matches;
        }

        public IReadOnlyList<ActionJournalEntry> GetEntriesByType(ActionJournalEntryType entryType)
        {
            var matches = new List<ActionJournalEntry>();
            for (int i = 0; i < _entries.Count; i++)
            {
                ActionJournalEntry entry = _entries[i];
                if (entry.EntryType == entryType)
                    matches.Add(entry);
            }

            return matches;
        }

        public void Clear()
        {
            _entries.Clear();
            _nextSequence = 0;
        }

        private void AddEntry(
            int tick,
            ActionJournalEntryType entryType,
            Guid actionId,
            Guid actorId,
            int scheduledTick,
            string reservationResult,
            int resolveOrder,
            string details)
        {
            _entries.Add(new ActionJournalEntry(
                tick,
                _nextSequence++,
                entryType,
                actionId,
                actorId,
                scheduledTick,
                reservationResult ?? string.Empty,
                resolveOrder,
                details ?? string.Empty));
        }
    }
}
