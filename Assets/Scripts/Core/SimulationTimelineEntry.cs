using System;

namespace CheckmateRPG.Core
{
    public enum SimulationTimelineEntryType
    {
        Tick,
        Action,
        Mutation,
        Effect,
        Interrupt,
        Event
    }

    [Serializable]
    public sealed class SimulationTimelineEntry
    {
        public int Tick;
        public long Sequence;
        public SimulationTimelineEntryType Type;
        public string Label;
        public string Detail;
        public string Source;
        public string Target;

        public static SimulationTimelineEntry Create(
            int tick,
            long sequence,
            SimulationTimelineEntryType type,
            string label,
            string detail,
            string source = "",
            string target = "")
        {
            return new SimulationTimelineEntry
            {
                Tick = tick,
                Sequence = sequence,
                Type = type,
                Label = label ?? string.Empty,
                Detail = detail ?? string.Empty,
                Source = source ?? string.Empty,
                Target = target ?? string.Empty
            };
        }
    }
}
