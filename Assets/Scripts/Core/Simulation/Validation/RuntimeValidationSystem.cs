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

        public ValidationResult Validate(IReadOnlySimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            if (_validators.Count == 0)
                return ValidationResult.Valid();

            ValidationResult aggregated = ValidationResult.Valid();

            for (int i = 0; i < _validators.Count; i++)
            {
                ISimulationValidator validator = _validators[i];
                ValidationResult result = validator.Validate(runtime);
                if (result == null)
                    throw new InvalidOperationException($"Validator '{validator.GetType().Name}' returned a null ValidationResult.");

                aggregated = ValidationResult.Merge(aggregated, result);
            }

            return aggregated;
        }
    }
}
