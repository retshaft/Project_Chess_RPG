using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Simulation;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// Holds the cloned <see cref="SimulationRuntime"/> used during a prediction run and
    /// accumulates the predicted outcomes as mutations are applied to the sandbox.
    /// <para>
    /// Isolation guarantees:
    /// <list type="bullet">
    ///   <item>Only the cloned runtime is ever mutated — the source runtime is untouched.</item>
    ///   <item>No events are broadcast outside this context.</item>
    ///   <item>No persistence or external side effects occur.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class PredictionContext
    {
        private readonly List<PredictedDamage> _damages = new();
        private readonly List<PredictedPosition> _positions = new();
        private readonly List<PredictedDeath> _deaths = new();
        private readonly List<PredictedInterrupt> _interrupts = new();
        private readonly List<PredictedResolveOrder> _resolveOrder = new();

        /// <summary>
        /// Creates a new context whose sandbox runtime is a deep clone of
        /// <paramref name="source"/> at the specified tick.
        /// </summary>
        public PredictionContext(
            SimulationRuntime source,
            int predictionTick,
            PredictionSource predictionSource,
            Guid predictionId = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            PredictionTick = predictionTick;
            PredictionSource = predictionSource;
            PredictionId = predictionId == Guid.Empty
                ? Guid.NewGuid()
                : predictionId;
            RuntimeClone = new PredictionRuntimeClone(source, predictionTick);
            PredictionIsolation.AssertRuntimeIsolation(RuntimeClone);
        }

        /// <summary>
        /// Tick associated with this prediction run.
        /// </summary>
        public int PredictionTick { get; }

        /// <summary>
        /// The origin of this prediction request.
        /// </summary>
        public PredictionSource PredictionSource { get; }

        /// <summary>
        /// Unique identifier for this prediction run.
        /// </summary>
        public Guid PredictionId { get; }

        /// <summary>
        /// Runtime clone handle for this prediction run.
        /// </summary>
        public PredictionRuntimeClone RuntimeClone { get; }

        /// <summary>
        /// Cloned runtime where prediction is resolved.
        /// </summary>
        public SimulationRuntime ClonedRuntime => RuntimeClone.ClonedRuntime;

        public IReadOnlyList<PredictedDamage> PredictedDamages => _damages;
        public IReadOnlyList<PredictedPosition> PredictedPositions => _positions;
        public IReadOnlyList<PredictedDeath> PredictedDeaths => _deaths;
        public IReadOnlyList<PredictedInterrupt> PredictedInterrupts => _interrupts;
        public IReadOnlyList<PredictedResolveOrder> PredictedResolveOrder => _resolveOrder;

        internal void RecordDamage(PredictedDamage damage) => _damages.Add(damage);
        internal void RecordPosition(PredictedPosition position) => _positions.Add(position);
        internal void RecordDeath(PredictedDeath death) => _deaths.Add(death);
        internal void RecordInterrupt(PredictedInterrupt interrupt) => _interrupts.Add(interrupt);
        internal void RecordResolveOrder(PredictedResolveOrder order) => _resolveOrder.Add(order);

        public PredictionResult ToResult()
        {
            return new PredictionResult(
                new List<PredictedDamage>(_damages),
                new List<PredictedPosition>(_positions),
                new List<PredictedDeath>(_deaths),
                new List<PredictedInterrupt>(_interrupts),
                new List<PredictedResolveOrder>(_resolveOrder));
        }
    }
}
