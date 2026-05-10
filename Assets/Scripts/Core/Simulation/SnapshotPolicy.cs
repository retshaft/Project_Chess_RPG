using System;

namespace CheckmateRPG.Core.Simulation
{
    [Serializable]
    public sealed class SnapshotPolicy
    {
        public SnapshotPolicy(int snapshotInterval, int maxSnapshotCount)
        {
            SnapshotInterval = Math.Max(1, snapshotInterval);
            MaxSnapshotCount = Math.Max(1, maxSnapshotCount);
        }

        public int SnapshotInterval { get; }
        public int MaxSnapshotCount { get; }
    }
}
