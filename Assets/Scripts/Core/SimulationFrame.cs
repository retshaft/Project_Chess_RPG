using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    [Serializable]
    public sealed class SimulationFrame
    {
        public int Tick;
        public List<string> Inputs = new();
        public List<string> Actions = new();
        public List<string> Events = new();
        public FrameSnapshot Snapshot;
    }
}
