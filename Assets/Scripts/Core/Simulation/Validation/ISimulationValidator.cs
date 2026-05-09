namespace CheckmateRPG.Core.Simulation.Validation
{
    public interface ISimulationValidator
    {
        ValidationResult Validate(SimulationRuntime runtime);
    }
}
