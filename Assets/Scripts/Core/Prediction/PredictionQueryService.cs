using System;
using System.Collections.Generic;
using System.Text;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    public sealed class PredictionQueryService
    {
        private const int EstimatedCharsPerAction = 96;

        private readonly Func<int> _tickProvider;
        private readonly Func<IReadOnlySimulationRuntime> _runtimeProvider;
        private readonly Func<IReadOnlyList<IActionCommand>, PredictionSource, PredictionResult> _predictionExecutor;
        private readonly Dictionary<PredictionQueryCacheKey, PredictionResult> _resultCache = new();
        private int _cachedTick = int.MinValue;

        public PredictionQueryService(
            Func<int> tickProvider,
            Func<IReadOnlySimulationRuntime> runtimeProvider,
            Func<IReadOnlyList<IActionCommand>, PredictionSource, PredictionResult> predictionExecutor)
        {
            _tickProvider = tickProvider ?? throw new ArgumentNullException(nameof(tickProvider));
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            _predictionExecutor = predictionExecutor ?? throw new ArgumentNullException(nameof(predictionExecutor));
        }

        public IReadOnlyPredictionResult Query(IReadOnlyList<IActionCommand> actions, PredictionSource source)
        {
            if (actions == null || actions.Count == 0)
                return PredictionResult.Empty;

            PredictionResult result = GetOrExecute(actions, source);
            return result ?? PredictionResult.Empty;
        }

        public IReadOnlyList<PredictedPosition> PredictMove(IReadOnlyList<IActionCommand> actions, PredictionSource source)
        {
            return Query(actions, source).ExpectedPosition;
        }

        public IReadOnlyList<PredictedDamage> PredictDamage(IReadOnlyList<IActionCommand> actions, PredictionSource source)
        {
            return Query(actions, source).ExpectedDamage;
        }

        public IReadOnlyList<PredictedInterrupt> PredictInterrupt(IReadOnlyList<IActionCommand> actions, PredictionSource source)
        {
            return Query(actions, source).ExpectedInterrupt;
        }

        public IReadOnlyDictionary<Vector2Int, Guid> PredictOccupancy(IReadOnlyList<IActionCommand> actions, PredictionSource source)
        {
            PredictionResult result = GetOrExecute(actions, source);
            if (result == null)
                return new Dictionary<Vector2Int, Guid>();

            return BuildPredictedOccupancy(result);
        }

        private PredictionResult GetOrExecute(IReadOnlyList<IActionCommand> actions, PredictionSource source)
        {
            if (actions == null || actions.Count == 0)
                return PredictionResult.Empty;

            int tick = _tickProvider();
            if (tick != _cachedTick)
            {
                _cachedTick = tick;
                _resultCache.Clear();
            }

            var key = new PredictionQueryCacheKey(
                tick,
                source,
                BuildActionSignature(actions));

            if (_resultCache.TryGetValue(key, out PredictionResult cached) && cached != null)
                return cached;

            PredictionResult computed = _predictionExecutor(actions, source) ?? PredictionResult.Empty;
            _resultCache[key] = computed;
            return computed;
        }

        private IReadOnlyDictionary<Vector2Int, Guid> BuildPredictedOccupancy(PredictionResult result)
        {
            IReadOnlySimulationRuntime runtime = _runtimeProvider();
            var occupancy = new Dictionary<Vector2Int, Guid>(runtime.OccupiedPositions);
            var unitPositions = new Dictionary<Guid, Vector2Int>();

            foreach (KeyValuePair<Guid, IReadOnlyUnitRuntimeState> pair in runtime.RuntimeStates)
            {
                if (pair.Value == null || pair.Key == Guid.Empty)
                    continue;
                unitPositions[pair.Key] = pair.Value.Position;
            }

            for (int i = 0; i < result.ExpectedPosition.Count; i++)
            {
                PredictedPosition move = result.ExpectedPosition[i];
                if (move.UnitId == Guid.Empty)
                    continue;

                if (unitPositions.TryGetValue(move.UnitId, out Vector2Int currentPos))
                    occupancy.Remove(currentPos);
                else
                    occupancy.Remove(move.From);

                occupancy[move.To] = move.UnitId;
                unitPositions[move.UnitId] = move.To;
            }

            for (int i = 0; i < result.ExpectedDeaths.Count; i++)
            {
                PredictedDeath death = result.ExpectedDeaths[i];
                if (death.UnitId == Guid.Empty)
                    continue;

                if (unitPositions.TryGetValue(death.UnitId, out Vector2Int deadPos))
                    occupancy.Remove(deadPos);
            }

            return occupancy;
        }

        private static string BuildActionSignature(IReadOnlyList<IActionCommand> actions)
        {
            if (actions == null || actions.Count == 0)
                return string.Empty;

            var sb = new StringBuilder(actions.Count * EstimatedCharsPerAction);
            for (int i = 0; i < actions.Count; i++)
            {
                IActionCommand action = actions[i];
                if (action == null)
                {
                    sb.Append("null|");
                    continue;
                }

                sb.Append(action.GetType().Name).Append(':')
                  .Append(action.ActorId.ToString("N")).Append(':')
                  .Append((int)action.State).Append(':')
                  .Append(action.QueuedTick).Append(':')
                  .Append(action.StartTick).Append(':')
                  .Append(action.ResolveTick).Append(':')
                  .Append(action.RecoveryEndTick).Append(':')
                  .Append((int)action.SpeedTier).Append(':')
                  .Append((int)action.InterruptPriority).Append(':')
                  .Append((int)action.InterruptWindow).Append(':')
                  .Append((int)action.IntentLockType).Append(':')
                  .Append((int)action.ConcurrencyPolicy).Append(':');

                switch (action)
                {
                    case MoveActionCommand move:
                        sb.Append(move.From.x).Append(',').Append(move.From.y).Append('>')
                          .Append(move.To.x).Append(',').Append(move.To.y);
                        break;
                    case AttackActionCommand attack:
                        sb.Append(attack.TargetId.ToString("N")).Append(':')
                          .Append(attack.Damage).Append(':')
                          .Append(attack.IsCritical ? '1' : '0');
                        break;
                    case AbilityActionCommand ability:
                        sb.Append(ability.AbilityId).Append(':').Append(ability.TargetIds.Count);
                        for (int t = 0; t < ability.TargetIds.Count; t++)
                        {
                            sb.Append(':').Append(ability.TargetIds[t].ToString("N"));
                        }
                        break;
                }

                sb.Append('|');
            }

            return sb.ToString();
        }

        private readonly record struct PredictionQueryCacheKey(
            int Tick,
            PredictionSource Source,
            string ActionSignature);
    }
}
