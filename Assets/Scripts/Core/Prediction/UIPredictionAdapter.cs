using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    public sealed class UIPredictionAdapter : IReadOnlyPredictionQueryAdapter
    {
        private readonly PredictionQueryService _queryService;

        public UIPredictionAdapter(PredictionQueryService queryService)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        }

        public IReadOnlyPredictionResult Query(IReadOnlyList<IActionCommand> actions)
        {
            return _queryService.Query(actions, PredictionSource.UI);
        }

        public IReadOnlyDictionary<Vector2Int, Guid> QueryOccupancy(IReadOnlyList<IActionCommand> actions)
        {
            return _queryService.PredictOccupancy(actions, PredictionSource.UI);
        }
    }
}
