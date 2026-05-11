using System;

namespace CheckmateRPG.Core.Effects
{
    /// <summary>
    /// Immutable snapshot of the execution context supplied to every effect evaluation
    /// inside the <see cref="EffectTimingPipeline"/>.
    /// </summary>
    public readonly struct EffectTimingContext
    {
        /// <param name="currentTick">The simulation tick at which this context was created.</param>
        /// <param name="currentPhase">The timing phase currently executing.</param>
        /// <param name="sourceUnitId">Unit ID of the effect source (may be <see cref="Guid.Empty"/>).</param>
        /// <param name="targetUnitId">Unit ID of the effect target.</param>
        /// <param name="actionId">ID of the action that triggered this effect evaluation (may be <see cref="Guid.Empty"/>).</param>
        public EffectTimingContext(
            int currentTick,
            EffectTimingPhase currentPhase,
            Guid sourceUnitId,
            Guid targetUnitId,
            Guid actionId)
        {
            CurrentTick = currentTick;
            CurrentPhase = currentPhase;
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            ActionId = actionId;
        }

        /// <summary>The simulation tick at which the pipeline is running.</summary>
        public int CurrentTick { get; }

        /// <summary>The timing phase currently being processed by the pipeline.</summary>
        public EffectTimingPhase CurrentPhase { get; }

        /// <summary>
        /// Unit ID of the entity that owns or cast the effect.
        /// <see cref="Guid.Empty"/> when no source unit is associated.
        /// </summary>
        public Guid SourceUnitId { get; }

        /// <summary>Unit ID of the entity the effect is applied to.</summary>
        public Guid TargetUnitId { get; }

        /// <summary>
        /// ID of the action that triggered this effect evaluation.
        /// <see cref="Guid.Empty"/> for passive or tick-driven effects.
        /// </summary>
        public Guid ActionId { get; }
    }
}
