using System;
using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// Indicates that an action is predicted to be interrupted or cancelled before resolving.
    /// </summary>
    public readonly record struct PredictedInterrupt(
        Guid ActionId,
        Guid ActorId,
        ActionCancellationReason Reason);
}
