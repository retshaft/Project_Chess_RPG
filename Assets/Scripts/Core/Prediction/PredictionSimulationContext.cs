using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Runtime.Mutations;
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
    public sealed class PredictionSimulationContext
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
        public PredictionSimulationContext(SimulationRuntime source, int tick)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            PredictedRuntime = source.CreateSnapshot(tick);
        }

        /// <summary>
        /// The isolated, cloned runtime on which all prediction mutations are applied.
        /// Never the same instance as the live production runtime.
        /// </summary>
        public SimulationRuntime PredictedRuntime { get; }

        // ── Accumulated predictions ───────────────────────────────────────────────

        /// <summary>Damage outcomes buffered so far.</summary>
        public IReadOnlyList<PredictedDamage> PredictedDamages => _damages;

        /// <summary>Movement outcomes buffered so far.</summary>
        public IReadOnlyList<PredictedPosition> PredictedPositions => _positions;

        /// <summary>Death outcomes buffered so far.</summary>
        public IReadOnlyList<PredictedDeath> PredictedDeaths => _deaths;

        /// <summary>Interrupt outcomes buffered so far.</summary>
        public IReadOnlyList<PredictedInterrupt> PredictedInterrupts => _interrupts;

        /// <summary>Resolve ordering captured for the current run.</summary>
        public IReadOnlyList<PredictedResolveOrder> PredictedResolveOrder => _resolveOrder;

        // ── Internal accumulation API ─────────────────────────────────────────────

        internal void RecordDamage(PredictedDamage damage) => _damages.Add(damage);

        internal void RecordPosition(PredictedPosition position) => _positions.Add(position);

        internal void RecordDeath(PredictedDeath death) => _deaths.Add(death);

        internal void RecordInterrupt(PredictedInterrupt interrupt) => _interrupts.Add(interrupt);

        internal void RecordResolveOrder(PredictedResolveOrder order) => _resolveOrder.Add(order);

        // ── Result materialisation ────────────────────────────────────────────────

        /// <summary>
        /// Materialises the accumulated predictions into an immutable
        /// <see cref="PredictionResult"/>.
        /// </summary>
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
