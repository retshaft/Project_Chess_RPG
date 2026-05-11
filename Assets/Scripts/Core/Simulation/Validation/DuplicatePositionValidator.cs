using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class DuplicatePositionValidator : ISimulationValidator
    {
        public ValidationResult Validate(IReadOnlySimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            var issues = new List<ValidationIssue>();
            var occupiedUnitIds = new HashSet<Guid>(runtime.OccupiedPositions.Values);
            var seenPositions = new HashSet<Vector2Int>();

            foreach (KeyValuePair<Guid, IReadOnlyUnitRuntimeState> pair in runtime.RuntimeStates)
            {
                Guid unitId = pair.Key;
                IReadOnlyUnitRuntimeState state = pair.Value;
                if (state == null || !occupiedUnitIds.Contains(unitId))
                    continue;

                Vector2Int position = state.Position;
                if (seenPositions.Add(position))
                    continue;

                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    $"Duplicate occupied position detected: {position}.",
                    unitId,
                    runtime.CurrentTick));
            }

            return new ValidationResult(issues.Count == 0, issues);
        }
    }
}
