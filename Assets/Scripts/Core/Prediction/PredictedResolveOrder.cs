using System;
using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// Describes the deterministic position of a single action within the predicted resolve sequence.
    /// </summary>
    public readonly record struct PredictedResolveOrder(
        Guid ActionId,
        Guid ActorId,
        ActionSpeedTier SpeedTier,
        int ResolveTick,
        int PositionInOrder);
}
