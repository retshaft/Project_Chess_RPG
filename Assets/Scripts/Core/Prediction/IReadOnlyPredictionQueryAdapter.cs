using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    public interface IReadOnlyPredictionQueryAdapter
    {
        IReadOnlyPredictionResult Query(IReadOnlyList<IActionCommand> actions);
        IReadOnlyDictionary<Vector2Int, Guid> QueryOccupancy(IReadOnlyList<IActionCommand> actions);
    }
}
