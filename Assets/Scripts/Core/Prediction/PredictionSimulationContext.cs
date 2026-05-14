using CheckmateRPG.Core.Simulation;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// Backward-compatible alias for existing call sites.
    /// </summary>
    public sealed class PredictionSimulationContext : PredictionContext
    {
        public PredictionSimulationContext(SimulationRuntime source, int tick)
            : base(source, tick, PredictionSource.Legacy)
        {
        }
    }
}
