using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Runtime;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class DeadUnitActionValidator : ISimulationValidator
    {
        public ValidationResult Validate(SimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            var issues = new List<ValidationIssue>();
            var actorsWithActiveActions = new HashSet<Guid>();

            foreach (IActionCommand action in runtime.ActiveActions.Values)
            {
                if (action == null)
                    continue;

                actorsWithActiveActions.Add(action.ActorId);
            }

            foreach (KeyValuePair<Guid, UnitRuntimeState> pair in runtime.RuntimeStates)
            {
                UnitRuntimeState state = pair.Value;
                if (state == null)
                    continue;

                Guid unitId = state.UnitId == Guid.Empty ? pair.Key : state.UnitId;
                bool hasActiveAction = state.CurrentActionId.HasValue || actorsWithActiveActions.Contains(unitId);
                if (state.HP > 0 || !hasActiveAction)
                    continue;

                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "Dead unit must not hold an active action.",
                    unitId,
                    runtime.CurrentTick));
            }

            return new ValidationResult(issues.Count == 0, issues);
        }
    }
}
