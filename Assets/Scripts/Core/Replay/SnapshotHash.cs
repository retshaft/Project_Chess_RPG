using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.Replay
{
    public static class SnapshotHash
    {
        public static string Compute(RuntimeSnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;

            string canonicalState = BuildCanonicalState(snapshot);
            byte[] inputBytes = Encoding.UTF8.GetBytes(canonicalState);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(inputBytes);
            return ToLowerHex(hash);
        }

        private static string BuildCanonicalState(RuntimeSnapshot snapshot)
        {
            var builder = new StringBuilder(2048);
            builder.Append("Tick=").Append(snapshot.Tick).Append('|');
            builder.Append("AP=").Append(snapshot.AP.ToString("R", CultureInfo.InvariantCulture)).Append('|');

            AppendUnitStates(builder, snapshot.UnitStates);
            AppendOccupancy(builder, snapshot.Occupancy);
            AppendEffects(builder, snapshot.ActiveEffects);
            AppendReservations(builder, snapshot.Reservations);
            return builder.ToString();
        }

        private static void AppendUnitStates(StringBuilder builder, IReadOnlyDictionary<Guid, SimulationUnitSnapshot> unitStates)
        {
            var orderedUnitIds = new SortedSet<Guid>(unitStates.Keys);
            builder.Append("Units[");
            foreach (Guid unitId in orderedUnitIds)
            {
                if (!unitStates.TryGetValue(unitId, out SimulationUnitSnapshot unit) || unit == null)
                    continue;

                builder.Append(unit.UnitId.ToString("N")).Append(':');
                builder.Append(unit.HP).Append(',');
                builder.Append(unit.SP).Append(',');
                builder.Append(unit.Position.x).Append(',').Append(unit.Position.y).Append(',');
                builder.Append(unit.CurrentActionId.HasValue ? unit.CurrentActionId.Value.ToString("N") : "null").Append(',');
                builder.Append(unit.RecoveryUntilTick).Append(',');
                builder.Append((int)unit.StatusFlags).Append(';');
            }

            builder.Append("]|");
        }

        private static void AppendOccupancy(StringBuilder builder, IReadOnlyDictionary<Vector2Int, Guid> occupancy)
        {
            var orderedPositions = new SortedSet<Vector2Int>(occupancy.Keys, DeterministicVector2IntComparer.Instance);
            builder.Append("Occupancy[");
            foreach (Vector2Int pos in orderedPositions)
            {
                if (!occupancy.TryGetValue(pos, out Guid unitId))
                    continue;

                builder.Append(pos.x).Append(',').Append(pos.y).Append(':');
                builder.Append(unitId.ToString("N")).Append(';');
            }

            builder.Append("]|");
        }

        private static void AppendEffects(StringBuilder builder, IReadOnlyDictionary<string, SimulationEffectSnapshot> effects)
        {
            var orderedKeys = new SortedSet<string>(effects.Keys, StringComparer.Ordinal);
            builder.Append("Effects[");
            foreach (string key in orderedKeys)
            {
                if (!effects.TryGetValue(key, out SimulationEffectSnapshot effect) || effect == null)
                    continue;

                builder.Append(key).Append(':');
                builder.Append(effect.EffectId).Append(',');
                builder.Append(effect.SourceId.ToString("N")).Append(',');
                builder.Append(effect.TargetId.ToString("N")).Append(',');
                builder.Append(effect.RemainingTick).Append(',');
                builder.Append(effect.StackCount).Append(',');
                builder.Append(effect.TickInterval).Append(',');
                builder.Append(effect.NextTickIn).Append(',');
                builder.Append(effect.Magnitude.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                builder.Append((int)effect.TimingPhase).Append(',');
                builder.Append((int)effect.ActionSpeedLevel).Append(',');
                builder.Append(effect.IsReaction ? '1' : '0').Append(';');
            }

            builder.Append("]|");
        }

        private static void AppendReservations(StringBuilder builder, IReadOnlyList<RuntimeReservationEntry> reservations)
        {
            builder.Append("Reservations[");
            int count = reservations?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                RuntimeReservationEntry reservation = reservations[i];
                builder.Append(i).Append(':');
                builder.Append(reservation.ActionId.ToString("N")).Append(',');
                builder.Append(reservation.ActorId.ToString("N")).Append(',');
                builder.Append(reservation.APCost.ToString("R", CultureInfo.InvariantCulture)).Append(',');
                builder.Append(reservation.SPCost).Append(';');
            }

            builder.Append(']');
        }

        private static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));

            return builder.ToString();
        }
    }
}
