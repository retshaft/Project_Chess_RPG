using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class MovementMutationProcessor
    {
        private const float SwampPenaltyMultiplier = 2f;
        private readonly Func<Guid, UnitBrain> _unitLookup;
        private readonly SimulationRuntime _simulationRuntime;

        public MovementMutationProcessor(Func<Guid, UnitBrain> unitLookup, SimulationRuntime simulationRuntime)
        {
            _unitLookup = unitLookup ?? throw new ArgumentNullException(nameof(unitLookup));
            _simulationRuntime = simulationRuntime ?? throw new ArgumentNullException(nameof(simulationRuntime));
        }

        public IReadOnlyList<IGameEvent> Apply(MovementMutation mutation)
        {
            UnitBrain unit = _unitLookup(mutation.TargetId);
            if (unit == null || unit.Movement == null)
                return Array.Empty<IGameEvent>();

            bool moved = unit.Movement.ApplyResolvedMovement(mutation.To);
            if (!moved)
                return Array.Empty<IGameEvent>();

            _simulationRuntime.SetUnitPosition(mutation.TargetId, mutation.To, OwnershipOwners.MovementMutationProcessor);

            MoveCompletedEvent moveCompletedEvent = new(
                new CheckmateRPG.Core.Events.ActionEvents.MoveCompletedPayload(mutation.TargetId, mutation.From, mutation.To),
                mutation.TargetId.ToString("N"),
                mutation.To.ToString());

            return new IGameEvent[] { moveCompletedEvent };
        }

        public IReadOnlyList<IGameEvent> Apply(MoveMutation mutation)
        {
            return Apply(new MovementMutation(
                mutation.MutationId,
                mutation.TargetId,
                mutation.From,
                mutation.To,
                mutation.Context));
        }

        public static float ResolveSwampApMultiplier(Vector2Int from, Vector2Int to, bool isJumpSkill = false)
        {
            GridSystem grid = GridSystem.Instance;
            if (grid == null || !grid.IsValidCell(to))
                return 1f;

            if (grid.GetTileType(to) == TileType.Swamp)
                return SwampPenaltyMultiplier;

            if (isJumpSkill)
                return 1f;

            int deltaX = to.x - from.x;
            int deltaY = to.y - from.y;
            int stepCount = Mathf.Max(Mathf.Abs(deltaX), Mathf.Abs(deltaY));
            if (stepCount <= 1)
                return 1f;

            for (int step = 1; step < stepCount; step++)
            {
                int x = from.x + Mathf.RoundToInt(deltaX * (step / (float)stepCount));
                int y = from.y + Mathf.RoundToInt(deltaY * (step / (float)stepCount));
                var sample = new Vector2Int(x, y);
                if (grid.IsValidCell(sample) && grid.GetTileType(sample) == TileType.Swamp)
                    return SwampPenaltyMultiplier;
            }

            return 1f;
        }
    }
}
