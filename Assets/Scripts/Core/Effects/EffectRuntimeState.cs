using System;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Runtime.Ownership;
using UnityEngine;

namespace CheckmateRPG.Core.Effects
{
    [Serializable]
    public sealed class EffectRuntimeState : IReadOnlyEffectRuntimeState
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
            bool isReaction = false)
        {
            Seed(effectId, sourceId, targetId, remainingTick, stackCount, tickInterval, nextTickIn, magnitude, timingPhase, actionSpeedLevel, isReaction);
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
                source.IsReaction);
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
            bool isReaction = false)
        {
            EffectId = effectId ?? string.Empty;
            SourceId = sourceId;
            TargetId = targetId;
            RemainingTick = remainingTick;
            StackCount = Mathf.Max(1, stackCount);
            TickInterval = Mathf.Max(1, tickInterval);
            NextTickIn = Mathf.Max(1, nextTickIn);
            Magnitude = Mathf.Max(0f, magnitude);
            TimingPhase = timingPhase;
            ActionSpeedLevel = actionSpeedLevel;
            IsReaction = isReaction;
        }

        internal void RefreshFromApplication(
            Guid sourceId,
            int stackCount,
            int remainingTick,
            int tickInterval,
            int nextTickIn,
            float magnitude,
            string ownerName)
        {
            OwnershipValidationService.Default.EnsureAuthorized(ownerName, OwnershipStateKeys.EffectStack);

            SourceId = sourceId;
            StackCount = Mathf.Max(1, stackCount);
            RemainingTick = Mathf.Max(RemainingTick, remainingTick);
            TickInterval = Mathf.Max(1, tickInterval);
            NextTickIn = Mathf.Clamp(nextTickIn, 1, TickInterval);
            Magnitude = Mathf.Max(0f, magnitude);
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
    }
}
