using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    public sealed class ReplayValidationService
    {
        public ReplayValidationService(float floatTolerance = SnapshotDiffUtility.DefaultFloatTolerance)
        {
            if (floatTolerance < 0f)
                throw new ArgumentOutOfRangeException(nameof(floatTolerance));

            FloatTolerance = floatTolerance;
        }

        public float FloatTolerance { get; }

        public SnapshotDiffResult Validate(ReplayRecorder expectedReplay, ReplayRecorder actualReplay)
        {
            if (expectedReplay == null)
                throw new ArgumentNullException(nameof(expectedReplay));
            if (actualReplay == null)
                throw new ArgumentNullException(nameof(actualReplay));

            return Validate(expectedReplay.GetFrames(), actualReplay.GetFrames());
        }

        public SnapshotDiffResult Validate(
            IReadOnlyList<SimulationFrame> expectedFrames,
            IReadOnlyList<SimulationFrame> actualFrames)
        {
            if (expectedFrames == null)
                throw new ArgumentNullException(nameof(expectedFrames));
            if (actualFrames == null)
                throw new ArgumentNullException(nameof(actualFrames));

            var differences = new List<string>();
            var expectedByTick = IndexFrames(expectedFrames);
            var actualByTick = IndexFrames(actualFrames);
            var ticks = new SortedSet<int>(expectedByTick.Keys);
            ticks.UnionWith(actualByTick.Keys);

            foreach (int tick in ticks)
            {
                bool hasExpected = expectedByTick.TryGetValue(tick, out SimulationFrame expectedFrame);
                bool hasActual = actualByTick.TryGetValue(tick, out SimulationFrame actualFrame);

                if (!hasExpected || !hasActual)
                {
                    differences.Add(!hasExpected
                        ? $"Replay frame[{tick}] missing from expected replay."
                        : $"Replay frame[{tick}] missing from actual replay.");
                    continue;
                }

                if (expectedFrame.Tick != actualFrame.Tick)
                {
                    differences.Add(
                        $"Replay frame tick mismatch at key={tick}: expected={expectedFrame.Tick}, actual={actualFrame.Tick}.");
                }

                SnapshotDiffResult snapshotDiff = SnapshotDiffUtility.Compare(
                    expectedFrame.Snapshot,
                    actualFrame.Snapshot,
                    FloatTolerance);

                for (int i = 0; i < snapshotDiff.Differences.Count; i++)
                    differences.Add($"Tick {tick}: {snapshotDiff.Differences[i]}");
            }

            return new SnapshotDiffResult("Replay", null, null, differences);
        }

        private static SortedDictionary<int, SimulationFrame> IndexFrames(IReadOnlyList<SimulationFrame> frames)
        {
            var indexed = new SortedDictionary<int, SimulationFrame>();
            for (int i = 0; i < frames.Count; i++)
            {
                SimulationFrame frame = frames[i];
                if (frame == null)
                    continue;

                indexed[frame.Tick] = frame;
            }

            return indexed;
        }
    }
}
