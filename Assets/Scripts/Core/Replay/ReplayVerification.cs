using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Replay
{
    public sealed class ReplayVerification
    {
        public ReplayVerificationResult Verify(
            ActionJournal expectedActionJournal,
            MutationJournal expectedMutationJournal,
            ActionJournal actualActionJournal,
            MutationJournal actualMutationJournal)
        {
            if (expectedActionJournal == null)
                throw new ArgumentNullException(nameof(expectedActionJournal));
            if (expectedMutationJournal == null)
                throw new ArgumentNullException(nameof(expectedMutationJournal));
            if (actualActionJournal == null)
                throw new ArgumentNullException(nameof(actualActionJournal));
            if (actualMutationJournal == null)
                throw new ArgumentNullException(nameof(actualMutationJournal));

            var differences = new List<string>();
            CompareActionJournal(expectedActionJournal.GetEntries(), actualActionJournal.GetEntries(), differences);
            CompareMutationJournal(expectedMutationJournal.GetEntries(), actualMutationJournal.GetEntries(), differences);
            return new ReplayVerificationResult(differences);
        }

        private static void CompareActionJournal(
            IReadOnlyList<ActionJournalEntry> expected,
            IReadOnlyList<ActionJournalEntry> actual,
            List<string> differences)
        {
            CompareEntries("ActionJournal", expected, actual, differences);
        }

        private static void CompareMutationJournal(
            IReadOnlyList<MutationJournalEntry> expected,
            IReadOnlyList<MutationJournalEntry> actual,
            List<string> differences)
        {
            CompareEntries("MutationJournal", expected, actual, differences);
        }

        private static void CompareEntries<T>(
            string scope,
            IReadOnlyList<T> expected,
            IReadOnlyList<T> actual,
            List<string> differences)
        {
            int expectedCount = expected?.Count ?? 0;
            int actualCount = actual?.Count ?? 0;
            if (expectedCount != actualCount)
            {
                differences.Add($"{scope} count mismatch: expected={expectedCount}, actual={actualCount}.");
            }

            int sharedCount = Math.Min(expectedCount, actualCount);
            for (int i = 0; i < sharedCount; i++)
            {
                if (Equals(expected[i], actual[i]))
                    continue;

                differences.Add($"{scope}[{i}] mismatch: expected={expected[i]}, actual={actual[i]}.");
            }
        }
    }
}
