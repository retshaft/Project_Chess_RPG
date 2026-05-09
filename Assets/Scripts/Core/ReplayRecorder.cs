using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    public sealed class ReplayRecorder
    {
        private readonly SortedDictionary<int, SimulationFrame> _frames = new();

        public SimulationFrame EnsureFrame(int tick)
        {
            if (!_frames.TryGetValue(tick, out SimulationFrame frame))
            {
                frame = new SimulationFrame { Tick = tick };
                _frames[tick] = frame;
            }

            return frame;
        }

        public void RecordInput(int tick, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;
            EnsureFrame(tick).Inputs.Add(input);
        }

        public void RecordAction(int tick, string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return;
            EnsureFrame(tick).Actions.Add(action);
        }

        public void RecordEvent(int tick, string eventTrace)
        {
            if (string.IsNullOrWhiteSpace(eventTrace))
                return;
            EnsureFrame(tick).Events.Add(eventTrace);
        }

        public void RecordSnapshot(FrameSnapshot snapshot)
        {
            if (snapshot == null)
                return;
            EnsureFrame(snapshot.Tick).Snapshot = snapshot;
        }

        public bool TryGetFrame(int tick, out SimulationFrame frame)
        {
            return _frames.TryGetValue(tick, out frame);
        }

        public IReadOnlyList<SimulationFrame> GetFrames()
        {
            return new List<SimulationFrame>(_frames.Values);
        }

        public void Clear()
        {
            _frames.Clear();
        }
    }
}
