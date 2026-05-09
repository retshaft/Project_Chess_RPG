using System;

namespace CheckmateRPG.Core
{
    [Flags]
    public enum UnitStatusFlags
    {
        None = 0,
        Dead = 1 << 0,
        MoveLocked = 1 << 1,
        AttackLocked = 1 << 2
    }

    [Serializable]
    public sealed class AbilityRuntimeState
    {
        public string AbilityId;
        public int CooldownRemaining;
        public int CooldownEndTick;
        public int Charges;
        public bool Locked;
        public Guid? PendingActionId;
        public int LastCommittedTick;
    }
}
