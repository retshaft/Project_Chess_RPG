using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Units;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class MovementMutationProcessor
    {
        private readonly Func<Guid, UnitBrain> _unitLookup;

        public MovementMutationProcessor(Func<Guid, UnitBrain> unitLookup)
        {
            _unitLookup = unitLookup ?? throw new ArgumentNullException(nameof(unitLookup));
        }

        public IReadOnlyList<IGameEvent> Apply(MovementMutation mutation)
        {
            UnitBrain unit = _unitLookup(mutation.TargetId);
            if (unit == null || unit.Movement == null)
                return Array.Empty<IGameEvent>();

            bool moved = unit.Movement.ApplyResolvedMovement(mutation.To);
            if (!moved)
                return Array.Empty<IGameEvent>();

            UnitRuntimeState state = unit.RuntimeState;
            if (state != null)
                state.Position = mutation.To;

            MoveCompletedEvent moveCompletedEvent = new(
                new CheckmateRPG.Core.Events.ActionEvents.MoveCompletedPayload(mutation.TargetId, mutation.From, mutation.To),
                mutation.TargetId.ToString("N"),
                mutation.To.ToString());

            return new IGameEvent[] { moveCompletedEvent };
        }
    }
}
