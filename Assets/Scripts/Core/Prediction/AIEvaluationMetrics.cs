using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Replay;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    public sealed class AIEvaluationMetrics
    {
        private const float DamageWeight = 1.2f;
        private const float TakedownWeight = 90f;
        private const float SelfDamagePenaltyWeight = 1.5f;
        private const float SelfDeathPenalty = 240f;
        private const float SelfSurvivalBonus = 12f;

        public static AIEvaluationMetrics Default { get; } = new();

        public float Evaluate(RuntimeSnapshot currentSnapshot, RuntimeSnapshot predictedSnapshot, Guid actorId)
        {
            if (currentSnapshot == null || predictedSnapshot == null || actorId == Guid.Empty)
                return 0f;

            float outgoingDamage = 0f;
            float selfDamage = 0f;
            int takedowns = 0;

            foreach (KeyValuePair<Guid, SimulationUnitSnapshot> pair in currentSnapshot.UnitStates)
            {
                Guid unitId = pair.Key;
                if (!predictedSnapshot.UnitStates.TryGetValue(unitId, out SimulationUnitSnapshot predictedUnit))
                    continue;

                SimulationUnitSnapshot currentUnit = pair.Value;
                int hpDelta = Mathf.Max(0, Mathf.Max(0, currentUnit?.HP ?? 0) - Mathf.Max(0, predictedUnit?.HP ?? 0));

                if (unitId == actorId)
                {
                    selfDamage += hpDelta;
                }
                else
                {
                    outgoingDamage += hpDelta;
                    if (!IsDead(currentUnit) && IsDead(predictedUnit))
                        takedowns++;
                }
            }

            float score = outgoingDamage * DamageWeight;
            score += takedowns * TakedownWeight;
            score -= selfDamage * SelfDamagePenaltyWeight;

            bool actorWasAlive = !IsDead(currentSnapshot, actorId);
            bool actorIsDead = IsDead(predictedSnapshot, actorId);
            if (actorWasAlive && actorIsDead)
                score -= SelfDeathPenalty;
            else if (!actorIsDead)
                score += SelfSurvivalBonus;

            return score;
        }

        public static RuntimeSnapshot CaptureRuntimeSnapshot(SimulationRuntime runtime)
        {
            if (runtime == null)
                return RuntimeSnapshot.Capture(
                    0,
                    new Dictionary<Guid, SimulationUnitSnapshot>(),
                    new Dictionary<Vector2Int, Guid>(),
                    0f,
                    new Dictionary<string, SimulationEffectSnapshot>(StringComparer.Ordinal),
                    Array.Empty<RuntimeReservationEntry>());

            var unitStates = new Dictionary<Guid, SimulationUnitSnapshot>();
            foreach (KeyValuePair<Guid, IReadOnlyUnitRuntimeState> pair in runtime.RuntimeStates)
                unitStates[pair.Key] = SimulationUnitSnapshot.From(pair.Key, pair.Value);

            var occupancy = new Dictionary<Vector2Int, Guid>();
            foreach (KeyValuePair<Vector2Int, Guid> pair in runtime.OccupiedPositions)
                occupancy[pair.Key] = pair.Value;

            var activeEffects = new Dictionary<string, SimulationEffectSnapshot>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyEffectRuntimeState> pair in runtime.ActiveEffects)
                activeEffects[pair.Key] = SimulationEffectSnapshot.From(pair.Key, pair.Value);

            return RuntimeSnapshot.Capture(
                runtime.CurrentTick,
                unitStates,
                occupancy,
                ap: 0f,
                activeEffects,
                reservations: Array.Empty<RuntimeReservationEntry>());
        }

        public static RuntimeSnapshot ProjectSnapshot(RuntimeSnapshot currentSnapshot, IReadOnlyPredictionResult prediction)
        {
            if (currentSnapshot == null)
            {
                return RuntimeSnapshot.Capture(
                    0,
                    new Dictionary<Guid, SimulationUnitSnapshot>(),
                    new Dictionary<Vector2Int, Guid>(),
                    0f,
                    new Dictionary<string, SimulationEffectSnapshot>(StringComparer.Ordinal),
                    Array.Empty<RuntimeReservationEntry>());
            }

            if (prediction == null)
                return currentSnapshot.Clone();

            var unitStates = new Dictionary<Guid, SimulationUnitSnapshot>();
            foreach (KeyValuePair<Guid, SimulationUnitSnapshot> pair in currentSnapshot.UnitStates)
                unitStates[pair.Key] = pair.Value != null ? new SimulationUnitSnapshot(pair.Value) : null;

            var occupancy = new Dictionary<Vector2Int, Guid>(currentSnapshot.Occupancy);

            for (int i = 0; i < prediction.ExpectedPosition.Count; i++)
            {
                PredictedPosition move = prediction.ExpectedPosition[i];
                if (!unitStates.TryGetValue(move.UnitId, out SimulationUnitSnapshot unit) || unit == null)
                    continue;

                occupancy.Remove(unit.Position);
                occupancy[move.To] = move.UnitId;
                unitStates[move.UnitId] = new SimulationUnitSnapshot(
                    unit.UnitId,
                    unit.HP,
                    unit.SP,
                    move.To,
                    unit.CurrentActionId,
                    unit.RecoveryUntilTick,
                    unit.StatusFlags);
            }

            for (int i = 0; i < prediction.ExpectedDamage.Count; i++)
            {
                PredictedDamage damage = prediction.ExpectedDamage[i];
                if (!unitStates.TryGetValue(damage.TargetId, out SimulationUnitSnapshot unit) || unit == null)
                    continue;

                int nextHp = Mathf.Max(0, unit.HP - Mathf.Max(0, damage.Amount));
                UnitStatusFlags nextFlags = nextHp <= 0 ? unit.StatusFlags | UnitStatusFlags.Dead : unit.StatusFlags;
                unitStates[damage.TargetId] = new SimulationUnitSnapshot(
                    unit.UnitId,
                    nextHp,
                    unit.SP,
                    unit.Position,
                    unit.CurrentActionId,
                    unit.RecoveryUntilTick,
                    nextFlags);
            }

            for (int i = 0; i < prediction.ExpectedDeaths.Count; i++)
            {
                PredictedDeath death = prediction.ExpectedDeaths[i];
                if (!unitStates.TryGetValue(death.UnitId, out SimulationUnitSnapshot unit) || unit == null)
                    continue;

                occupancy.Remove(unit.Position);
                unitStates[death.UnitId] = new SimulationUnitSnapshot(
                    unit.UnitId,
                    0,
                    unit.SP,
                    unit.Position,
                    unit.CurrentActionId,
                    unit.RecoveryUntilTick,
                    unit.StatusFlags | UnitStatusFlags.Dead);
            }

            var activeEffects = new Dictionary<string, SimulationEffectSnapshot>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, SimulationEffectSnapshot> pair in currentSnapshot.ActiveEffects)
                activeEffects[pair.Key] = pair.Value != null ? new SimulationEffectSnapshot(pair.Value) : null;

            RuntimeReservationEntry[] reservations = new RuntimeReservationEntry[currentSnapshot.Reservations.Count];
            for (int i = 0; i < currentSnapshot.Reservations.Count; i++)
                reservations[i] = currentSnapshot.Reservations[i];

            return RuntimeSnapshot.Capture(
                currentSnapshot.Tick,
                unitStates,
                occupancy,
                currentSnapshot.AP,
                activeEffects,
                reservations);
        }

        private static bool IsDead(RuntimeSnapshot snapshot, Guid unitId)
        {
            return snapshot == null ||
                   !snapshot.UnitStates.TryGetValue(unitId, out SimulationUnitSnapshot unit) ||
                   IsDead(unit);
        }

        private static bool IsDead(SimulationUnitSnapshot unit)
        {
            if (unit == null)
                return true;
            if (unit.HP <= 0)
                return true;
            return (unit.StatusFlags & UnitStatusFlags.Dead) != 0;
        }
    }
}
