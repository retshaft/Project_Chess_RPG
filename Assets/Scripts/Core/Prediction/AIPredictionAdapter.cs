using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Replay;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    public readonly record struct PredictionActionEvaluation(
        float Score,
        int ExpectedDamage,
        int ExpectedDeathCount,
        int ExpectedInterruptCount,
        bool OccupancyConflict);

    public readonly record struct AIPredictionScenario(
        IActionCommand Action,
        RuntimeSnapshot PredictedSnapshot,
        PredictionActionEvaluation Evaluation,
        float Score);

    public sealed class AIPredictionAdapter : IReadOnlyPredictionQueryAdapter
    {
        private const float DeathScoreWeight = 100f;
        private const float InterruptPenalty = 80f;
        private const float OccupancyConflictPenalty = 20f;

        private readonly PredictionQueryService _queryService;
        private readonly PredictionPipeline _predictionPipeline;
        private readonly Func<SimulationRuntime> _runtimeProvider;
        private readonly Func<int> _tickProvider;

        public AIPredictionAdapter(
            PredictionQueryService queryService,
            PredictionPipeline predictionPipeline = null,
            Func<SimulationRuntime> runtimeProvider = null,
            Func<int> tickProvider = null)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _predictionPipeline = predictionPipeline;
            _runtimeProvider = runtimeProvider;
            _tickProvider = tickProvider;
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

            return Evaluate(action, result);
        }

        public IReadOnlyList<AIPredictionScenario> ExploreScenarios(
            UnitBrain brain,
            int maxScenariosPerTick,
            AIEvaluationMetrics metrics = null)
        {
            var scenarios = new List<AIPredictionScenario>();
            if (brain == null)
                return scenarios;

            int scenarioCap = Mathf.Max(1, maxScenariosPerTick);
            IReadOnlyList<IActionCommand> candidates = brain.BuildPredictionActionCandidates(scenarioCap);
            if (candidates == null || candidates.Count == 0)
                return scenarios;

            SimulationRuntime runtime = _runtimeProvider != null ? _runtimeProvider() : null;
            if (runtime == null)
                return scenarios;

            int tick = _tickProvider != null ? _tickProvider() : runtime.CurrentTick;
            RuntimeSnapshot currentSnapshot = AIEvaluationMetrics.CaptureRuntimeSnapshot(runtime);
            AIEvaluationMetrics evaluator = metrics ?? AIEvaluationMetrics.Default;

            for (int i = 0; i < candidates.Count && scenarios.Count < scenarioCap; i++)
            {
                IActionCommand action = candidates[i];
                if (action == null)
                    continue;

                var branchRuntime = new PredictionRuntimeClone(runtime, tick);
                IReadOnlyPredictionResult prediction = _predictionPipeline != null
                    ? _predictionPipeline.Execute(
                        new[] { action },
                        branchRuntime.ClonedRuntime,
                        tick,
                        PredictionSource.AI)
                    : _queryService.Query(new[] { action }, PredictionSource.AI);

                if (prediction == null)
                    continue;

                PredictionActionEvaluation evaluation = Evaluate(action, prediction);
                RuntimeSnapshot predictedSnapshot = AIEvaluationMetrics.ProjectSnapshot(currentSnapshot, prediction);
                float score = evaluation.Score + evaluator.Evaluate(currentSnapshot, predictedSnapshot, brain.ActorId);

                scenarios.Add(new AIPredictionScenario(action, predictedSnapshot, evaluation, score));
            }

            return scenarios;
        }

        public bool TryGetBestAction(
            UnitBrain brain,
            AIEvaluationMetrics metrics,
            int maxScenariosPerTick,
            out IActionCommand bestAction)
        {
            bestAction = null;
            IReadOnlyList<AIPredictionScenario> scenarios = ExploreScenarios(brain, maxScenariosPerTick, metrics);
            if (scenarios.Count == 0)
                return false;

            float bestScore = float.MinValue;
            for (int i = 0; i < scenarios.Count; i++)
            {
                AIPredictionScenario scenario = scenarios[i];
                if (scenario.Action == null)
                    continue;

                if (bestAction == null || scenario.Score > bestScore)
                {
                    bestAction = scenario.Action;
                    bestScore = scenario.Score;
                }
            }

            return bestAction != null;
        }

        private static PredictionActionEvaluation Evaluate(IActionCommand action, IReadOnlyPredictionResult result)
        {
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
