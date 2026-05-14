using System;
using CheckmateRPG.Core.Simulation;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// Runtime clone handle for prediction execution.
    /// </summary>
    public sealed class PredictionRuntimeClone
    {
        public PredictionRuntimeClone(SimulationRuntime sourceRuntime, int predictionTick)
        {
            SourceRuntime = sourceRuntime ?? throw new ArgumentNullException(nameof(sourceRuntime));
            ClonedRuntime = sourceRuntime.CreateSnapshot(predictionTick);
            if (ReferenceEquals(SourceRuntime, ClonedRuntime))
            {
                throw new InvalidOperationException(
                    "Prediction runtime clone must never share the same runtime instance as source runtime.");
            }
        }

        public SimulationRuntime SourceRuntime { get; }
        public SimulationRuntime ClonedRuntime { get; }
    }
}
