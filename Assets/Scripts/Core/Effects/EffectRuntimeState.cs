using System;

namespace CheckmateRPG.Core.Effects
{
    [Serializable]
    public sealed class EffectRuntimeState
    {
        public string EffectId;
        public Guid SourceId;
        public Guid TargetId;
        public int RemainingTick;
        public int StackCount;

        public int TickInterval = 1;
        public int NextTickIn = 1;
        public float Magnitude = 1f;

        public bool IsExpired => RemainingTick <= 0;
    }
}
