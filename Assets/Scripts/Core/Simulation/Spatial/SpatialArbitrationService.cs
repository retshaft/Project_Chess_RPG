using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation.Spatial
{
    public sealed class SpatialArbitrationService
    {
        private readonly SpatialResolutionPolicy _policy;

        public SpatialArbitrationService(SpatialResolutionPolicy policy)
        {
            _policy = policy;
        }

        public SpatialArbitrationOutcome Resolve(
            IReadOnlyList<(MoveActionCommand Action, ReservedPosition Reservation)> moveReservations,
            IReadOnlySimulationRuntime runtime,
            int currentTick)
        {
            if (moveReservations == null || moveReservations.Count == 0)
                return SpatialArbitrationOutcome.Empty;

            var byAction = new Dictionary<Guid, (MoveActionCommand Action, ReservedPosition Reservation)>();
            var byTarget = new Dictionary<Vector2Int, List<ReservedPosition>>();
            var winners = new HashSet<Guid>();
            var losers = new HashSet<Guid>();
            var conflicts = new List<SpatialConflictDecision>();

            for (int i = 0; i < moveReservations.Count; i++)
            {
                MoveActionCommand move = moveReservations[i].Action;
                ReservedPosition reservation = moveReservations[i].Reservation;
                if (move == null || move.ActionId == Guid.Empty)
                    continue;

                byAction[move.ActionId] = (move, reservation);
                winners.Add(move.ActionId);
                if (!byTarget.TryGetValue(reservation.Position, out List<ReservedPosition> list))
                {
                    list = new List<ReservedPosition>();
                    byTarget[reservation.Position] = list;
                }

                list.Add(reservation);
            }

            ResolveSameTarget(byTarget, winners, losers, conflicts, currentTick);
            ResolveCrossSwap(byAction, winners, losers, conflicts, currentTick);
            ResolveOccupancy(byAction, winners, losers, conflicts, runtime, currentTick);

            var winnerActions = new Dictionary<Guid, ReservedPosition>();
            var winnerPositions = new Dictionary<Vector2Int, ReservedPosition>();
            var orderedLost = new List<Guid>();
            var orderedActionIds = new List<Guid>(byAction.Keys);
            orderedActionIds.Sort();

            for (int i = 0; i < orderedActionIds.Count; i++)
            {
                Guid actionId = orderedActionIds[i];
                ReservedPosition reservation = byAction[actionId].Reservation;
                if (winners.Contains(actionId) && !losers.Contains(actionId))
                {
                    winnerActions[actionId] = reservation;
                    winnerPositions[reservation.Position] = reservation;
                    continue;
                }

                orderedLost.Add(actionId);
            }

            conflicts.Sort(CompareConflictDecisionOrder);
            return new SpatialArbitrationOutcome(winnerActions, winnerPositions, orderedLost, conflicts);
        }

        private void ResolveSameTarget(
            IReadOnlyDictionary<Vector2Int, List<ReservedPosition>> byTarget,
            HashSet<Guid> winners,
            HashSet<Guid> losers,
            List<SpatialConflictDecision> conflicts,
            int tick)
        {
            foreach (KeyValuePair<Vector2Int, List<ReservedPosition>> group in byTarget)
            {
                List<ReservedPosition> contenders = group.Value;
                if (contenders == null || contenders.Count < 2)
                    continue;

                contenders.Sort(CompareReservationOrder);

                switch (_policy)
                {
                    case SpatialResolutionPolicy.Reject:
                    case SpatialResolutionPolicy.MutualCancel:
                        CancelAll(contenders, winners, losers);
                        conflicts.Add(new SpatialConflictDecision(
                            SpatialConflictType.SameTarget,
                            Guid.Empty,
                            ToOrderedActionIds(contenders),
                            tick));
                        break;
                    case SpatialResolutionPolicy.PriorityWin:
                    case SpatialResolutionPolicy.HigherSpeedWins:
                    case SpatialResolutionPolicy.SwapAllowed:
                        ResolvePriorityWinner(contenders, winners, losers, conflicts, tick, SpatialConflictType.SameTarget);
                        break;
                    case SpatialResolutionPolicy.ForceOverride:
                        ResolveForcedOverrideWinner(contenders, winners, losers, conflicts, tick, SpatialConflictType.ForcedOverride);
                        break;
                }
            }
        }

        private void ResolveCrossSwap(
            IReadOnlyDictionary<Guid, (MoveActionCommand Action, ReservedPosition Reservation)> byAction,
            HashSet<Guid> winners,
            HashSet<Guid> losers,
            List<SpatialConflictDecision> conflicts,
            int tick)
        {
            if (winners.Count < 2)
                return;

            var ordered = new List<Guid>(winners);
            ordered.Sort();
            var processed = new HashSet<Guid>();

            for (int i = 0; i < ordered.Count; i++)
            {
                Guid actionId = ordered[i];
                if (processed.Contains(actionId) || !byAction.TryGetValue(actionId, out var left))
                    continue;

                Guid counterpartId = FindSwapCounterpart(actionId, left.Action, winners, byAction);
                if (counterpartId == Guid.Empty || !byAction.TryGetValue(counterpartId, out var right))
                    continue;

                processed.Add(actionId);
                processed.Add(counterpartId);
                var pair = new List<ReservedPosition> { left.Reservation, right.Reservation };
                pair.Sort(CompareReservationOrder);

                switch (_policy)
                {
                    case SpatialResolutionPolicy.SwapAllowed:
                        break;
                    case SpatialResolutionPolicy.Reject:
                    case SpatialResolutionPolicy.MutualCancel:
                        CancelAll(pair, winners, losers);
                        conflicts.Add(new SpatialConflictDecision(
                            SpatialConflictType.CrossSwap,
                            Guid.Empty,
                            ToOrderedActionIds(pair),
                            tick));
                        break;
                    case SpatialResolutionPolicy.PriorityWin:
                    case SpatialResolutionPolicy.HigherSpeedWins:
                        ResolvePriorityWinner(pair, winners, losers, conflicts, tick, SpatialConflictType.CrossSwap);
                        break;
                    case SpatialResolutionPolicy.ForceOverride:
                        ResolveForcedOverrideWinner(pair, winners, losers, conflicts, tick, SpatialConflictType.ForcedOverride);
                        break;
                }
            }
        }

        private void ResolveOccupancy(
            IReadOnlyDictionary<Guid, (MoveActionCommand Action, ReservedPosition Reservation)> byAction,
            HashSet<Guid> winners,
            HashSet<Guid> losers,
            List<SpatialConflictDecision> conflicts,
            IReadOnlySimulationRuntime runtime,
            int tick)
        {
            if (runtime == null || winners.Count == 0)
                return;

            var winnerActionIds = new List<Guid>(winners);
            winnerActionIds.Sort();

            for (int i = 0; i < winnerActionIds.Count; i++)
            {
                Guid actionId = winnerActionIds[i];
                if (!winners.Contains(actionId) || losers.Contains(actionId) || !byAction.TryGetValue(actionId, out var entry))
                    continue;

                MoveActionCommand move = entry.Action;
                ReservedPosition reservation = entry.Reservation;
                IReadOnlyList<IReadOnlyUnitRuntimeState> occupants = runtime.GetUnitsAtPosition(move.To);
                if (occupants == null || occupants.Count == 0)
                    continue;

                Guid occupantId = Guid.Empty;
                UnitStatusFlags occupantFlags = UnitStatusFlags.None;
                for (int j = 0; j < occupants.Count; j++)
                {
                    IReadOnlyUnitRuntimeState occupant = occupants[j];
                    if (occupant == null || occupant.UnitId == move.ActorId)
                        continue;

                    occupantId = occupant.UnitId;
                    occupantFlags = occupant.StatusFlags;
                    break;
                }

                if (occupantId == Guid.Empty)
                    continue;

                bool occupantIsDead = (occupantFlags & UnitStatusFlags.Dead) != 0;
                bool occupantWillVacate = HasWinningMoveOut(occupantId, move.To, winners, byAction);

                if (occupantWillVacate)
                    continue;

                if (occupantIsDead)
                {
                    if (_policy == SpatialResolutionPolicy.Reject || _policy == SpatialResolutionPolicy.MutualCancel)
                    {
                        winners.Remove(actionId);
                        losers.Add(actionId);
                        conflicts.Add(new SpatialConflictDecision(
                            SpatialConflictType.DeadOccupancy,
                            Guid.Empty,
                            new Guid[] { actionId },
                            tick));
                    }

                    continue;
                }

                if (_policy == SpatialResolutionPolicy.ForceOverride && reservation.IsForcedMovement)
                {
                    conflicts.Add(new SpatialConflictDecision(
                        SpatialConflictType.ForcedOverride,
                        actionId,
                        Array.Empty<Guid>(),
                        tick));
                    continue;
                }

                winners.Remove(actionId);
                losers.Add(actionId);
                conflicts.Add(new SpatialConflictDecision(
                    SpatialConflictType.BlockedPath,
                    Guid.Empty,
                    new Guid[] { actionId },
                    tick));
            }
        }

        private static void ResolvePriorityWinner(
            IReadOnlyList<ReservedPosition> contenders,
            HashSet<Guid> winners,
            HashSet<Guid> losers,
            List<SpatialConflictDecision> conflicts,
            int tick,
            SpatialConflictType type)
        {
            if (contenders == null || contenders.Count == 0)
                return;

            Guid winningActionId = contenders[0].ActionId;
            var losingActions = new List<Guid>();
            for (int i = 1; i < contenders.Count; i++)
            {
                Guid loser = contenders[i].ActionId;
                winners.Remove(loser);
                losers.Add(loser);
                losingActions.Add(loser);
            }

            if (losingActions.Count == 0)
                return;

            conflicts.Add(new SpatialConflictDecision(
                type,
                winningActionId,
                losingActions,
                tick));
        }

        private static void ResolveForcedOverrideWinner(
            IReadOnlyList<ReservedPosition> contenders,
            HashSet<Guid> winners,
            HashSet<Guid> losers,
            List<SpatialConflictDecision> conflicts,
            int tick,
            SpatialConflictType type)
        {
            if (contenders == null || contenders.Count == 0)
                return;

            int forcedWinnerIndex = -1;
            for (int i = 0; i < contenders.Count; i++)
            {
                if (!contenders[i].IsForcedMovement)
                    continue;

                forcedWinnerIndex = forcedWinnerIndex < 0
                    ? i
                    : CompareReservationOrder(contenders[i], contenders[forcedWinnerIndex]) < 0
                        ? i
                        : forcedWinnerIndex;
            }

            if (forcedWinnerIndex < 0)
            {
                ResolvePriorityWinner(contenders, winners, losers, conflicts, tick, type);
                return;
            }

            Guid winnerActionId = contenders[forcedWinnerIndex].ActionId;
            var losingActions = new List<Guid>();
            for (int i = 0; i < contenders.Count; i++)
            {
                if (i == forcedWinnerIndex)
                    continue;

                Guid loser = contenders[i].ActionId;
                winners.Remove(loser);
                losers.Add(loser);
                losingActions.Add(loser);
            }

            conflicts.Add(new SpatialConflictDecision(type, winnerActionId, losingActions, tick));
        }

        private static void CancelAll(
            IReadOnlyList<ReservedPosition> contenders,
            HashSet<Guid> winners,
            HashSet<Guid> losers)
        {
            for (int i = 0; i < contenders.Count; i++)
            {
                Guid actionId = contenders[i].ActionId;
                winners.Remove(actionId);
                losers.Add(actionId);
            }
        }

        private static bool HasWinningMoveOut(
            Guid unitId,
            Vector2Int fromCell,
            IReadOnlyCollection<Guid> winners,
            IReadOnlyDictionary<Guid, (MoveActionCommand Action, ReservedPosition Reservation)> byAction)
        {
            foreach (Guid actionId in winners)
            {
                if (!byAction.TryGetValue(actionId, out var entry))
                    continue;
                if (entry.Action.ActorId != unitId)
                    continue;
                if (entry.Action.From != fromCell)
                    continue;
                if (entry.Action.To == fromCell)
                    continue;
                return true;
            }

            return false;
        }

        private static Guid FindSwapCounterpart(
            Guid actionId,
            MoveActionCommand action,
            IReadOnlyCollection<Guid> winners,
            IReadOnlyDictionary<Guid, (MoveActionCommand Action, ReservedPosition Reservation)> byAction)
        {
            foreach (Guid candidateId in winners)
            {
                if (candidateId == actionId || !byAction.TryGetValue(candidateId, out var candidate))
                    continue;
                if (action.From == candidate.Action.To && action.To == candidate.Action.From)
                    return candidateId;
            }

            return Guid.Empty;
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

        private static int CompareConflictDecisionOrder(SpatialConflictDecision x, SpatialConflictDecision y)
        {
            int tickCompare = x.Tick.CompareTo(y.Tick);
            if (tickCompare != 0)
                return tickCompare;

            int typeCompare = x.ConflictType.CompareTo(y.ConflictType);
            if (typeCompare != 0)
                return typeCompare;

            int winCompare = x.WinningAction.CompareTo(y.WinningAction);
            if (winCompare != 0)
                return winCompare;

            IReadOnlyList<Guid> xLosers = x.LosingActions ?? Array.Empty<Guid>();
            IReadOnlyList<Guid> yLosers = y.LosingActions ?? Array.Empty<Guid>();
            int countCompare = xLosers.Count.CompareTo(yLosers.Count);
            if (countCompare != 0)
                return countCompare;

            for (int i = 0; i < xLosers.Count; i++)
            {
                int compare = xLosers[i].CompareTo(yLosers[i]);
                if (compare != 0)
                    return compare;
            }

            return 0;
        }

        private static IReadOnlyList<Guid> ToOrderedActionIds(IReadOnlyList<ReservedPosition> contenders)
        {
            var ids = new List<Guid>(contenders.Count);
            for (int i = 0; i < contenders.Count; i++)
                ids.Add(contenders[i].ActionId);
            ids.Sort();
            return ids;
        }
    }

    public readonly record struct SpatialConflictDecision(
        SpatialConflictType ConflictType,
        Guid WinningAction,
        IReadOnlyList<Guid> LosingActions,
        int Tick);

    public sealed class SpatialArbitrationOutcome
    {
        private static readonly IReadOnlyDictionary<Guid, ReservedPosition> EmptyActionReservations =
            new Dictionary<Guid, ReservedPosition>();
        private static readonly IReadOnlyDictionary<Vector2Int, ReservedPosition> EmptyPositionReservations =
            new Dictionary<Vector2Int, ReservedPosition>();
        private static readonly IReadOnlyList<Guid> EmptyLostActions = Array.Empty<Guid>();
        private static readonly IReadOnlyList<SpatialConflictDecision> EmptyConflictDecisions = Array.Empty<SpatialConflictDecision>();

        public static SpatialArbitrationOutcome Empty { get; } = new(
            EmptyActionReservations,
            EmptyPositionReservations,
            EmptyLostActions,
            EmptyConflictDecisions);

        public SpatialArbitrationOutcome(
            IReadOnlyDictionary<Guid, ReservedPosition> winningReservationsByAction,
            IReadOnlyDictionary<Vector2Int, ReservedPosition> winningReservationsByPosition,
            IReadOnlyList<Guid> reservationLostActions,
            IReadOnlyList<SpatialConflictDecision> conflictDecisions)
        {
            WinningReservationsByAction = winningReservationsByAction ?? EmptyActionReservations;
            WinningReservationsByPosition = winningReservationsByPosition ?? EmptyPositionReservations;
            ReservationLostActions = reservationLostActions ?? EmptyLostActions;
            ConflictDecisions = conflictDecisions ?? EmptyConflictDecisions;
        }

        public IReadOnlyDictionary<Guid, ReservedPosition> WinningReservationsByAction { get; }
        public IReadOnlyDictionary<Vector2Int, ReservedPosition> WinningReservationsByPosition { get; }
        public IReadOnlyList<Guid> ReservationLostActions { get; }
        public IReadOnlyList<SpatialConflictDecision> ConflictDecisions { get; }
    }
}
