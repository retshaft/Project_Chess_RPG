using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class InvalidRecoveryValidator : ISimulationValidator
    {
        public ValidationResult Validate(IReadOnlySimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            var issues = new List<ValidationIssue>();

            foreach (KeyValuePair<Guid, IReadOnlyUnitRuntimeState> pair in runtime.RuntimeStates)
            {
                IReadOnlyUnitRuntimeState state = pair.Value;
                if (state == null)
                    continue;

                bool isRecovering = runtime.CurrentTick < state.RecoveryUntilTick;
                if (isRecovering && !state.CurrentActionId.HasValue)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "Unit is in recovery state but has no CurrentActionId.",
                        state.UnitId == Guid.Empty ? pair.Key : state.UnitId,
                        runtime.CurrentTick));
                }
            }

            return new ValidationResult(issues.Count == 0, issues);
        }
    }
}
