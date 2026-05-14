using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Replay
{
    public sealed class DivergenceDetector
    {
        public event Action<DivergenceEvent> DivergenceDetected;

        public IReadOnlyList<DivergenceEvent> Detect(ReplayVerificationResult verificationResult)
        {
            if (verificationResult == null)
                throw new ArgumentNullException(nameof(verificationResult));

            if (verificationResult.IsMatch)
                return Array.Empty<DivergenceEvent>();

            var divergences = new List<DivergenceEvent>(verificationResult.Differences.Count);
            for (int i = 0; i < verificationResult.Differences.Count; i++)
            {
                string message = verificationResult.Differences[i] ?? string.Empty;
                DivergenceKind kind = Classify(message);
                var divergence = new DivergenceEvent(kind, -1, message);
                divergences.Add(divergence);
                DivergenceDetected?.Invoke(divergence);
            }

            return divergences;
        }

        private static DivergenceKind Classify(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return DivergenceKind.Unknown;
            if (message.StartsWith("ActionJournal", StringComparison.Ordinal))
                return DivergenceKind.ActionJournal;
            if (message.StartsWith("MutationJournal", StringComparison.Ordinal))
                return DivergenceKind.MutationJournal;
            return DivergenceKind.Unknown;
        }
    }
}
