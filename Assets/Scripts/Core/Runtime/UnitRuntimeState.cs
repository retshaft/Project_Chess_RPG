using System;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime
{
    [Serializable]
    public sealed class UnitRuntimeState
    {
        public Guid UnitId;
        public int HP;
        public int SP;
        public Vector2Int Position;
        public Guid? CurrentActionId;
        public int RecoveryUntilTick;
        public UnitStatusFlags StatusFlags;
    }
}
