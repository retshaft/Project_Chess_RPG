using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class RuntimeValidationSystem
    {
        private readonly List<ISimulationValidator> _validators = new();
        private readonly List<ValidationIssue> _runtimeIssues = new();

        public void Register(ISimulationValidator validator)
        {
            if (validator == null)
                throw new ArgumentNullException(nameof(validator));

            if (_validators.Contains(validator))
                return;

            _validators.Add(validator);
        }

        public void Unregister(ISimulationValidator validator)
        {
            if (validator == null)
                return;

            _validators.Remove(validator);
        }

        public void ReportIssue(ValidationIssue issue)
        {
            _runtimeIssues.Add(issue);
        }

        public ValidationResult Validate(IReadOnlySimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            // Keep evaluating even when no validators are registered so externally reported runtime issues
            // (for example reaction-depth guard warnings) are still surfaced in the aggregated result.
            ValidationResult aggregated = ValidationResult.Valid();

            for (int i = 0; i < _validators.Count; i++)
            {
                ISimulationValidator validator = _validators[i];
                ValidationResult result = validator.Validate(runtime);
                if (result == null)
                    throw new InvalidOperationException($"Validator '{validator.GetType().Name}' returned a null ValidationResult.");

                aggregated = ValidationResult.Merge(aggregated, result);
            }

            if (_runtimeIssues.Count > 0)
            {
                ValidationIssue[] bufferedIssues = _runtimeIssues.ToArray();
                _runtimeIssues.Clear();
                aggregated = ValidationResult.Merge(aggregated, new ValidationResult(true, bufferedIssues));
            }

            return aggregated;
        }
    }
}
