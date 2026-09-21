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

        /// <summary>마지막 정상 이동(넉백·강제 이동 제외)의 직전 셀. <see cref="LastNormalMoveTick"/>이 0 미만이면 무효.</summary>
        Vector2Int LastNormalMoveFromCell { get; }

        /// <summary>마지막 정상 이동의 논리 좌표 확정 틱. 기록이 없으면 -1.</summary>
        int LastNormalMoveTick { get; }
    }
}
