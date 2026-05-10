using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class NegativeHpValidator : ISimulationValidator
    {
        public ValidationResult Validate(SimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            var issues = new List<ValidationIssue>();

            foreach (KeyValuePair<Guid, UnitRuntimeState> pair in runtime.RuntimeStates)
            {
                UnitRuntimeState state = pair.Value;
                if (state == null)
                    continue;

                if (state.HP >= 0)
                    continue;

                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"Unit HP is negative: {state.HP}.",
                    state.UnitId == Guid.Empty ? pair.Key : state.UnitId,
                    runtime.CurrentTick));
            }

            return new ValidationResult(issues.Count == 0, issues);
        }
    }
}
