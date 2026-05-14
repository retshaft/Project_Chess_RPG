using System;
using CheckmateRPG.Core.Simulation;

namespace CheckmateRPG.Core.Prediction
{
    internal static class PredictionIsolation
    {
        internal static void AssertRuntimeIsolation(PredictionRuntimeClone runtimeClone)
        {
            if (runtimeClone == null)
                throw new ArgumentNullException(nameof(runtimeClone));

            if (ReferenceEquals(runtimeClone.SourceRuntime, runtimeClone.ClonedRuntime))
            {
                throw new InvalidOperationException(
                    "Prediction isolation violated: shared mutable runtime is not allowed.");
            }
        }

        internal static void AssertMutationSandboxTarget(PredictionRuntimeClone runtimeClone, SimulationRuntime mutationTarget)
        {
            if (runtimeClone == null)
                throw new ArgumentNullException(nameof(runtimeClone));
            if (mutationTarget == null)
                throw new ArgumentNullException(nameof(mutationTarget));

            if (!ReferenceEquals(runtimeClone.ClonedRuntime, mutationTarget))
            {
                throw new InvalidOperationException(
                    "Prediction isolation violated: prediction mutation target must be the cloned runtime only.");
            }
        }
    }
}
