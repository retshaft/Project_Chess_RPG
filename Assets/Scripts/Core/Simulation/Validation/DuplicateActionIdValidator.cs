using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class DuplicateActionIdValidator : ISimulationValidator
    {
        public ValidationResult Validate(IReadOnlySimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            var issues = new List<ValidationIssue>();
            var seenActionIds = new HashSet<Guid>();

            foreach (KeyValuePair<Guid, IReadOnlyActionState> pair in runtime.ActiveActions)
            {
                Guid actionId = pair.Value?.ActionId ?? pair.Key;
                if (pair.Value != null && pair.Key != pair.Value.ActionId)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        $"ActiveActions key/action mismatch: key={pair.Key:N}, value={pair.Value.ActionId:N}.",
                        pair.Value.ActorId,
                        runtime.CurrentTick));
                }

                if (seenActionIds.Add(actionId))
                    continue;

                Guid? unitId = pair.Value?.ActorId;
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"Duplicate ActionId detected: {actionId:N}.",
                    unitId,
                    runtime.CurrentTick));
            }

            return new ValidationResult(issues.Count == 0, issues);
        }
    }
}
