using System;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Runtime.Ownership;
using UnityEngine;

namespace CheckmateRPG.Core.Effects
{
    [Serializable]
    public sealed class EffectRuntimeState : IEffectRuntime
    {
        public EffectRuntimeState()
        {
        }

        public EffectRuntimeState(
            string effectId,
            Guid sourceId,
            Guid targetId,
            int remainingTick,
            int stackCount,
            int tickInterval = 1,
            int nextTickIn = 1,
            float magnitude = 1f,
            EffectTimingPhase timingPhase = EffectTimingPhase.OnTickEnd,
            ActionSpeedTier actionSpeedLevel = ActionSpeedTier.Normal,
            bool isReaction = false,
            int appliedTick = 0,
            EffectStackPolicy stackPolicy = EffectStackPolicy.Refresh,
            int maxStackCap = int.MaxValue)
        {
            Seed(effectId, sourceId, targetId, remainingTick, stackCount, tickInterval, nextTickIn, magnitude, timingPhase, actionSpeedLevel, isReaction, appliedTick, stackPolicy, maxStackCap);
        }

        public EffectRuntimeState(EffectRuntimeState source)
        {
            if (source == null)
                return;

            Seed(
                source.EffectId,
                source.SourceId,
                source.TargetId,
                source.RemainingTick,
                source.StackCount,
                source.TickInterval,
                source.NextTickIn,
                source.Magnitude,
                source.TimingPhase,
                source.ActionSpeedLevel,
                source.IsReaction,
                source.AppliedTick,
                source.StackPolicy,
                source.MaxStackCap);

            RemainingDuration = source.RemainingDuration;
            Lifecycle = source.Lifecycle;
        }

        public string EffectId { get; private set; } = string.Empty;
        public Guid SourceId { get; private set; }
        public Guid TargetId { get; private set; }
        public int RemainingTick { get; private set; }
        public int StackCount { get; private set; }
        public int TickInterval { get; private set; } = 1;
        public int NextTickIn { get; private set; } = 1;
        public float Magnitude { get; private set; } = 1f;
        public bool IsExpired => RemainingTick <= 0;
        public EffectTimingPhase TimingPhase { get; private set; } = EffectTimingPhase.OnTickEnd;
        public ActionSpeedTier ActionSpeedLevel { get; private set; } = ActionSpeedTier.Normal;
        public bool IsReaction { get; private set; }
        public EffectStackPolicy StackPolicy { get; private set; } = EffectStackPolicy.Refresh;
        public int MaxStackCap { get; private set; } = int.MaxValue;

        /// <inheritdoc/>
        public int RemainingDuration { get; private set; }

        /// <inheritdoc/>
        public int AppliedTick { get; private set; }

        /// <inheritdoc/>
        public EffectLifecycle Lifecycle { get; private set; } = EffectLifecycle.Applied;

        internal void Seed(
            string effectId,
            Guid sourceId,
            Guid targetId,
            int remainingTick,
            int stackCount,
            int tickInterval = 1,
            int nextTickIn = 1,
            float magnitude = 1f,
            EffectTimingPhase timingPhase = EffectTimingPhase.OnTickEnd,
            ActionSpeedTier actionSpeedLevel = ActionSpeedTier.Normal,
            bool isReaction = false,
            int appliedTick = 0,
            EffectStackPolicy stackPolicy = EffectStackPolicy.Refresh,
            int maxStackCap = int.MaxValue)
        {
            EffectId = effectId ?? string.Empty;
            SourceId = sourceId;
            TargetId = targetId;
            RemainingTick = remainingTick;
            RemainingDuration = remainingTick;
            MaxStackCap = ResolveStackCap(maxStackCap);
            StackCount = ClampStack(stackCount, MaxStackCap);
            TickInterval = Mathf.Max(1, tickInterval);
            NextTickIn = Mathf.Max(1, nextTickIn);
            Magnitude = Mathf.Max(0f, magnitude);
            TimingPhase = timingPhase;
            ActionSpeedLevel = actionSpeedLevel;
            IsReaction = isReaction;
            AppliedTick = appliedTick;
            StackPolicy = stackPolicy;
            Lifecycle = EffectLifecycle.Applied;
        }

        internal void RefreshFromApplication(
            Guid sourceId,
            int stackToAdd,
            int remainingTick,
            int tickInterval,
            int nextTickIn,
            float magnitude,
            int maxStackCap,
            string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.EffectStack);

            SourceId = sourceId;
            MaxStackCap = ResolveStackCap(maxStackCap);
            StackCount = ClampStack(StackCount + Mathf.Max(0, stackToAdd), MaxStackCap);
            RemainingTick = Mathf.Max(RemainingTick, remainingTick);
            RemainingDuration = Mathf.Max(RemainingDuration, remainingTick);
            TickInterval = Mathf.Max(1, tickInterval);
            NextTickIn = Mathf.Clamp(nextTickIn, 1, TickInterval);
            Magnitude = Mathf.Max(0f, magnitude);
            Lifecycle = EffectLifecycle.Applied;
        }

        internal void ReplaceFromApplication(
            Guid sourceId,
            int stackCount,
            int remainingTick,
            int tickInterval,
            int nextTickIn,
            float magnitude,
            int appliedTick,
            EffectStackPolicy stackPolicy,
            int maxStackCap,
            string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.EffectStack);

            SourceId = sourceId;
            StackPolicy = stackPolicy;
            MaxStackCap = ResolveStackCap(maxStackCap);
            StackCount = ClampStack(stackCount, MaxStackCap);
            RemainingTick = Mathf.Max(1, remainingTick);
            RemainingDuration = RemainingTick;
            TickInterval = Mathf.Max(1, tickInterval);
            NextTickIn = Mathf.Clamp(nextTickIn, 1, TickInterval);
            Magnitude = Mathf.Max(0f, magnitude);
            AppliedTick = appliedTick;
            Lifecycle = EffectLifecycle.Applied;
        }

        internal void AdvanceTick(string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.EffectStack);
            RemainingTick--;
            NextTickIn--;
        }

        internal void ResetTickCountdown(string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.EffectStack);
            NextTickIn = TickInterval;
        }

        /// <summary>
        /// Transitions this effect to <paramref name="next"/> lifecycle stage.
        /// <para>
        /// Transitions from <see cref="EffectLifecycle.Removed"/> are silently ignored to
        /// prevent accidental resurrection of a removed effect.
        /// </para>
        /// </summary>
        internal void TransitionLifecycle(string ownerName, EffectLifecycle next)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.EffectLifecycle);

            if (Lifecycle == EffectLifecycle.Removed)
                return;

            Lifecycle = next;
        }

        private static int ResolveStackCap(int maxStackCap)
        {
            return maxStackCap <= 0 ? int.MaxValue : maxStackCap;
        }

        private static int ClampStack(int stackCount, int cap)
        {
            return Mathf.Clamp(Mathf.Max(1, stackCount), 1, cap);
        }
    }
}
