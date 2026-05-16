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
                return GridSystem.SwampMoveCostMultiplier;

            if (isJumpSkill)
                return 1f;

            if (PathContainsSwamp(grid, from, to))
                return GridSystem.SwampMoveCostMultiplier;

            return 1f;
        }

        private static bool PathContainsSwamp(GridSystem grid, Vector2Int from, Vector2Int to)
        {
            int x = from.x;
            int y = from.y;
            int xEnd = to.x;
            int yEnd = to.y;
            int deltaX = Mathf.Abs(xEnd - x);
            int stepX = x < xEnd ? 1 : -1;
            int deltaY = -Mathf.Abs(yEnd - y);
            int stepY = y < yEnd ? 1 : -1;
            int error = deltaX + deltaY;

            bool isStart = true;
            while (true)
            {
                var sample = new Vector2Int(x, y);
                if (!isStart && sample != to && grid.IsValidCell(sample) && grid.GetTileType(sample) == TileType.Swamp)
                    return true;

                if (x == xEnd && y == yEnd)
                    break;

                int doubledError = 2 * error;
                if (doubledError >= deltaY)
                {
                    error += deltaY;
                    x += stepX;
                }

                if (doubledError <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }

                isStart = false;
            }

            return false;
        }
    }
}
