using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    [Serializable]
    public sealed class FrameSnapshot
    {
        public int Tick;
        public List<UnitFrameSnapshot> Units = new();
        public List<ActionFrameSnapshot> ActiveActions = new();
        public List<EffectFrameSnapshot> ActiveEffects = new();
    }

    [Serializable]
    public sealed class UnitFrameSnapshot
    {
        public string UnitId;
        public int HP;
        public int SP;
        public int PosX;
        public int PosY;
        public UnitStatusFlags StatusFlags;
        public string CurrentActionId;
        public int RecoveryUntilTick;
    }

    [Serializable]
    public sealed class ActionFrameSnapshot
    {
        public string ActionId;
        public string ActorId;
        public string ActionType;
        public string State;
        public int QueuedTick;
        public int StartTick;
        public int ResolveTick;
        public int RecoveryEndTick;
        public string Data;
    }

    [Serializable]
    public sealed class EffectFrameSnapshot
    {
        public string EffectId;
        public string SourceId;
        public string TargetId;
        public int RemainingTick;
        public int StackCount;
        public int TickInterval;
        public int NextTickIn;
        public float Magnitude;
    }
}
