using System;
using System.Collections.Generic;
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
        private const float SplatDamageRatio = 0.1f;
        private readonly Func<Guid, UnitBrain> _unitLookup;
        private readonly SimulationRuntime _simulationRuntime;

        public MovementMutationProcessor(Func<Guid, UnitBrain> unitLookup, SimulationRuntime simulationRuntime)
        {
            _unitLookup = unitLookup ?? throw new ArgumentNullException(nameof(unitLookup));
            _simulationRuntime = simulationRuntime ?? throw new ArgumentNullException(nameof(simulationRuntime));
        }

        public MovementProcessResult ApplyWithGeneratedMutations(MovementMutation mutation)
        {
            UnitBrain unit = _unitLookup(mutation.TargetId);
            if (unit == null || unit.Movement == null)
                return MovementProcessResult.Empty;

            Vector2Int origin = unit.Movement.GridPosition;
            Vector3 beforeWorld = unit.transform.position;
            bool directMoveResolution = mutation.UseDirectDestinationResolution;
            MovementResolution resolution = ResolveMovementResolution(unit, origin, mutation.To, directMoveResolution);
            bool moved = unit.Movement.ApplyResolvedMovement(resolution.FinalCell);
            if (BattleDiagnostics.ShouldLogMovement(unit))
            {
                Vector2Int afterGrid = unit.Movement.GridPosition;
                Vector3 afterWorld = unit.transform.position;
                Vector2Int worldToGridAfter = GridSystem.Instance != null
                    ? GridSystem.Instance.WorldToGrid(afterWorld)
                    : new Vector2Int(-1, -1);
                Debug.Log(
                    $"[MovementDebug][Mutation] Tick={mutation.Context.Tick}, Unit={mutation.TargetId:N}, " +
                    $"RequestedTo={mutation.To}, ResolvedFinal={resolution.FinalCell}, HitWall={resolution.HitWall}, " +
                    $"CollidedTarget={resolution.CollidedTargetId:N}, GridBefore={origin}, GridAfter={afterGrid}, " +
                    $"WorldBefore={beforeWorld}, WorldAfter={afterWorld}, WorldToGridAfter={worldToGridAfter}, Applied={moved}");
            }

            if (!moved)
                return MovementProcessResult.Empty;

            _simulationRuntime.SetUnitPosition(mutation.TargetId, resolution.FinalCell, OwnershipOwners.MovementMutationProcessor);

            MoveCompletedEvent moveCompletedEvent = new(
                new CheckmateRPG.Core.Events.ActionEvents.MoveCompletedPayload(mutation.TargetId, origin, resolution.FinalCell),
                mutation.TargetId.ToString("N"),
                resolution.FinalCell.ToString());

            IReadOnlyList<IRuntimeMutation> generatedMutations = BuildSplatDamageMutations(unit, origin, mutation, resolution);
            return new MovementProcessResult(new IGameEvent[] { moveCompletedEvent }, generatedMutations);
        }

        public IReadOnlyList<IGameEvent> Apply(MovementMutation mutation)
        {
            return ApplyWithGeneratedMutations(mutation).Events;
        }

        public MovementProcessResult ApplyWithGeneratedMutations(MoveMutation mutation)
        {
            return ApplyWithGeneratedMutations(new MovementMutation(
                mutation.MutationId,
                mutation.TargetId,
                mutation.From,
                mutation.To,
                mutation.Context));
        }

        public IReadOnlyList<IGameEvent> Apply(MoveMutation mutation)
        {
            return ApplyWithGeneratedMutations(mutation).Events;
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

        private static MovementResolution ResolveMovementResolution(
            UnitBrain pushedUnit,
            Vector2Int origin,
            Vector2Int requestedDestination,
            bool directMoveResolution)
        {
            GridSystem grid = GridSystem.Instance;
            if (grid == null)
                return new MovementResolution(requestedDestination, false, Guid.Empty);

            Vector2Int clampedDestination = grid.ClampToValidCell(requestedDestination);
            bool wallSplat = clampedDestination != requestedDestination;
            if (directMoveResolution)
                return new MovementResolution(clampedDestination, wallSplat, Guid.Empty);

            Vector2Int direction = new(
                Mathf.Clamp(requestedDestination.x - origin.x, -1, 1),
                Mathf.Clamp(requestedDestination.y - origin.y, -1, 1));
            int intendedSteps = Mathf.Max(
                Mathf.Abs(requestedDestination.x - origin.x),
                Mathf.Abs(requestedDestination.y - origin.y));

            if (direction == Vector2Int.zero || intendedSteps <= 0)
                return new MovementResolution(origin, wallSplat, Guid.Empty);

            Vector2Int finalCell = origin;
            Guid collidedTargetId = Guid.Empty;
            for (int step = 1; step <= intendedSteps; step++)
            {
                Vector2Int candidate = origin + direction * step;
                if (!grid.IsValidCell(candidate))
                {
                    wallSplat = true;
                    break;
                }

                GameObject occupant = grid.GetOccupant(candidate);
                if (occupant != null && occupant != pushedUnit.gameObject)
                {
                    if (occupant.TryGetComponent(out UnitBrain collidedUnit) &&
                        collidedUnit != null &&
                        !collidedUnit.IsDead &&
                        collidedUnit.ActorId != Guid.Empty)
                    {
                        collidedTargetId = collidedUnit.ActorId;
                    }

                    break;
                }

                finalCell = candidate;
            }

            return new MovementResolution(finalCell, wallSplat, collidedTargetId);
        }

        private IReadOnlyList<IRuntimeMutation> BuildSplatDamageMutations(
            UnitBrain pushedUnit,
            Vector2Int origin,
            MovementMutation sourceMutation,
            MovementResolution resolution)
        {
            if (pushedUnit == null || pushedUnit.Health == null)
                return Array.Empty<IRuntimeMutation>();

            if (!ShouldTriggerSplat(sourceMutation, origin, resolution))
                return Array.Empty<IRuntimeMutation>();

            Guid splatSourceId = ResolveSplatSourceId(sourceMutation, pushedUnit.ActorId);
            var mutations = new List<IRuntimeMutation>(2);
            if (resolution.CollidedTargetId != Guid.Empty)
            {
                TryAddSplatMutation(mutations, pushedUnit, splatSourceId, sourceMutation, "UnitSplat:Pushed");
                UnitBrain collidedUnit = _unitLookup(resolution.CollidedTargetId);
                if (collidedUnit != null)
                    TryAddSplatMutation(mutations, collidedUnit, splatSourceId, sourceMutation, "UnitSplat:Collided");
            }
            else if (resolution.HitWall)
            {
                TryAddSplatMutation(mutations, pushedUnit, splatSourceId, sourceMutation, "WallSplat");
            }

            return mutations.Count == 0 ? Array.Empty<IRuntimeMutation>() : mutations;
        }

        private static bool ShouldTriggerSplat(
            MovementMutation sourceMutation,
            Vector2Int origin,
            MovementResolution resolution)
        {
            bool hasSplatOutcome = resolution.HitWall || resolution.CollidedTargetId != Guid.Empty;
            if (!hasSplatOutcome)
                return false;

            int requestedDisplacementSteps = Mathf.Max(
                Mathf.Abs(sourceMutation.To.x - origin.x),
                Mathf.Abs(sourceMutation.To.y - origin.y));
            if (requestedDisplacementSteps <= 0)
                return false;

            // Standard move actions resolve as MovementMutation and should not trigger splat damage.
            return !string.Equals(
                sourceMutation.Context.MutationReason,
                nameof(MovementMutation),
                StringComparison.Ordinal);
        }

        private static Guid ResolveSplatSourceId(MovementMutation sourceMutation, Guid fallbackSourceId)
        {
            if (sourceMutation.Context.TargetRuntime != Guid.Empty)
                return sourceMutation.Context.TargetRuntime;

            return fallbackSourceId;
        }

        private static void TryAddSplatMutation(
            List<IRuntimeMutation> mutations,
            UnitBrain target,
            Guid sourceId,
            MovementMutation sourceMutation,
            string reasonSuffix)
        {
            if (target == null || target.Health == null || target.IsDead || target.ActorId == Guid.Empty)
                return;

            int amount = Mathf.FloorToInt(Mathf.Max(0f, target.Health.MaxHealth) * SplatDamageRatio);
            if (amount <= 0)
                return;

            MutationContext context = new(
                sourceMutation.Context.Tick,
                sourceMutation.Context.SourceAction,
                target.ActorId,
                $"{nameof(MovementMutationProcessor)}:{reasonSuffix}");

            mutations.Add(new DamageMutation(
                SeededRandomProvider.Shared.NextGuid(),
                target.ActorId,
                sourceId,
                amount,
                IsCritical: false,
                Context: context,
                DamageType: DamageType.True,
                IsTrueDamage: true));
        }

        private readonly record struct MovementResolution(
            Vector2Int FinalCell,
            bool HitWall,
            Guid CollidedTargetId);
    }

    public readonly record struct MovementProcessResult(
        IReadOnlyList<IGameEvent> Events,
        IReadOnlyList<IRuntimeMutation> GeneratedMutations)
    {
        public static MovementProcessResult Empty =>
            new(Array.Empty<IGameEvent>(), Array.Empty<IRuntimeMutation>());
    }
}
