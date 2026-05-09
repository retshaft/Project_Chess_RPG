using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class DuplicatePositionValidator : ISimulationValidator
    {
        public ValidationResult Validate(SimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            var issues = new List<ValidationIssue>();
            var seenPositions = new HashSet<Vector2Int>();

            foreach (KeyValuePair<Vector2Int, Guid> pair in runtime.OccupiedPositions)
            {
                Vector2Int position = pair.Key;
                if (seenPositions.Add(position))
                    continue;

                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"Duplicate occupied position detected: {position}.",
                    pair.Value,
                    runtime.CurrentTick));
            }

            return new ValidationResult(issues.Count == 0, issues);
        }
    }
}
