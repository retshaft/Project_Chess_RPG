using System;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// Indicates that a unit is predicted to die as a result of the simulated actions.
    /// </summary>
    public readonly record struct PredictedDeath(
        Guid UnitId,
        Guid KilledByActionId);
}
