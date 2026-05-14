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
                int tick = ExtractTick(message);
                var divergence = new DivergenceEvent(kind, tick, message);
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
