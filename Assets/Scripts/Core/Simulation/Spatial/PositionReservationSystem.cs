using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation.Spatial
{
    public sealed class PositionReservationSystem
    {
        private readonly SpatialResolutionPolicy _policy;

        public PositionReservationSystem(SpatialResolutionPolicy policy = SpatialResolutionPolicy.HigherSpeedWins)
        {
            _policy = policy;
        }

        public PositionReservationSnapshot Build(IReadOnlyList<IActionCommand> actions)
        {
            if (actions == null || actions.Count == 0)
                return PositionReservationSnapshot.Empty;

            var moveReservations = new List<(MoveActionCommand Action, ReservedPosition Reservation)>();
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] is not MoveActionCommand move)
                    continue;

                moveReservations.Add((move, new ReservedPosition(
                    move.To,
                    move.ActorId,
                    move.ActionId,
                    move.SpeedTier,
                    move.ResolveTick)));
            }

            if (moveReservations.Count == 0)
                return PositionReservationSnapshot.Empty;

            moveReservations.Sort((left, right) => CompareReservationOrder(left.Reservation, right.Reservation));

            var winnersByPosition = new Dictionary<Vector2Int, ReservedPosition>();
            var winnerActions = new Dictionary<Guid, ReservedPosition>();
            var reservationLostActions = new HashSet<Guid>();
            var winnerMoves = new Dictionary<Guid, MoveActionCommand>();

            for (int i = 0; i < moveReservations.Count; i++)
            {
                MoveActionCommand move = moveReservations[i].Action;
                ReservedPosition reservation = moveReservations[i].Reservation;
                if (move.ActionId == Guid.Empty)
                    continue;

                if (winnersByPosition.ContainsKey(reservation.Position))
                {
                    reservationLostActions.Add(move.ActionId);
                    continue;
                }

                winnersByPosition[reservation.Position] = reservation;
                winnerActions[move.ActionId] = reservation;
                winnerMoves[move.ActionId] = move;
            }

            if (_policy == SpatialResolutionPolicy.HigherSpeedWins)
                ApplySwapBan(winnerActions, winnersByPosition, reservationLostActions, winnerMoves);

            return new PositionReservationSnapshot(winnerActions, winnersByPosition, reservationLostActions);
        }

        private static int CompareReservationOrder(ReservedPosition x, ReservedPosition y)
        {
            int speedCompare = ((int)x.ActionSpeedLevel).CompareTo((int)y.ActionSpeedLevel);
            if (speedCompare != 0)
                return speedCompare;

            int tickCompare = x.ReservationTick.CompareTo(y.ReservationTick);
            if (tickCompare != 0)
                return tickCompare;

            return x.ActionId.CompareTo(y.ActionId);
        }

        private static void ApplySwapBan(
            Dictionary<Guid, ReservedPosition> winnerActions,
            Dictionary<Vector2Int, ReservedPosition> winnersByPosition,
            HashSet<Guid> reservationLostActions,
            Dictionary<Guid, MoveActionCommand> winnerMoves)
        {
            if (winnerMoves.Count < 2)
                return;

            var examined = new HashSet<Guid>();
            var orderedIds = new List<Guid>(winnerMoves.Keys);
            orderedIds.Sort();

            for (int i = 0; i < orderedIds.Count; i++)
            {
                Guid actionId = orderedIds[i];
                if (!winnerMoves.TryGetValue(actionId, out MoveActionCommand move))
                    continue;
                if (examined.Contains(actionId))
                    continue;

                Guid otherActionId = FindSwapCounterpartActionId(move, winnerMoves);
                if (otherActionId == Guid.Empty || !winnerMoves.TryGetValue(otherActionId, out MoveActionCommand otherMove))
                    continue;

                examined.Add(actionId);
                examined.Add(otherActionId);

                ReservedPosition left = winnerActions[actionId];
                ReservedPosition right = winnerActions[otherActionId];
                bool leftWins = CompareReservationOrder(left, right) <= 0;
                Guid loserId = leftWins ? otherActionId : actionId;
                MoveActionCommand loserMove = leftWins ? otherMove : move;

                reservationLostActions.Add(loserId);
                winnerActions.Remove(loserId);
                winnersByPosition.Remove(loserMove.To);
                winnerMoves.Remove(loserId);
            }
        }

        private static Guid FindSwapCounterpartActionId(
            MoveActionCommand move,
            IReadOnlyDictionary<Guid, MoveActionCommand> candidates)
        {
            foreach (KeyValuePair<Guid, MoveActionCommand> pair in candidates)
            {
                MoveActionCommand candidate = pair.Value;
                if (candidate == null || candidate.ActionId == move.ActionId)
                    continue;

                if (move.From == candidate.To && move.To == candidate.From)
                    return candidate.ActionId;
            }

            return Guid.Empty;
        }
    }

    public sealed class PositionReservationSnapshot
    {
        private static readonly IReadOnlyDictionary<Guid, ReservedPosition> EmptyActionReservations =
            new Dictionary<Guid, ReservedPosition>();
        private static readonly IReadOnlyDictionary<Vector2Int, ReservedPosition> EmptyPositionReservations =
            new Dictionary<Vector2Int, ReservedPosition>();
        private static readonly IReadOnlyCollection<Guid> EmptyLostActions = Array.Empty<Guid>();

        public static PositionReservationSnapshot Empty { get; } =
            new(EmptyActionReservations, EmptyPositionReservations, EmptyLostActions);

        public PositionReservationSnapshot(
            IReadOnlyDictionary<Guid, ReservedPosition> winningReservationsByAction,
            IReadOnlyDictionary<Vector2Int, ReservedPosition> winningReservationsByPosition,
            IReadOnlyCollection<Guid> reservationLostActions)
        {
            WinningReservationsByAction = winningReservationsByAction ?? EmptyActionReservations;
            WinningReservationsByPosition = winningReservationsByPosition ?? EmptyPositionReservations;
            ReservationLostActions = reservationLostActions ?? EmptyLostActions;
        }

        public IReadOnlyDictionary<Guid, ReservedPosition> WinningReservationsByAction { get; }
        public IReadOnlyDictionary<Vector2Int, ReservedPosition> WinningReservationsByPosition { get; }
        public IReadOnlyCollection<Guid> ReservationLostActions { get; }

        public bool HasWinningReservation(Guid actionId)
        {
            return actionId != Guid.Empty && WinningReservationsByAction.ContainsKey(actionId);
        }

        public bool IsReservationLost(Guid actionId)
        {
            if (actionId == Guid.Empty)
                return false;

            foreach (Guid lostActionId in ReservationLostActions)
            {
                if (lostActionId == actionId)
                    return true;
            }

            return false;
        }
    }
}
