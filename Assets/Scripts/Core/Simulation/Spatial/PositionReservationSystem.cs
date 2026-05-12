using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation.Spatial
{
    public sealed class PositionReservationSystem
    {
        private readonly SpatialResolutionPolicy _policy;
        private readonly SpatialArbitrationService _arbitrationService;

        public PositionReservationSystem(SpatialResolutionPolicy policy = SpatialResolutionPolicy.PriorityWin)
        {
            _policy = policy;
            _arbitrationService = new SpatialArbitrationService(_policy);
        }

        public PositionReservationSnapshot Build(
            IReadOnlyList<IActionCommand> actions,
            IReadOnlySimulationRuntime runtime = null,
            int currentTick = 0)
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
                    move.ResolveTick,
                    IsForcedMovement: false)));
            }

            if (moveReservations.Count == 0)
                return PositionReservationSnapshot.Empty;

            SpatialArbitrationOutcome outcome = _arbitrationService.Resolve(moveReservations, runtime, currentTick);
            IReadOnlyList<SpatialConflictResolvedEvent> conflictEvents =
                BuildConflictEvents(outcome.ConflictDecisions);

            return new PositionReservationSnapshot(
                outcome.WinningReservationsByAction,
                outcome.WinningReservationsByPosition,
                outcome.ReservationLostActions,
                conflictEvents);
        }

        private static IReadOnlyList<SpatialConflictResolvedEvent> BuildConflictEvents(
            IReadOnlyList<SpatialConflictDecision> conflictDecisions)
        {
            if (conflictDecisions == null || conflictDecisions.Count == 0)
                return Array.Empty<SpatialConflictResolvedEvent>();

            var events = new List<SpatialConflictResolvedEvent>(conflictDecisions.Count);
            for (int i = 0; i < conflictDecisions.Count; i++)
            {
                SpatialConflictDecision decision = conflictDecisions[i];
                var payload = new SpatialConflictResolvedPayload(
                    decision.ConflictType,
                    decision.WinningAction,
                    decision.LosingActions ?? Array.Empty<Guid>(),
                    decision.Tick);
                events.Add(new SpatialConflictResolvedEvent(
                    payload,
                    source: decision.WinningAction != Guid.Empty ? decision.WinningAction.ToString("N") : string.Empty,
                    target: payload.ConflictType.ToString()));
            }

            return events;
        }
    }

    public sealed class PositionReservationSnapshot
    {
        private static readonly IReadOnlyDictionary<Guid, ReservedPosition> EmptyActionReservations =
            new Dictionary<Guid, ReservedPosition>();
        private static readonly IReadOnlyDictionary<Vector2Int, ReservedPosition> EmptyPositionReservations =
            new Dictionary<Vector2Int, ReservedPosition>();
        private static readonly IReadOnlyCollection<Guid> EmptyLostActions = Array.Empty<Guid>();
        private static readonly IReadOnlyList<SpatialConflictResolvedEvent> EmptyConflictEvents =
            Array.Empty<SpatialConflictResolvedEvent>();

        public static PositionReservationSnapshot Empty { get; } =
            new(EmptyActionReservations, EmptyPositionReservations, EmptyLostActions, EmptyConflictEvents);

        public PositionReservationSnapshot(
            IReadOnlyDictionary<Guid, ReservedPosition> winningReservationsByAction,
            IReadOnlyDictionary<Vector2Int, ReservedPosition> winningReservationsByPosition,
            IReadOnlyCollection<Guid> reservationLostActions,
            IReadOnlyList<SpatialConflictResolvedEvent> conflictEvents)
        {
            WinningReservationsByAction = winningReservationsByAction ?? EmptyActionReservations;
            WinningReservationsByPosition = winningReservationsByPosition ?? EmptyPositionReservations;
            ReservationLostActions = reservationLostActions ?? EmptyLostActions;
            ConflictEvents = conflictEvents ?? EmptyConflictEvents;
            _reservationLostLookup = BuildReservationLostLookup(ReservationLostActions);
        }

        public IReadOnlyDictionary<Guid, ReservedPosition> WinningReservationsByAction { get; }
        public IReadOnlyDictionary<Vector2Int, ReservedPosition> WinningReservationsByPosition { get; }
        public IReadOnlyCollection<Guid> ReservationLostActions { get; }
        public IReadOnlyList<SpatialConflictResolvedEvent> ConflictEvents { get; }
        private readonly HashSet<Guid> _reservationLostLookup;

        public bool HasWinningReservation(Guid actionId)
        {
            return actionId != Guid.Empty && WinningReservationsByAction.ContainsKey(actionId);
        }

        public bool IsReservationLost(Guid actionId)
        {
            return actionId != Guid.Empty && _reservationLostLookup.Contains(actionId);
        }

        private static HashSet<Guid> BuildReservationLostLookup(IReadOnlyCollection<Guid> actionIds)
        {
            var lookup = new HashSet<Guid>();
            if (actionIds == null || actionIds.Count == 0)
                return lookup;

            foreach (Guid actionId in actionIds)
            {
                if (actionId != Guid.Empty)
                    lookup.Add(actionId);
            }

            return lookup;
        }
    }
}
