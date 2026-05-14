using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Prediction
{
    internal static class PredictionMutationSandbox
    {
        internal static void Apply(IReadOnlyList<IRuntimeMutation> orderedMutations, PredictionContext context)
        {
            if (context == null || orderedMutations == null || orderedMutations.Count == 0)
                return;

            PredictionIsolation.AssertRuntimeIsolation(context.RuntimeClone);
            PredictionIsolation.AssertMutationSandboxTarget(context.RuntimeClone, context.ClonedRuntime);
            PredictionMutationApplier.Apply(orderedMutations, context);
        }
    }
}
