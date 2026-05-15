using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Simulation;

namespace CheckmateRPG.Core.Replay
{
    public sealed class ReplayVerification
    {
        private const int UnsetDivergenceTick = int.MaxValue;

        private readonly SnapshotCompare _snapshotCompare;
        private readonly DeterministicValidationRule _deterministicValidationRule;
        private readonly RuntimeInvariantValidator _runtimeInvariantValidator;

        public ReplayVerification(
            SnapshotCompare snapshotCompare = null,
            DeterministicValidationRule deterministicValidationRule = null,
            RuntimeInvariantValidator runtimeInvariantValidator = null)
        {
            _snapshotCompare = snapshotCompare ?? new SnapshotCompare();
            _deterministicValidationRule = deterministicValidationRule ?? new DeterministicValidationRule();
            _runtimeInvariantValidator = runtimeInvariantValidator ?? new RuntimeInvariantValidator();
        }

        public ReplayVerificationResult Verify(
            ActionJournal expectedActionJournal,
            MutationJournal expectedMutationJournal,
            ActionJournal actualActionJournal,
            MutationJournal actualMutationJournal,
            RuntimeSnapshot expectedSnapshot = null,
            RuntimeSnapshot actualSnapshot = null)
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
            var mismatchedRuntimeObjects = new SortedSet<string>(StringComparer.Ordinal);
            int divergenceTick = UnsetDivergenceTick;
            DivergenceReason divergenceReason = DivergenceReason.None;

            CompareActionJournal(expectedActionJournal.GetEntries(), actualActionJournal.GetEntries(), differences, mismatchedRuntimeObjects, ref divergenceTick, ref divergenceReason);
            CompareMutationJournal(expectedMutationJournal.GetEntries(), actualMutationJournal.GetEntries(), differences, mismatchedRuntimeObjects, ref divergenceTick, ref divergenceReason);
            CompareRuntimeSnapshots(expectedSnapshot, actualSnapshot, differences, mismatchedRuntimeObjects, ref divergenceTick, ref divergenceReason);
            ValidateDeterministicRules(
                expectedActionJournal,
                actualActionJournal,
                expectedMutationJournal,
                actualMutationJournal,
                expectedSnapshot,
                actualSnapshot,
                differences,
                mismatchedRuntimeObjects,
                ref divergenceTick,
                ref divergenceReason);
            ValidateRuntimeInvariants(expectedSnapshot, "Expected", differences, mismatchedRuntimeObjects, ref divergenceTick, ref divergenceReason);
            ValidateRuntimeInvariants(actualSnapshot, "Actual", differences, mismatchedRuntimeObjects, ref divergenceTick, ref divergenceReason);

            if (differences.Count == 0)
                return ReplayVerificationResult.Match();

            int resolvedTick = divergenceTick == UnsetDivergenceTick ? -1 : divergenceTick;
            DivergenceReason resolvedReason = divergenceReason == DivergenceReason.None ? DivergenceReason.Unknown : divergenceReason;
            return new ReplayVerificationResult(
                ReplayMatchResult.Diverged,
                resolvedTick,
                resolvedReason,
                differences,
                ToArray(mismatchedRuntimeObjects));
        }

        private static void CompareActionJournal(
            IReadOnlyList<ActionJournalEntry> expected,
            IReadOnlyList<ActionJournalEntry> actual,
            List<string> differences,
            SortedSet<string> mismatchedRuntimeObjects,
            ref int divergenceTick,
            ref DivergenceReason divergenceReason)
        {
            CompareEntries("ActionJournal", expected, actual, differences, mismatchedRuntimeObjects, ref divergenceTick, ref divergenceReason, DivergenceReason.ActionOrderingMismatch);
        }

        private static void CompareMutationJournal(
            IReadOnlyList<MutationJournalEntry> expected,
            IReadOnlyList<MutationJournalEntry> actual,
            List<string> differences,
            SortedSet<string> mismatchedRuntimeObjects,
            ref int divergenceTick,
            ref DivergenceReason divergenceReason)
        {
            CompareEntries("MutationJournal", expected, actual, differences, mismatchedRuntimeObjects, ref divergenceTick, ref divergenceReason, DivergenceReason.MutationSequenceMismatch);
        }

