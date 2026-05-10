using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Simulation
{
    public sealed class SnapshotRecorder
    {
        private readonly SortedDictionary<int, SimulationSnapshot> _snapshots = new();

        public SnapshotRecorder(SnapshotPolicy policy)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public SnapshotPolicy Policy { get; private set; }
        public int Count => _snapshots.Count;

        public bool ShouldCapture(int tick)
        {
            return tick >= 0 && tick % Policy.SnapshotInterval == 0;
        }

        public bool TryRecord(SimulationSnapshot snapshot)
        {
            if (snapshot == null || !ShouldCapture(snapshot.Tick))
                return false;

            _snapshots[snapshot.Tick] = snapshot.Clone();
            TrimToPolicy();
            return true;
        }

        public void UpdatePolicy(SnapshotPolicy policy)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            TrimToPolicy();
        }

        public bool TryGetSnapshot(int tick, out SimulationSnapshot snapshot)
        {
            if (_snapshots.TryGetValue(tick, out SimulationSnapshot stored))
            {
                snapshot = stored.Clone();
                return true;
            }

            snapshot = null;
            return false;
        }

        public IReadOnlyList<SimulationSnapshot> GetSnapshots()
        {
            var snapshots = new List<SimulationSnapshot>(_snapshots.Count);
            foreach (KeyValuePair<int, SimulationSnapshot> pair in _snapshots)
                snapshots.Add(pair.Value.Clone());
            return snapshots.AsReadOnly();
        }

        public void Clear()
        {
            _snapshots.Clear();
        }

        private void TrimToPolicy()
        {
            while (_snapshots.Count > Policy.MaxSnapshotCount)
            {
                using IEnumerator<KeyValuePair<int, SimulationSnapshot>> enumerator = _snapshots.GetEnumerator();
                if (!enumerator.MoveNext())
                    return;

                _snapshots.Remove(enumerator.Current.Key);
            }
        }
    }
}
