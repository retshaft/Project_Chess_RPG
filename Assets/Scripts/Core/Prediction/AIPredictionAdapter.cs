using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    public readonly record struct PredictionActionEvaluation(
        float Score,
        int ExpectedDamage,
        int ExpectedDeathCount,
        int ExpectedInterruptCount,
        bool OccupancyConflict);

    public sealed class AIPredictionAdapter : IReadOnlyPredictionQueryAdapter
    {
        private const float DeathScoreWeight = 100f;
        private const float InterruptPenalty = 80f;
        private const float OccupancyConflictPenalty = 20f;

        private readonly PredictionQueryService _queryService;

        public AIPredictionAdapter(PredictionQueryService queryService)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        }

        public IReadOnlyPredictionResult Query(IReadOnlyList<IActionCommand> actions)
        {
            return _queryService.Query(actions, PredictionSource.AI);
        }

        public IReadOnlyDictionary<Vector2Int, Guid> QueryOccupancy(IReadOnlyList<IActionCommand> actions)
        {
            return _queryService.PredictOccupancy(actions, PredictionSource.AI);
        }

        public PredictionActionEvaluation Evaluate(IActionCommand action)
        {
            if (action == null)
                return default;

            IReadOnlyPredictionResult result = _queryService.Query(new[] { action }, PredictionSource.AI);
            if (result == null)
                return default;

            int expectedDamage = 0;
            for (int i = 0; i < result.ExpectedDamage.Count; i++)
            {
                PredictedDamage damage = result.ExpectedDamage[i];
                if (damage.SourceId == action.ActorId)
                    expectedDamage += Mathf.Max(0, damage.Amount);
            }

            int expectedDeathCount = 0;
            for (int i = 0; i < result.ExpectedDeaths.Count; i++)
            {
                PredictedDeath death = result.ExpectedDeaths[i];
                if (death.KilledByActionId == action.ActionId)
                    expectedDeathCount++;
            }

            int expectedInterruptCount = 0;
            bool occupancyConflict = false;
            for (int i = 0; i < result.ExpectedInterrupt.Count; i++)
            {
                PredictedInterrupt interrupt = result.ExpectedInterrupt[i];
                if (interrupt.ActionId != action.ActionId)
                    continue;

                expectedInterruptCount++;
                if (interrupt.Reason == ActionCancellationReason.ReservationLost)
                    occupancyConflict = true;
            }

            float score = expectedDamage + (expectedDeathCount * DeathScoreWeight) - (expectedInterruptCount * InterruptPenalty);
            if (occupancyConflict)
                score -= OccupancyConflictPenalty;

            return new PredictionActionEvaluation(
                score,
                expectedDamage,
                expectedDeathCount,
                expectedInterruptCount,
                occupancyConflict);
        }
    }
}
