using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.Replay
{
    /// <summary>
    /// Compares two <see cref="RuntimeSnapshot"/> instances field-by-field and reports divergences.
    /// Intended to validate that a replayed simulation produces the same state as the original.
    /// </summary>
    public sealed class SnapshotCompare
    {
        private const float DefaultFloatTolerance = 0.0001f;

        private readonly float _floatTolerance;

        public SnapshotCompare(float floatTolerance = DefaultFloatTolerance)
        {
            if (floatTolerance < 0f)
                throw new ArgumentOutOfRangeException(nameof(floatTolerance));

            _floatTolerance = floatTolerance;
        }

        /// <summary>
        /// Compares <paramref name="expected"/> (original) against <paramref name="actual"/> (replay).
        /// </summary>
        public SnapshotCompareResult Compare(RuntimeSnapshot expected, RuntimeSnapshot actual)
        {
            var differences = new List<string>();

            if (expected == null && actual == null)
                return new SnapshotCompareResult(differences);

            if (expected == null)
            {
                differences.Add("Expected snapshot is null.");
                return new SnapshotCompareResult(differences);
            }

            if (actual == null)
            {
                differences.Add("Actual snapshot is null.");
                return new SnapshotCompareResult(differences);
            }

            if (expected.Tick != actual.Tick)
                differences.Add($"Tick mismatch: expected={expected.Tick}, actual={actual.Tick}.");

            CompareUnitStates(expected.UnitStates, actual.UnitStates, differences);
            CompareOccupancy(expected.Occupancy, actual.Occupancy, differences);
            CompareAP(expected.AP, actual.AP, differences);
            CompareActiveEffects(expected.ActiveEffects, actual.ActiveEffects, differences);
            CompareReservations(expected.Reservations, actual.Reservations, differences);

            return new SnapshotCompareResult(differences);
        }

        private static void CompareUnitStates(
            IReadOnlyDictionary<Guid, SimulationUnitSnapshot> expected,
            IReadOnlyDictionary<Guid, SimulationUnitSnapshot> actual,
            List<string> differences)
        {
            var unitIds = new SortedSet<Guid>(expected.Keys);
            unitIds.UnionWith(actual.Keys);

            foreach (Guid unitId in unitIds)
            {
                bool hasExpected = expected.TryGetValue(unitId, out SimulationUnitSnapshot expectedUnit);
                bool hasActual = actual.TryGetValue(unitId, out SimulationUnitSnapshot actualUnit);

                if (!hasExpected || !hasActual)
                {
                    differences.Add(!hasExpected
                        ? $"Unit[{unitId:N}] missing from expected snapshot."
                        : $"Unit[{unitId:N}] missing from actual snapshot.");
                    continue;
                }

                string key = unitId.ToString("N");
                CompareField($"Unit[{key}].HP", expectedUnit.HP, actualUnit.HP, differences);
                CompareField($"Unit[{key}].SP", expectedUnit.SP, actualUnit.SP, differences);

                if (expectedUnit.Position != actualUnit.Position)
                {
                    differences.Add(
                        $"Unit[{key}].Position mismatch: " +
                        $"expected=({expectedUnit.Position.x},{expectedUnit.Position.y}), " +
                        $"actual=({actualUnit.Position.x},{actualUnit.Position.y}).");
                }

                CompareField($"Unit[{key}].CurrentActionId", expectedUnit.CurrentActionId, actualUnit.CurrentActionId, differences);
                CompareField($"Unit[{key}].RecoveryUntilTick", expectedUnit.RecoveryUntilTick, actualUnit.RecoveryUntilTick, differences);
                CompareField($"Unit[{key}].StatusFlags", expectedUnit.StatusFlags, actualUnit.StatusFlags, differences);
            }
        }

        private static void CompareOccupancy(
            IReadOnlyDictionary<Vector2Int, Guid> expected,
            IReadOnlyDictionary<Vector2Int, Guid> actual,
            List<string> differences)
        {
            var positions = new SortedSet<Vector2Int>(expected.Keys, Vector2IntComparer.Instance);
            positions.UnionWith(actual.Keys);

            foreach (Vector2Int pos in positions)
            {
                bool hasExpected = expected.TryGetValue(pos, out Guid expectedUnit);
                bool hasActual = actual.TryGetValue(pos, out Guid actualUnit);

                if (!hasExpected || !hasActual)
                {
                    differences.Add(!hasExpected
                        ? $"Occupancy[({pos.x},{pos.y})] missing from expected snapshot."
                        : $"Occupancy[({pos.x},{pos.y})] missing from actual snapshot.");
                    continue;
                }

                if (expectedUnit != actualUnit)
                {
                    differences.Add(
                        $"Occupancy[({pos.x},{pos.y})] mismatch: " +
                        $"expected={expectedUnit:N}, actual={actualUnit:N}.");
                }
            }
        }

        private void CompareAP(float expected, float actual, List<string> differences)
        {
            if (Math.Abs(expected - actual) > _floatTolerance)
            {
                differences.Add(
                    $"AP mismatch: expected={expected}, actual={actual}, tolerance={_floatTolerance}.");
            }
        }

        private void CompareActiveEffects(
            IReadOnlyDictionary<string, SimulationEffectSnapshot> expected,
            IReadOnlyDictionary<string, SimulationEffectSnapshot> actual,
            List<string> differences)
        {
            var keys = new SortedSet<string>(expected.Keys, StringComparer.Ordinal);
            keys.UnionWith(actual.Keys);

            foreach (string key in keys)
            {
                bool hasExpected = expected.TryGetValue(key, out SimulationEffectSnapshot expectedEffect);
                bool hasActual = actual.TryGetValue(key, out SimulationEffectSnapshot actualEffect);

                if (!hasExpected || !hasActual)
                {
                    differences.Add(!hasExpected
                        ? $"ActiveEffect[{key}] missing from expected snapshot."
                        : $"ActiveEffect[{key}] missing from actual snapshot.");
                    continue;
                }

                CompareField($"ActiveEffect[{key}].RemainingTick", expectedEffect.RemainingTick, actualEffect.RemainingTick, differences);
                CompareField($"ActiveEffect[{key}].StackCount", expectedEffect.StackCount, actualEffect.StackCount, differences);
                CompareField($"ActiveEffect[{key}].TickInterval", expectedEffect.TickInterval, actualEffect.TickInterval, differences);
                CompareField($"ActiveEffect[{key}].NextTickIn", expectedEffect.NextTickIn, actualEffect.NextTickIn, differences);
                CompareField($"ActiveEffect[{key}].TimingPhase", expectedEffect.TimingPhase, actualEffect.TimingPhase, differences);
                CompareField($"ActiveEffect[{key}].ActionSpeedLevel", expectedEffect.ActionSpeedLevel, actualEffect.ActionSpeedLevel, differences);
                CompareField($"ActiveEffect[{key}].IsReaction", expectedEffect.IsReaction, actualEffect.IsReaction, differences);

                if (Math.Abs(expectedEffect.Magnitude - actualEffect.Magnitude) > _floatTolerance)
                {
                    differences.Add(
                        $"ActiveEffect[{key}].Magnitude mismatch: " +
                        $"expected={expectedEffect.Magnitude}, actual={actualEffect.Magnitude}, tolerance={_floatTolerance}.");
                }
            }
        }

        private void CompareReservations(
            IReadOnlyList<RuntimeReservationEntry> expected,
            IReadOnlyList<RuntimeReservationEntry> actual,
            List<string> differences)
        {
            int expectedCount = expected?.Count ?? 0;
            int actualCount = actual?.Count ?? 0;

            if (expectedCount != actualCount)
            {
                differences.Add($"Reservations count mismatch: expected={expectedCount}, actual={actualCount}.");
            }

            int sharedCount = Math.Min(expectedCount, actualCount);
            for (int i = 0; i < sharedCount; i++)
            {
                RuntimeReservationEntry e = expected![i];
                RuntimeReservationEntry a = actual![i];

                if (e.ActionId != a.ActionId)
                    differences.Add($"Reservation[{i}].ActionId mismatch: expected={e.ActionId:N}, actual={a.ActionId:N}.");

                if (e.ActorId != a.ActorId)
                    differences.Add($"Reservation[{i}].ActorId mismatch: expected={e.ActorId:N}, actual={a.ActorId:N}.");

                if (Math.Abs(e.APCost - a.APCost) > _floatTolerance)
                    differences.Add($"Reservation[{i}].APCost mismatch: expected={e.APCost}, actual={a.APCost}.");

                if (e.SPCost != a.SPCost)
                    differences.Add($"Reservation[{i}].SPCost mismatch: expected={e.SPCost}, actual={a.SPCost}.");
            }
        }

        private static void CompareField<T>(
            string fieldName,
            T expected,
            T actual,
            List<string> differences)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
                return;

            differences.Add(
                $"{fieldName} mismatch: expected={FormatValue(expected)}, actual={FormatValue(actual)}.");
        }

        private static string FormatValue<T>(T value) => value == null ? "null" : value.ToString();

        private sealed class Vector2IntComparer : IComparer<Vector2Int>
        {
            public static readonly Vector2IntComparer Instance = new();

            public int Compare(Vector2Int x, Vector2Int y)
            {
                int cx = x.x.CompareTo(y.x);
                return cx != 0 ? cx : x.y.CompareTo(y.y);
            }
        }
    }

    /// <summary>
    /// Result of a <see cref="SnapshotCompare.Compare"/> operation.
    /// </summary>
    public sealed class SnapshotCompareResult
    {
        public SnapshotCompareResult(IReadOnlyList<string> differences)
        {
            Differences = differences ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Differences { get; }
        public bool IsMatch => Differences.Count == 0;
    }
}
