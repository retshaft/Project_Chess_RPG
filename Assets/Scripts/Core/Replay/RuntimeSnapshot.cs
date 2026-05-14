using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.Replay
{
    /// <summary>
    /// Immutable snapshot of the full runtime state at a specific tick.
    /// Captures unit states, grid occupancy, global AP, active effects, and pending reservations.
    /// </summary>
    public sealed class RuntimeSnapshot
    {
        private static readonly IReadOnlyDictionary<Guid, SimulationUnitSnapshot> EmptyUnitStates =
            new ReadOnlyDictionary<Guid, SimulationUnitSnapshot>(new Dictionary<Guid, SimulationUnitSnapshot>());
        private static readonly IReadOnlyDictionary<Vector2Int, Guid> EmptyOccupancy =
            new ReadOnlyDictionary<Vector2Int, Guid>(new Dictionary<Vector2Int, Guid>());
        private static readonly IReadOnlyDictionary<string, SimulationEffectSnapshot> EmptyActiveEffects =
            new ReadOnlyDictionary<string, SimulationEffectSnapshot>(new Dictionary<string, SimulationEffectSnapshot>(StringComparer.Ordinal));
        private static readonly IReadOnlyList<RuntimeReservationEntry> EmptyReservations =
            Array.AsReadOnly(Array.Empty<RuntimeReservationEntry>());

        private RuntimeSnapshot(
            int tick,
            IReadOnlyDictionary<Guid, SimulationUnitSnapshot> unitStates,
            IReadOnlyDictionary<Vector2Int, Guid> occupancy,
            float ap,
            IReadOnlyDictionary<string, SimulationEffectSnapshot> activeEffects,
            IReadOnlyList<RuntimeReservationEntry> reservations)
        {
            Tick = tick;
            UnitStates = unitStates ?? EmptyUnitStates;
            Occupancy = occupancy ?? EmptyOccupancy;
            AP = ap;
            ActiveEffects = activeEffects ?? EmptyActiveEffects;
            Reservations = reservations ?? EmptyReservations;
        }

        public int Tick { get; }
        public IReadOnlyDictionary<Guid, SimulationUnitSnapshot> UnitStates { get; }
        public IReadOnlyDictionary<Vector2Int, Guid> Occupancy { get; }
        public float AP { get; }
        public IReadOnlyDictionary<string, SimulationEffectSnapshot> ActiveEffects { get; }
        public IReadOnlyList<RuntimeReservationEntry> Reservations { get; }

        public static RuntimeSnapshot Capture(
            int tick,
            IReadOnlyDictionary<Guid, SimulationUnitSnapshot> unitStates,
            IReadOnlyDictionary<Vector2Int, Guid> occupancy,
            float ap,
            IReadOnlyDictionary<string, SimulationEffectSnapshot> activeEffects,
            IReadOnlyList<RuntimeReservationEntry> reservations)
        {
            return new RuntimeSnapshot(
                tick,
                CloneUnitStates(unitStates),
                CloneOccupancy(occupancy),
                ap,
                CloneActiveEffects(activeEffects),
                CloneReservations(reservations));
        }

        public RuntimeSnapshot Clone()
        {
            return new RuntimeSnapshot(
                Tick,
                CloneUnitStates(UnitStates),
                CloneOccupancy(Occupancy),
                AP,
                CloneActiveEffects(ActiveEffects),
                CloneReservations(Reservations));
        }

        private static IReadOnlyDictionary<Guid, SimulationUnitSnapshot> CloneUnitStates(
            IReadOnlyDictionary<Guid, SimulationUnitSnapshot> source)
        {
            if (source == null)
                return EmptyUnitStates;

            var cloned = new SortedDictionary<Guid, SimulationUnitSnapshot>();
            foreach (KeyValuePair<Guid, SimulationUnitSnapshot> pair in source)
            {
                if (pair.Value != null)
                    cloned[pair.Key] = new SimulationUnitSnapshot(pair.Value);
            }

            return new ReadOnlyDictionary<Guid, SimulationUnitSnapshot>(cloned);
        }

        private static IReadOnlyDictionary<Vector2Int, Guid> CloneOccupancy(
            IReadOnlyDictionary<Vector2Int, Guid> source)
        {
            if (source == null)
                return EmptyOccupancy;

            var cloned = new SortedDictionary<Vector2Int, Guid>(Vector2IntComparer.Instance);
            foreach (KeyValuePair<Vector2Int, Guid> pair in source)
                cloned[pair.Key] = pair.Value;

            return new ReadOnlyDictionary<Vector2Int, Guid>(cloned);
        }

        private static IReadOnlyDictionary<string, SimulationEffectSnapshot> CloneActiveEffects(
            IReadOnlyDictionary<string, SimulationEffectSnapshot> source)
        {
            if (source == null)
                return EmptyActiveEffects;

            var cloned = new SortedDictionary<string, SimulationEffectSnapshot>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, SimulationEffectSnapshot> pair in source)
            {
                if (pair.Value != null)
                    cloned[pair.Key] = new SimulationEffectSnapshot(pair.Value);
            }

            return new ReadOnlyDictionary<string, SimulationEffectSnapshot>(cloned);
        }

        private static IReadOnlyList<RuntimeReservationEntry> CloneReservations(
            IReadOnlyList<RuntimeReservationEntry> source)
        {
            if (source == null || source.Count == 0)
                return EmptyReservations;

            var cloned = new RuntimeReservationEntry[source.Count];
            for (int i = 0; i < source.Count; i++)
                cloned[i] = source[i];

            return Array.AsReadOnly(cloned);
        }

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
    /// An entry describing a pending action cost reservation at the time of snapshot capture.
    /// </summary>
    public readonly record struct RuntimeReservationEntry(
        Guid ActionId,
        Guid ActorId,
        float APCost,
        int SPCost);
}
