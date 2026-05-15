using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Replay
{
    public sealed class DeterministicValidationRule
    {
        public DeterministicValidationResult Validate(
            ActionJournal expectedActionJournal,
            ActionJournal actualActionJournal,
            MutationJournal expectedMutationJournal,
            MutationJournal actualMutationJournal,
            RuntimeSnapshot expectedSnapshot,
            RuntimeSnapshot actualSnapshot)
        {
            if (expectedActionJournal == null)
                throw new ArgumentNullException(nameof(expectedActionJournal));
            if (actualActionJournal == null)
                throw new ArgumentNullException(nameof(actualActionJournal));
            if (expectedMutationJournal == null)
                throw new ArgumentNullException(nameof(expectedMutationJournal));
            if (actualMutationJournal == null)
                throw new ArgumentNullException(nameof(actualMutationJournal));

            var violations = new List<string>();
            int firstViolationTick = int.MaxValue;

            ValidateActionOrdering(expectedActionJournal.GetEntries(), actualActionJournal.GetEntries(), violations, ref firstViolationTick);
            ValidateMutationOrdering(expectedMutationJournal.GetEntries(), actualMutationJournal.GetEntries(), violations, ref firstViolationTick);
            ValidateEffectOrdering(expectedSnapshot, actualSnapshot, violations, ref firstViolationTick);
            ValidateReservationOrdering(expectedSnapshot, actualSnapshot, violations, ref firstViolationTick);

            return new DeterministicValidationResult(violations, firstViolationTick == int.MaxValue ? -1 : firstViolationTick);
        }

        private static void ValidateActionOrdering(
            IReadOnlyList<ActionJournalEntry> expected,
            IReadOnlyList<ActionJournalEntry> actual,
            List<string> violations,
            ref int firstViolationTick)
        {
            if (!IsMonotonicActionSequence(expected))
                AddViolation(violations, ref firstViolationTick, ResolveTick(expected, 0), "DeterministicRule action ordering violation: expected action sequence is not monotonic.");
            if (!IsMonotonicActionSequence(actual))
                AddViolation(violations, ref firstViolationTick, ResolveTick(actual, 0), "DeterministicRule action ordering violation: actual action sequence is not monotonic.");

            int sharedCount = Math.Min(expected.Count, actual.Count);
            for (int i = 0; i < sharedCount; i++)
            {
                ActionJournalEntry e = expected[i];
                ActionJournalEntry a = actual[i];
                if (e.Sequence == a.Sequence &&
                    e.Tick == a.Tick &&
                    e.EntryType == a.EntryType &&
                    e.ActionId == a.ActionId &&
                    e.ActorId == a.ActorId)
                {
                    continue;
                }

                AddViolation(
                    violations,
                    ref firstViolationTick,
                    Math.Min(e.Tick, a.Tick),
                    $"DeterministicRule action ordering mismatch at index {i}: expected={e}, actual={a}.");
            }
        }

        private static void ValidateMutationOrdering(
            IReadOnlyList<MutationJournalEntry> expected,
            IReadOnlyList<MutationJournalEntry> actual,
            List<string> violations,
            ref int firstViolationTick)
        {
            if (!IsMonotonicMutationSequence(expected))
                AddViolation(violations, ref firstViolationTick, ResolveTick(expected, 0), "DeterministicRule mutation ordering violation: expected mutation sequence is not monotonic.");
            if (!IsMonotonicMutationSequence(actual))
                AddViolation(violations, ref firstViolationTick, ResolveTick(actual, 0), "DeterministicRule mutation ordering violation: actual mutation sequence is not monotonic.");

            int sharedCount = Math.Min(expected.Count, actual.Count);
            for (int i = 0; i < sharedCount; i++)
            {
                MutationJournalEntry e = expected[i];
                MutationJournalEntry a = actual[i];
                if (e.Tick == a.Tick &&
                    e.CommitOrder == a.CommitOrder &&
                    e.Sequence == a.Sequence &&
                    e.SequenceInCommit == a.SequenceInCommit &&
                    e.MutationId == a.MutationId &&
                    e.TargetId == a.TargetId &&
                    string.Equals(e.MutationType, a.MutationType, StringComparison.Ordinal))
                {
                    continue;
                }

                AddViolation(
                    violations,
                    ref firstViolationTick,
                    Math.Min(e.Tick, a.Tick),
                    $"DeterministicRule mutation ordering mismatch at index {i}: expected={e}, actual={a}.");
            }
        }

        private static void ValidateEffectOrdering(
            RuntimeSnapshot expectedSnapshot,
            RuntimeSnapshot actualSnapshot,
            List<string> violations,
            ref int firstViolationTick)
        {
            if (expectedSnapshot == null || actualSnapshot == null)
                return;

            string[] expectedKeys = ToOrderedEffectKeys(expectedSnapshot.ActiveEffects);
            string[] actualKeys = ToOrderedEffectKeys(actualSnapshot.ActiveEffects);

            int sharedCount = Math.Min(expectedKeys.Length, actualKeys.Length);
            for (int i = 0; i < sharedCount; i++)
            {
                if (string.Equals(expectedKeys[i], actualKeys[i], StringComparison.Ordinal))
                    continue;

                AddViolation(
                    violations,
                    ref firstViolationTick,
                    Math.Min(expectedSnapshot.Tick, actualSnapshot.Tick),
                    $"DeterministicRule effect ordering mismatch at index {i}: expected={expectedKeys[i]}, actual={actualKeys[i]}.");
            }
        }

        private static void ValidateReservationOrdering(
            RuntimeSnapshot expectedSnapshot,
            RuntimeSnapshot actualSnapshot,
            List<string> violations,
            ref int firstViolationTick)
        {
            if (expectedSnapshot == null || actualSnapshot == null)
                return;

            IReadOnlyList<RuntimeReservationEntry> expectedReservations = expectedSnapshot.Reservations;
            IReadOnlyList<RuntimeReservationEntry> actualReservations = actualSnapshot.Reservations;
            int sharedCount = Math.Min(expectedReservations.Count, actualReservations.Count);

            for (int i = 0; i < sharedCount; i++)
            {
                RuntimeReservationEntry e = expectedReservations[i];
                RuntimeReservationEntry a = actualReservations[i];
                if (e.ActionId == a.ActionId &&
                    e.ActorId == a.ActorId &&
                    e.APCost.Equals(a.APCost) &&
                    e.SPCost == a.SPCost)
                {
                    continue;
                }

                AddViolation(
                    violations,
                    ref firstViolationTick,
                    Math.Min(expectedSnapshot.Tick, actualSnapshot.Tick),
                    $"DeterministicRule reservation ordering mismatch at index {i}: expected={e}, actual={a}.");
            }
        }

        private static bool IsMonotonicActionSequence(IReadOnlyList<ActionJournalEntry> entries)
        {
            int previousSequence = int.MinValue;
            for (int i = 0; i < entries.Count; i++)
            {
                int current = entries[i].Sequence;
                if (current < previousSequence)
                    return false;
                previousSequence = current;
            }

            return true;
        }

        private static bool IsMonotonicMutationSequence(IReadOnlyList<MutationJournalEntry> entries)
        {
            int previousSequence = int.MinValue;
            for (int i = 0; i < entries.Count; i++)
            {
                int current = entries[i].Sequence;
                if (current < previousSequence)
                    return false;
                previousSequence = current;
            }

            return true;
        }

        private static string[] ToOrderedEffectKeys(IReadOnlyDictionary<string, global::CheckmateRPG.Core.Simulation.SimulationEffectSnapshot> effects)
        {
            var ordered = new List<string>(effects.Count);
            foreach (KeyValuePair<string, global::CheckmateRPG.Core.Simulation.SimulationEffectSnapshot> pair in effects)
                ordered.Add(pair.Key ?? string.Empty);

            ordered.Sort(StringComparer.Ordinal);
            return ordered.ToArray();
        }

        private static int ResolveTick(IReadOnlyList<ActionJournalEntry> entries, int fallback)
        {
            return entries.Count == 0 ? fallback : entries[0].Tick;
        }

        private static int ResolveTick(IReadOnlyList<MutationJournalEntry> entries, int fallback)
        {
            return entries.Count == 0 ? fallback : entries[0].Tick;
        }

        private static void AddViolation(List<string> violations, ref int firstViolationTick, int tick, string message)
        {
            violations.Add(message);
            if (tick >= 0 && tick < firstViolationTick)
                firstViolationTick = tick;
        }
    }

    public sealed class DeterministicValidationResult
    {
        public DeterministicValidationResult(IReadOnlyList<string> violations, int firstViolationTick)
        {
            Violations = violations ?? Array.Empty<string>();
            FirstViolationTick = firstViolationTick;
        }

        public IReadOnlyList<string> Violations { get; }
        public int FirstViolationTick { get; }
        public bool IsValid => Violations.Count == 0;
    }
}