        private static void CompareEntries<T>(
            string scope,
            IReadOnlyList<T> expected,
            IReadOnlyList<T> actual,
            List<string> differences,
            SortedSet<string> mismatchedRuntimeObjects,
            ref int divergenceTick,
            ref DivergenceReason divergenceReason,
            DivergenceReason reason)
        {
            int expectedCount = expected?.Count ?? 0;
            int actualCount = actual?.Count ?? 0;
            if (expectedCount != actualCount)
            {
                AddDifference(
                    differences,
                    mismatchedRuntimeObjects,
                    ref divergenceTick,
                    ref divergenceReason,
                    reason,
                    -1,
                    scope,
                    $"{scope} count mismatch: expected={expectedCount}, actual={actualCount}.");
            }

            int sharedCount = Math.Min(expectedCount, actualCount);
            for (int i = 0; i < sharedCount; i++)
            {
                if (Equals(expected[i], actual[i]))
                    continue;

                int tick = ResolveTick(expected[i], actual[i]);
                AddDifference(
                    differences,
                    mismatchedRuntimeObjects,
                    ref divergenceTick,
                    ref divergenceReason,
                    reason,
                    tick,
                    $"{scope}[{i}]",
                    $"{scope}[{i}] mismatch: expected={expected[i]}, actual={actual[i]}.");
            }
        }

        private void CompareRuntimeSnapshots(
            RuntimeSnapshot expectedSnapshot,
            RuntimeSnapshot actualSnapshot,
            List<string> differences,
            SortedSet<string> mismatchedRuntimeObjects,
            ref int divergenceTick,
            ref DivergenceReason divergenceReason)
        {
            if (expectedSnapshot == null && actualSnapshot == null)
                return;

            if (expectedSnapshot == null || actualSnapshot == null)
            {
                AddDifference(
                    differences,
                    mismatchedRuntimeObjects,
                    ref divergenceTick,
                    ref divergenceReason,
                    DivergenceReason.RuntimeSnapshotMismatch,
                    ResolveSnapshotTick(expectedSnapshot, actualSnapshot),
                    "RuntimeSnapshot",
                    expectedSnapshot == null
                        ? "RuntimeSnapshot missing from expected side."
                        : "RuntimeSnapshot missing from actual side.");
                return;
            }

            string expectedHash = SnapshotHash.Compute(expectedSnapshot);
            string actualHash = SnapshotHash.Compute(actualSnapshot);
            if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
            {
                AddDifference(
                    differences,
                    mismatchedRuntimeObjects,
                    ref divergenceTick,
                    ref divergenceReason,
                    DivergenceReason.RuntimeSnapshotMismatch,
                    Math.Min(expectedSnapshot.Tick, actualSnapshot.Tick),
                    "RuntimeSnapshot",
                    $"RuntimeSnapshot hash mismatch: expected={expectedHash}, actual={actualHash}.");
            }

            SnapshotCompareResult compareResult = _snapshotCompare.Compare(expectedSnapshot, actualSnapshot);
            for (int i = 0; i < compareResult.Differences.Count; i++)
            {
                string difference = compareResult.Differences[i] ?? string.Empty;
                (DivergenceReason reason, string runtimeObject) = ClassifySnapshotDifference(difference);
                AddDifference(
                    differences,
                    mismatchedRuntimeObjects,
                    ref divergenceTick,
                    ref divergenceReason,
                    reason,
                    Math.Min(expectedSnapshot.Tick, actualSnapshot.Tick),
                    runtimeObject,
                    $"RuntimeSnapshot: {difference}");
            }
        }

        private void ValidateDeterministicRules(
            ActionJournal expectedActionJournal,
            ActionJournal actualActionJournal,
            MutationJournal expectedMutationJournal,
            MutationJournal actualMutationJournal,
            RuntimeSnapshot expectedSnapshot,
            RuntimeSnapshot actualSnapshot,
            List<string> differences,
            SortedSet<string> mismatchedRuntimeObjects,
            ref int divergenceTick,
            ref DivergenceReason divergenceReason)
        {
            DeterministicValidationResult validationResult = _deterministicValidationRule.Validate(
                expectedActionJournal,
                actualActionJournal,
                expectedMutationJournal,
                actualMutationJournal,
                expectedSnapshot,
                actualSnapshot);

            for (int i = 0; i < validationResult.Violations.Count; i++)
            {
                AddDifference(
                    differences,
                    mismatchedRuntimeObjects,
                    ref divergenceTick,
                    ref divergenceReason,
                    DivergenceReason.DeterministicRuleViolation,
                    validationResult.FirstViolationTick,
                    "DeterministicRule",
                    validationResult.Violations[i]);
            }
        }

