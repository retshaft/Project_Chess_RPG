using System;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// The predicted position change for a unit within a prediction sandbox run.
    /// </summary>
    public readonly record struct PredictedPosition(
        Guid UnitId,
        Vector2Int From,
        Vector2Int To);
}
