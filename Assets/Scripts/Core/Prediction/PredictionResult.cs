using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// Immutable container for all predicted outcomes produced by a
    /// <see cref="PredictionPipeline"/> run.  The runtime is never touched;
    /// all data here reflects what <em>would</em> happen if the supplied
    /// actions were resolved.
    /// </summary>
    public sealed class PredictionResult : IReadOnlyPredictionResult
    {
        private static readonly IReadOnlyList<PredictedDamage> EmptyDamages =
            Array.Empty<PredictedDamage>();
        private static readonly IReadOnlyList<PredictedPosition> EmptyPositions =
            Array.Empty<PredictedPosition>();
        private static readonly IReadOnlyList<PredictedDeath> EmptyDeaths =
            Array.Empty<PredictedDeath>();
        private static readonly IReadOnlyList<PredictedInterrupt> EmptyInterrupts =
            Array.Empty<PredictedInterrupt>();
        private static readonly IReadOnlyList<PredictedResolveOrder> EmptyOrder =
            Array.Empty<PredictedResolveOrder>();

        /// <summary>
        /// Returns an empty <see cref="PredictionResult"/> representing a no-op tick.
        /// </summary>
        public static PredictionResult Empty { get; } = new(
            EmptyDamages, EmptyPositions, EmptyDeaths, EmptyInterrupts, EmptyOrder);

        public PredictionResult(
            IReadOnlyList<PredictedDamage> damages,
            IReadOnlyList<PredictedPosition> positions,
            IReadOnlyList<PredictedDeath> deaths,
            IReadOnlyList<PredictedInterrupt> interrupts,
            IReadOnlyList<PredictedResolveOrder> resolveOrder)
        {
            Damages = damages ?? EmptyDamages;
            Positions = positions ?? EmptyPositions;
            Deaths = deaths ?? EmptyDeaths;
            Interrupts = interrupts ?? EmptyInterrupts;
            ResolveOrder = resolveOrder ?? EmptyOrder;
        }

        /// <summary>Damage events that would be applied, in mutation order.</summary>
        public IReadOnlyList<PredictedDamage> Damages { get; }
        public IReadOnlyList<PredictedDamage> ExpectedDamage => Damages;
        public IReadOnlyList<PredictedDamage> ExpectedDamages => Damages;

        /// <summary>Movement events that would be applied, in mutation order.</summary>
        public IReadOnlyList<PredictedPosition> Positions { get; }
        public IReadOnlyList<PredictedPosition> ExpectedPosition => Positions;
        public IReadOnlyList<PredictedPosition> ExpectedPositions => Positions;

        /// <summary>Units that would die as a result of the simulated tick.</summary>
        public IReadOnlyList<PredictedDeath> Deaths { get; }
        public IReadOnlyList<PredictedDeath> ExpectedDeaths => Deaths;

        /// <summary>Actions that are predicted to be interrupted or cancelled.</summary>
        public IReadOnlyList<PredictedInterrupt> Interrupts { get; }
        public IReadOnlyList<PredictedInterrupt> ExpectedInterrupt => Interrupts;
        public IReadOnlyList<PredictedInterrupt> ExpectedInterrupts => Interrupts;

        /// <summary>
        /// Deterministic resolve order for all non-cancelled actions, identical to the
        /// ordering the real <see cref="ResolutionPhasePipeline"/> would use.
        /// </summary>
        public IReadOnlyList<PredictedResolveOrder> ResolveOrder { get; }
    }
}
