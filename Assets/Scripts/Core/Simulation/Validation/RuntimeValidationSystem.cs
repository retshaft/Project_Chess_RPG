using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class RuntimeValidationSystem
    {
        private readonly List<ISimulationValidator> _validators = new();

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

        public ValidationResult Validate(SimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            if (_validators.Count == 0)
                return ValidationResult.Valid();

            var issues = new List<ValidationIssue>();
            bool isValid = true;

            for (int i = 0; i < _validators.Count; i++)
            {
                ISimulationValidator validator = _validators[i];
                ValidationResult result = validator.Validate(runtime);
                if (result == null)
                    throw new InvalidOperationException($"Validator '{validator.GetType().Name}' returned a null ValidationResult.");
                if (!result.IsValid)
                    isValid = false;

                IReadOnlyList<ValidationIssue> validatorIssues = result.Issues;
                if (validatorIssues == null || validatorIssues.Count == 0)
                    continue;

                for (int issueIndex = 0; issueIndex < validatorIssues.Count; issueIndex++)
                    issues.Add(validatorIssues[issueIndex]);
            }

            return new ValidationResult(isValid, issues);
        }
    }
}
