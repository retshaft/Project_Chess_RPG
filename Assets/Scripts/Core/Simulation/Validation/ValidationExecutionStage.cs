using System;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class ValidationExecutionStage
    {
        private readonly RuntimeValidationSystem _validationSystem;

        public ValidationExecutionStage(RuntimeValidationSystem validationSystem)
        {
            _validationSystem = validationSystem ?? throw new ArgumentNullException(nameof(validationSystem));
        }

        public ValidationResult Execute(IReadOnlySimulationRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));

            return _validationSystem.Validate(runtime);
        }
    }
}
