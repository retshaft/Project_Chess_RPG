using System;
using System.Collections.Generic;
using System.Text;

namespace CheckmateRPG.Core
{
    public static class TimelineExportUtility
    {
        public static string ExportAsText(IReadOnlyList<SimulationTimelineEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return string.Empty;

            var builder = new StringBuilder(entries.Count * 96);
            for (int i = 0; i < entries.Count; i++)
            {
                SimulationTimelineEntry entry = entries[i];
                if (entry == null)
                    continue;

                builder.Append("Tick=").Append(entry.Tick)
                    .Append(" | Type=").Append(entry.Type)
                    .Append(" | Label=").Append(entry.Label ?? string.Empty)
                    .Append(" | Detail=").Append(entry.Detail ?? string.Empty);

                if (!string.IsNullOrWhiteSpace(entry.Source))
                    builder.Append(" | Source=").Append(entry.Source);
                if (!string.IsNullOrWhiteSpace(entry.Target))
                    builder.Append(" | Target=").Append(entry.Target);

                builder.AppendLine();
            }

            return builder.ToString();
        }

        public static string ExportFromReplay(ReplayRecorder replayRecorder)
        {
            if (replayRecorder == null)
                throw new ArgumentNullException(nameof(replayRecorder));

            IReadOnlyList<SimulationFrame> frames = replayRecorder.GetFrames();
            var entries = new List<SimulationTimelineEntry>();
            for (int i = 0; i < frames.Count; i++)
            {
                SimulationFrame frame = frames[i];
                if (frame == null || frame.TimelineEntries == null || frame.TimelineEntries.Count == 0)
                    continue;

                for (int j = 0; j < frame.TimelineEntries.Count; j++)
                {
                    SimulationTimelineEntry entry = frame.TimelineEntries[j];
                    if (entry != null)
                        entries.Add(entry);
                }
            }

            entries.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return 1;
                if (right == null)
                    return -1;

                int tickCompare = left.Tick.CompareTo(right.Tick);
                if (tickCompare != 0)
                    return tickCompare;
                return left.Sequence.CompareTo(right.Sequence);
            });

            return ExportAsText(entries);
        }
    }
}
