using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Units;

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
    }
}
