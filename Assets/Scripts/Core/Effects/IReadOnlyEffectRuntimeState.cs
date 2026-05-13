using System;
using CheckmateRPG.Core;

namespace CheckmateRPG.Core.Effects
{
    public interface IReadOnlyEffectRuntimeState
    {
        string EffectId { get; }
        Guid SourceId { get; }
        Guid TargetId { get; }
        int RemainingTick { get; }
        int StackCount { get; }
        int TickInterval { get; }
        int NextTickIn { get; }
        float Magnitude { get; }
        bool IsExpired { get; }

        /// <summary>
        /// The total tick duration that was set when the effect was first applied.
        /// This value does not change as the effect counts down, making it useful for
        /// computing completion progress: <c>1 - RemainingTick / (float)RemainingDuration</c>.
        /// </summary>
        int RemainingDuration { get; }

        /// <summary>
        /// The simulation tick at which this effect was first applied to the target.
        /// Not updated on subsequent refreshes.
        /// </summary>
        int AppliedTick { get; }

        /// <summary>Current lifecycle stage of this effect.</summary>
        EffectLifecycle Lifecycle { get; }

        /// <summary>
        /// The timing phase in which this effect is evaluated by the
        /// <see cref="EffectTimingPipeline"/>. Defaults to <see cref="EffectTimingPhase.OnTickEnd"/>.
        /// </summary>
        EffectTimingPhase TimingPhase { get; }

        /// <summary>
        /// Speed tier used for deterministic ordering within the same
        /// <see cref="TimingPhase"/>. Lower values are processed first.
        /// </summary>
        ActionSpeedTier ActionSpeedLevel { get; }

        /// <summary>
        /// When <c>true</c>, this effect is a reaction and is restricted to the
        /// <see cref="EffectTimingPhase.OnPostResolve"/> phase.
        /// </summary>
        bool IsReaction { get; }
        EffectStackPolicy StackPolicy { get; }
        int MaxStackCap { get; }
    }
}
