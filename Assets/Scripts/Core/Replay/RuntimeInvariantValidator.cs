using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.Replay
{
    public sealed class RuntimeInvariantValidator
    {
        public RuntimeInvariantValidationResult Validate(RuntimeSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var issues = new List<RuntimeInvariantValidationIssue>();
            ValidateNegativeAP(snapshot, issues);
            ValidateDuplicateOccupancy(snapshot, issues);
            ValidateInvalidReservation(snapshot, issues);
            ValidateDeadUnitAction(snapshot, issues);
            ValidateOrphanEffect(snapshot, issues);
            return new RuntimeInvariantValidationResult(issues);
        }

        private static void ValidateNegativeAP(RuntimeSnapshot snapshot, List<RuntimeInvariantValidationIssue> issues)
        {
            if (snapshot.AP >= 0f)
                return;

            issues.Add(new RuntimeInvariantValidationIssue(
                "AP",
                $"Negative AP detected: {snapshot.AP}."));
        }

        private static void ValidateDuplicateOccupancy(RuntimeSnapshot snapshot, List<RuntimeInvariantValidationIssue> issues)
        {
            var seenPositions = new Dictionary<Vector2Int, Guid>(Vector2IntComparer.Instance);
            var orderedUnitIds = new SortedSet<Guid>(snapshot.UnitStates.Keys);
            foreach (Guid unitId in orderedUnitIds)
            {
                if (!snapshot.UnitStates.TryGetValue(unitId, out SimulationUnitSnapshot state) || state == null)
                    continue;

                if (seenPositions.TryGetValue(state.Position, out Guid existingUnitId))
                {
                    issues.Add(new RuntimeInvariantValidationIssue(
                        $"Unit[{unitId:N}]",
                        $"Duplicate occupancy detected at ({state.Position.x},{state.Position.y}) for units {existingUnitId:N} and {unitId:N}."));
                    continue;
                }

                seenPositions[state.Position] = unitId;
            }

            var orderedOccupancy = new SortedSet<Vector2Int>(snapshot.Occupancy.Keys, Vector2IntComparer.Instance);
            foreach (Vector2Int pos in orderedOccupancy)
            {
                if (!snapshot.Occupancy.TryGetValue(pos, out Guid occupancyUnitId))
                    continue;
                if (!snapshot.UnitStates.TryGetValue(occupancyUnitId, out SimulationUnitSnapshot state) || state == null)
                {
                    issues.Add(new RuntimeInvariantValidationIssue(
                        $"Occupancy[({pos.x},{pos.y})]",
                        $"Occupancy points to missing unit: {occupancyUnitId:N}."));
                    continue;
                }

                if (state.Position != pos)
                {
                    issues.Add(new RuntimeInvariantValidationIssue(
                        $"Occupancy[({pos.x},{pos.y})]",
                        $"Occupancy/unit position mismatch: unit {occupancyUnitId:N} is at ({state.Position.x},{state.Position.y})."));
                }
            }
        }

        private static void ValidateInvalidReservation(RuntimeSnapshot snapshot, List<RuntimeInvariantValidationIssue> issues)
        {
            for (int i = 0; i < snapshot.Reservations.Count; i++)
            {
                RuntimeReservationEntry reservation = snapshot.Reservations[i];
                if (reservation.ActionId == Guid.Empty)
                {
                    issues.Add(new RuntimeInvariantValidationIssue(
                        $"Reservation[{i}]",
                        "Invalid reservation: ActionId is empty."));
                }

                if (!snapshot.UnitStates.ContainsKey(reservation.ActorId))
                {
                    issues.Add(new RuntimeInvariantValidationIssue(
                        $"Reservation[{i}]",
                        $"Invalid reservation: unknown actor {reservation.ActorId:N}."));
                }

                if (reservation.APCost < 0f || reservation.SPCost < 0)
                {
                    issues.Add(new RuntimeInvariantValidationIssue(
                        $"Reservation[{i}]",
                        $"Invalid reservation cost: ap={reservation.APCost}, sp={reservation.SPCost}."));
                }
            }
        }

        private static void ValidateDeadUnitAction(RuntimeSnapshot snapshot, List<RuntimeInvariantValidationIssue> issues)
        {
            var orderedUnitIds = new SortedSet<Guid>(snapshot.UnitStates.Keys);
            foreach (Guid unitId in orderedUnitIds)
            {
                if (!snapshot.UnitStates.TryGetValue(unitId, out SimulationUnitSnapshot state) || state == null)
                    continue;

                if (state.HP <= 0 && state.CurrentActionId.HasValue)
                {
                    issues.Add(new RuntimeInvariantValidationIssue(
                        $"Unit[{unitId:N}]",
                        $"Dead unit action detected: CurrentActionId={state.CurrentActionId.Value:N}."));
                }
            }
        }

        private static void ValidateOrphanEffect(RuntimeSnapshot snapshot, List<RuntimeInvariantValidationIssue> issues)
        {
            var orderedEffectKeys = new SortedSet<string>(snapshot.ActiveEffects.Keys, StringComparer.Ordinal);
            foreach (string effectKey in orderedEffectKeys)
            {
                if (!snapshot.ActiveEffects.TryGetValue(effectKey, out SimulationEffectSnapshot effect) || effect == null)
                    continue;

                if (!snapshot.UnitStates.ContainsKey(effect.TargetId))
                {
                    issues.Add(new RuntimeInvariantValidationIssue(
                        $"ActiveEffect[{effectKey}]",
                        $"Orphan effect target detected: {effect.TargetId:N}."));
                }

                if (effect.SourceId != Guid.Empty && !snapshot.UnitStates.ContainsKey(effect.SourceId))
                {
                    issues.Add(new RuntimeInvariantValidationIssue(
                        $"ActiveEffect[{effectKey}]",
                        $"Orphan effect source detected: {effect.SourceId:N}."));
                }
            }
        }

        private sealed class Vector2IntComparer : IEqualityComparer<Vector2Int>, IComparer<Vector2Int>
        {
            public static readonly Vector2IntComparer Instance = new();

            public bool Equals(Vector2Int x, Vector2Int y)
            {
                return x.x == y.x && x.y == y.y;
            }

            public int GetHashCode(Vector2Int obj)
            {
                return HashCode.Combine(obj.x, obj.y);
            }

            public int Compare(Vector2Int x, Vector2Int y)
            {
                int cx = x.x.CompareTo(y.x);
                return cx != 0 ? cx : x.y.CompareTo(y.y);
            }
        }
    }

    public readonly record struct RuntimeInvariantValidationIssue(string RuntimeObject, string Message);

    public sealed class RuntimeInvariantValidationResult
    {
        public RuntimeInvariantValidationResult(IReadOnlyList<RuntimeInvariantValidationIssue> issues)
        {
            Issues = issues ?? Array.Empty<RuntimeInvariantValidationIssue>();
        }

        public IReadOnlyList<RuntimeInvariantValidationIssue> Issues { get; }
        public bool IsValid => Issues.Count == 0;
    }
}
