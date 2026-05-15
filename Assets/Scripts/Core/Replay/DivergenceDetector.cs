using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Replay
{
    public sealed class DivergenceDetector
    {
        private readonly ReplayVerification _replayVerification;

        public DivergenceDetector(ReplayVerification replayVerification = null)
        {
            _replayVerification = replayVerification ?? new ReplayVerification();
        }

        public event Action<DivergenceEvent> DivergenceDetected;

        public IReadOnlyList<DivergenceEvent> Detect(
            ActionJournal expectedActionJournal,
            MutationJournal expectedMutationJournal,
            ActionJournal actualActionJournal,
            MutationJournal actualMutationJournal,
            RuntimeSnapshot expectedSnapshot = null,
            RuntimeSnapshot actualSnapshot = null)
        {
            ReplayVerificationResult result = _replayVerification.Verify(
                expectedActionJournal,
                expectedMutationJournal,
                actualActionJournal,
                actualMutationJournal,
                expectedSnapshot,
                actualSnapshot);

            return Detect(result);
        }

        public IReadOnlyList<DivergenceEvent> Detect(ReplayVerificationResult verificationResult)
        {
            if (verificationResult == null)
                throw new ArgumentNullException(nameof(verificationResult));

            if (verificationResult.IsMatch)
                return Array.Empty<DivergenceEvent>();

            int eventCount = verificationResult.Differences.Count == 0 ? 1 : verificationResult.Differences.Count;
            var divergences = new List<DivergenceEvent>(eventCount);

            if (verificationResult.Differences.Count == 0)
            {
                var divergence = new DivergenceEvent(
                    Classify(verificationResult.DivergenceReason),
                    verificationResult.DivergenceTick,
                    verificationResult.DivergenceReason,
                    "Replay verification mismatch detected.");
                divergences.Add(divergence);
                DivergenceDetected?.Invoke(divergence);
                return divergences;
            }

            for (int i = 0; i < verificationResult.Differences.Count; i++)
            {
                string message = verificationResult.Differences[i] ?? string.Empty;
                DivergenceReason reason = ClassifyReason(message, verificationResult.DivergenceReason);
                DivergenceKind kind = Classify(reason);
                int tick = ResolveTick(verificationResult.DivergenceTick, message);
                var divergence = new DivergenceEvent(kind, tick, reason, message);
                divergences.Add(divergence);
                DivergenceDetected?.Invoke(divergence);
            }

            return divergences;
        }

        private static DivergenceReason ClassifyReason(string message, DivergenceReason fallback)
        {
            if (string.IsNullOrWhiteSpace(message))
                return fallback;
            if (message.StartsWith("ActionJournal", StringComparison.Ordinal))
                return DivergenceReason.ActionOrderingMismatch;
            if (message.StartsWith("MutationJournal", StringComparison.Ordinal))
                return DivergenceReason.MutationSequenceMismatch;
            if (message.IndexOf("Reservation", StringComparison.Ordinal) >= 0)
                return DivergenceReason.ReservationStateMismatch;
            if (message.IndexOf("Occupancy", StringComparison.Ordinal) >= 0)
                return DivergenceReason.OccupancyStateMismatch;
            if (message.IndexOf("ActiveEffect", StringComparison.Ordinal) >= 0)
                return DivergenceReason.EffectStateMismatch;
            if (message.StartsWith("DeterministicRule", StringComparison.Ordinal))
                return DivergenceReason.DeterministicRuleViolation;
            if (message.IndexOf("invariant violation", StringComparison.OrdinalIgnoreCase) >= 0)
                return DivergenceReason.RuntimeInvariantViolation;
            if (message.StartsWith("RuntimeSnapshot", StringComparison.Ordinal))
                return DivergenceReason.RuntimeSnapshotMismatch;
            return fallback;
        }

        private static DivergenceKind Classify(DivergenceReason reason)
        {
            return reason switch
            {
                DivergenceReason.ActionOrderingMismatch => DivergenceKind.ActionOrdering,
                DivergenceReason.MutationSequenceMismatch => DivergenceKind.MutationSequence,
                DivergenceReason.ReservationStateMismatch => DivergenceKind.ReservationState,
                DivergenceReason.OccupancyStateMismatch => DivergenceKind.OccupancyState,
                DivergenceReason.EffectStateMismatch => DivergenceKind.EffectState,
                DivergenceReason.DeterministicRuleViolation => DivergenceKind.DeterministicRule,
                DivergenceReason.RuntimeInvariantViolation => DivergenceKind.RuntimeInvariant,
                DivergenceReason.RuntimeSnapshotMismatch => DivergenceKind.RuntimeSnapshot,
                _ => DivergenceKind.Unknown
            };
        }

        private static int ResolveTick(int fallbackTick, string message)
        {
            int extracted = ExtractTick(message);
            return extracted >= 0 ? extracted : fallbackTick;
        }

        private static int ExtractTick(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return -1;

            const string tickPrefix = "Tick ";
            int tickIndex = message.IndexOf(tickPrefix, StringComparison.Ordinal);
            if (tickIndex >= 0)
                return ParseFollowingInt(message, tickIndex + tickPrefix.Length);

            const string tickEquals = "tick=";
            tickIndex = message.IndexOf(tickEquals, StringComparison.OrdinalIgnoreCase);
            if (tickIndex >= 0)
                return ParseFollowingInt(message, tickIndex + tickEquals.Length);

            return -1;
        }

        private static int ParseFollowingInt(string value, int startIndex)
        {
            if (startIndex < 0 || startIndex >= value.Length)
                return -1;

            int index = startIndex;
            while (index < value.Length && !char.IsDigit(value[index]) && value[index] != '-')
                index++;

            int end = index;
            if (end < value.Length && value[end] == '-')
                end++;
            while (end < value.Length && char.IsDigit(value[end]))
                end++;

            if (end <= index)
                return -1;

            if (int.TryParse(value.Substring(index, end - index), out int tick))
                return tick;

            return -1;
        }
    }
}
