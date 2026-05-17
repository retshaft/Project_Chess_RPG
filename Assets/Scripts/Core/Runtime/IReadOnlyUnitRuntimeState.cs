using System;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime
{
    public interface IReadOnlyUnitRuntimeState
    {
        Guid UnitId { get; }
        int HP { get; }
        int CurrentSP { get; }
        int MaxSP { get; }
        int SP { get; }
        Vector2Int Position { get; }
        Guid? CurrentActionId { get; }
        int RecoveryUntilTick { get; }
        UnitStatusFlags StatusFlags { get; }
        bool HasBaseline { get; }
    }
}