        private void ValidateRuntimeInvariants(
            RuntimeSnapshot snapshot,
            string side,
            List<string> differences,
            SortedSet<string> mismatchedRuntimeObjects,
            ref int divergenceTick,
            ref DivergenceReason divergenceReason)
        {
            if (snapshot == null)
                return;

            RuntimeInvariantValidationResult validationResult = _runtimeInvariantValidator.Validate(snapshot);
            for (int i = 0; i < validationResult.Issues.Count; i++)
            {
                RuntimeInvariantValidationIssue issue = validationResult.Issues[i];
                AddDifference(
                    differences,
                    mismatchedRuntimeObjects,
                    ref divergenceTick,
                    ref divergenceReason,
                    DivergenceReason.RuntimeInvariantViolation,
                    snapshot.Tick,
                    issue.RuntimeObject,
                    $"{side} runtime invariant violation: {issue.Message}");
            }
        }

        private static void AddDifference(
            List<string> differences,
            SortedSet<string> mismatchedRuntimeObjects,
            ref int divergenceTick,
            ref DivergenceReason divergenceReason,
            DivergenceReason reason,
            int tick,
            string runtimeObject,
            string message)
        {
            differences.Add(message ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(runtimeObject))
                mismatchedRuntimeObjects.Add(runtimeObject);

            if (tick >= 0 && tick < divergenceTick)
                divergenceTick = tick;
            if (divergenceReason == DivergenceReason.None)
                divergenceReason = reason;
        }

        private static int ResolveSnapshotTick(RuntimeSnapshot expectedSnapshot, RuntimeSnapshot actualSnapshot)
        {
            if (expectedSnapshot != null && actualSnapshot != null)
                return Math.Min(expectedSnapshot.Tick, actualSnapshot.Tick);
            if (expectedSnapshot != null)
                return expectedSnapshot.Tick;
            return actualSnapshot?.Tick ?? -1;
        }

        private static (DivergenceReason reason, string runtimeObject) ClassifySnapshotDifference(string difference)
        {
            if (string.IsNullOrWhiteSpace(difference))
                return (DivergenceReason.RuntimeSnapshotMismatch, "RuntimeSnapshot");
            if (difference.StartsWith("Reservations", StringComparison.Ordinal) || difference.StartsWith("Reservation[", StringComparison.Ordinal))
                return (DivergenceReason.ReservationStateMismatch, ExtractObjectName(difference, "Reservation"));
            if (difference.StartsWith("Occupancy[", StringComparison.Ordinal))
                return (DivergenceReason.OccupancyStateMismatch, ExtractObjectName(difference, "Occupancy"));
            if (difference.StartsWith("ActiveEffect[", StringComparison.Ordinal))
                return (DivergenceReason.EffectStateMismatch, ExtractObjectName(difference, "ActiveEffect"));
            return (DivergenceReason.RuntimeSnapshotMismatch, ExtractObjectName(difference, "RuntimeSnapshot"));
        }

        private static string ExtractObjectName(string difference, string fallback)
        {
            int end = difference.IndexOf(' ');
            if (end > 0)
                return difference.Substring(0, end);
            return string.IsNullOrWhiteSpace(difference) ? fallback : difference;
        }

        private static int ResolveTick<T>(T expected, T actual)
        {
            if (expected is ActionJournalEntry expectedAction && actual is ActionJournalEntry actualAction)
                return Math.Min(expectedAction.Tick, actualAction.Tick);
            if (expected is MutationJournalEntry expectedMutation && actual is MutationJournalEntry actualMutation)
                return Math.Min(expectedMutation.Tick, actualMutation.Tick);
            return -1;
        }

        private static IReadOnlyList<string> ToArray(SortedSet<string> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<string>();

            var cloned = new string[values.Count];
            int index = 0;
            foreach (string value in values)
                cloned[index++] = value;

            return cloned;
        }
    }
}
