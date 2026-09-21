using System;
using UnityEngine;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    public readonly record struct BattleUnitSnapshot(
        Guid UnitId,
        Vector2Int Position,
        int HP,
        UnitStatusFlags StatusFlags,
        Vector2Int LastNormalMoveFromCell = default,
        int LastNormalMoveTick = -1);

    public interface IBattleContext
    {
        bool TryGetUnit(Guid unitId, out BattleUnitSnapshot unit);
        bool IsCellValid(Vector2Int cell);
        bool IsCellOccupied(Vector2Int cell, Guid ignoredUnitId = default);
        bool IsTargetInAttackRange(Guid attackerId, Guid targetId);

        /// <summary>
        /// 이동 직후 공격 판정 유예가 적용된 사거리 판정. 현재 위치의 정상 판정을 먼저 수행하고,
        /// 실패했을 때만 공격 접수 이후 대상의 정상 이동 기록과 유예 틱 안에서
        /// 대상의 이동 직전 위치로 재검사한다 (Core.AttackGraceRules 규칙).
        /// </summary>
        /// <param name="attackQueuedTick">공격이 접수된 틱 (명령의 QueuedTick).</param>
        /// <param name="currentTick">타격 판정 시점의 틱 (해석 틱).</param>
        bool IsTargetInAttackRangeWithGrace(Guid attackerId, Guid targetId, int attackQueuedTick, int currentTick);

        int CriticalDamageMultiplier { get; }
        int GetAttackCount(Guid unitId);
        float GetAttackDamageRatio(Guid unitId);
        float GetDefPenetrationRatio(Guid attackerId, Guid targetId);
        float GetAttackDamageMultiplier(Guid unitId);
        void NotifyAttackPerformed(Guid unitId);
        void NotifyAttackStart(Guid attackerId, Guid targetId);
        int GetOnHitEffectCount(Guid unitId, CheckmateRPG.Core.StatusEffectType type);
    }
}
