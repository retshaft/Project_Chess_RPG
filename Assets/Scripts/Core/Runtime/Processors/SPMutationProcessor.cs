using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class SPMutationProcessor
    {
        private readonly SimulationRuntime _simulationRuntime;

        public SPMutationProcessor(SimulationRuntime simulationRuntime)
        {
            _simulationRuntime = simulationRuntime ?? throw new ArgumentNullException(nameof(simulationRuntime));
        }

        public IReadOnlyList<IGameEvent> Apply(SPMutation mutation)
        {
            if (!_simulationRuntime.TryApplyUnitSPDelta(
                    mutation.TargetUnitId,
                    mutation.Amount,
                    OwnershipOwners.SPMutationProcessor,
                    out int previousSp,
                    out int currentSp,
                    out int maxSp))
            {
                return Array.Empty<IGameEvent>();
            }

            SPChangedEvent changedEvent = new(
                new SPChangedPayload(
                    mutation.TargetUnitId,
                    previousSp,
                    currentSp,
                    maxSp,
                    currentSp - previousSp),
                mutation.Context.SourceAction != Guid.Empty ? mutation.Context.SourceAction.ToString("N") : mutation.MutationId.ToString("N"),
                mutation.TargetUnitId.ToString("N"));

            return new IGameEvent[] { changedEvent };
        }
    }
}
