using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Replay
{
    public enum ReplayMatchResult
    {
        Match = 0,
        Diverged = 1
    }

    public enum DivergenceReason
    {
        None = 0,
        RuntimeSnapshotMismatch = 1,
        MutationSequenceMismatch = 2,
        ReservationStateMismatch = 3,
        OccupancyStateMismatch = 4,
        EffectStateMismatch = 5,
        ActionOrderingMismatch = 6,
        DeterministicRuleViolation = 7,
        RuntimeInvariantViolation = 8,
        Unknown = 100
    }

    public sealed class ReplayVerificationResult
    {
        public ReplayVerificationResult(
            ReplayMatchResult matchResult,
            int divergenceTick,
            DivergenceReason divergenceReason,
            IReadOnlyList<string> differences,
            IReadOnlyList<string> mismatchedRuntimeObjects)
        {
            MatchResult = matchResult;
            DivergenceTick = divergenceTick;
            DivergenceReason = divergenceReason;
            Differences = Clone(differences);
            MismatchedRuntimeObjects = Clone(mismatchedRuntimeObjects);
        }

        public ReplayVerificationResult(IReadOnlyList<string> differences)
        {
            Differences = Clone(differences);
            MatchResult = Differences.Count == 0 ? ReplayMatchResult.Match : ReplayMatchResult.Diverged;
            DivergenceTick = -1;
            DivergenceReason = Differences.Count == 0 ? DivergenceReason.None : DivergenceReason.Unknown;
            MismatchedRuntimeObjects = Array.Empty<string>();
        }

        public ReplayMatchResult MatchResult { get; }
        public int DivergenceTick { get; }
        public DivergenceReason DivergenceReason { get; }
        public IReadOnlyList<string> Differences { get; }
        public IReadOnlyList<string> MismatchedRuntimeObjects { get; }
        public bool IsMatch => MatchResult == ReplayMatchResult.Match;

        public static ReplayVerificationResult Match()
        {
            return new ReplayVerificationResult(
                ReplayMatchResult.Match,
                -1,
                DivergenceReason.None,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static IReadOnlyList<string> Clone(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();

            var cloned = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
                cloned[i] = source[i] ?? string.Empty;

            return cloned;
        }
    }
}
