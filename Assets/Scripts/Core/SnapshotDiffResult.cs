using System;
using System.Collections.Generic;
using System.Text;

namespace CheckmateRPG.Core
{
    public sealed class SnapshotDiffResult
    {
        public SnapshotDiffResult(
            string scope,
            int? expectedTick,
            int? actualTick,
            IReadOnlyList<string> differences)
        {
            Scope = string.IsNullOrWhiteSpace(scope) ? "Snapshot" : scope;
            ExpectedTick = expectedTick;
            ActualTick = actualTick;
            Differences = differences ?? Array.Empty<string>();
        }

        public string Scope { get; }
        public int? ExpectedTick { get; }
        public int? ActualTick { get; }
        public IReadOnlyList<string> Differences { get; }
        public bool IsMatch => Differences.Count == 0;
        public int DifferenceCount => Differences.Count;

        public string BuildReport()
        {
            var builder = new StringBuilder();
            builder.Append(Scope);
            builder.Append(": ");
            builder.Append(IsMatch ? "MATCH" : "MISMATCH");
            builder.Append(" (expectedTick=");
            builder.Append(ExpectedTick.HasValue ? ExpectedTick.Value.ToString() : "null");
            builder.Append(", actualTick=");
            builder.Append(ActualTick.HasValue ? ActualTick.Value.ToString() : "null");
            builder.Append(", differences=");
            builder.Append(DifferenceCount);
            builder.Append(')');

            for (int i = 0; i < Differences.Count; i++)
            {
                builder.AppendLine();
                builder.Append("- ");
                builder.Append(Differences[i]);
            }

            return builder.ToString();
        }
    }
}
