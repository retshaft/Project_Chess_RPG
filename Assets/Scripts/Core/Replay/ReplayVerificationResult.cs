using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Replay
{
    public sealed class ReplayVerificationResult
    {
        public ReplayVerificationResult(IReadOnlyList<string> differences)
        {
            Differences = differences ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Differences { get; }
        public bool IsMatch => Differences.Count == 0;
    }
}
