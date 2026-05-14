using System.Collections.Generic;

namespace CheckmateRPG.Core.Prediction
{
    public interface IReadOnlyPredictionResult
    {
        IReadOnlyList<PredictedDamage> ExpectedDamage { get; }
        IReadOnlyList<PredictedPosition> ExpectedPosition { get; }
        IReadOnlyList<PredictedInterrupt> ExpectedInterrupt { get; }
        IReadOnlyList<PredictedDeath> ExpectedDeaths { get; }
        IReadOnlyList<PredictedResolveOrder> ResolveOrder { get; }
    }
}
