using System;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// The predicted outcome of a damage mutation within a prediction sandbox run.
    /// </summary>
    public readonly record struct PredictedDamage(
        Guid MutationId,
        Guid TargetId,
        Guid SourceId,
        int Amount,
        bool IsCritical,
        int ResultingHp);
}
