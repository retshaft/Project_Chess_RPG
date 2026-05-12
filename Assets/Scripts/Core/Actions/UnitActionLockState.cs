using System;

namespace CheckmateRPG.Core.Actions
{
    [Serializable]
    public sealed class UnitActionLockState
    {
        public ActionLockType ActiveLock { get; private set; }
        public Guid? SourceActionId { get; private set; }
        public int ExpirationTick { get; private set; }

        public bool IsActiveAt(int tick)
        {
            return ActiveLock != ActionLockType.None &&
                   SourceActionId.HasValue &&
                   tick < ExpirationTick;
        }

        internal void Acquire(ActionLockType lockType, Guid sourceActionId, int expirationTick)
        {
            if (lockType == ActionLockType.None || sourceActionId == Guid.Empty)
            {
                Release();
                return;
            }

            ActiveLock = lockType;
            SourceActionId = sourceActionId;
            ExpirationTick = Math.Max(0, expirationTick);
        }

        internal bool ReleaseIfSource(Guid sourceActionId)
        {
            if (!SourceActionId.HasValue || SourceActionId.Value != sourceActionId)
                return false;

            Release();
            return true;
        }

        internal void Release()
        {
            ActiveLock = ActionLockType.None;
            SourceActionId = null;
            ExpirationTick = 0;
        }
    }
}
