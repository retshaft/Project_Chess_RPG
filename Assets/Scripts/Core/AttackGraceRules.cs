using System;
using CheckmateRPG.Data;
using UnityEngine;

namespace CheckmateRPG.Core
{
    /// <summary>
    /// 이동 직후 공격 판정 유예 규칙 (순수 함수 — 실전·예측·리플레이에서 같은 함수를 사용한다).
    /// <para>
    /// 이동 전에 유효하게 시작한 공격만, 대상의 "정상 이동 직전 위치"를 기준으로 짧은 시간 안에
    /// 다시 판정할 기회를 얻는다. 사거리 판정만 완화하며 피해 대상·피해량·기존 사거리 규칙
    /// (<see cref="CombatPatternRules"/>)은 변경하지 않는다. 넉백·당기기 등 강제 이동은
    /// 기록 자체에서 제외되므로 유예 대상이 아니다.
    /// </para>
    /// </summary>
    public static class AttackGraceRules
    {
        /// <summary>대상의 마지막 정상 이동 기록. Tick이 0 미만이면 기록이 없다.</summary>
        public readonly record struct TargetMoveRecord(Vector2Int FromCell, int Tick)
        {
            public static TargetMoveRecord Empty => new(Vector2Int.zero, -1);
            public bool IsEmpty => Tick < 0;
        }

        /// <summary>
        /// 유예 창이 열려 있는지 판정한다. 대상의 직전 이동이 "이 공격 접수 이후"이고,
        /// 논리 이동 완료(기록 틱)로부터 graceTicks 안이며, 공격자는 접수 후 이동하지 않았어야 한다.
        /// </summary>
        public static bool IsGraceWindowOpen(
            TargetMoveRecord targetLastNormalMove,
            int attackerLastNormalMoveTick,
            int attackQueuedTick,
            int currentTick,
            int graceTicks)
        {
            if (graceTicks <= 0)
                return false;
            if (targetLastNormalMove.IsEmpty)
                return false;
            // 조건 2: 공격 대상이 확정된 뒤(접수 뒤)에 이동했어야 한다.
            if (targetLastNormalMove.Tick <= attackQueuedTick)
                return false;
            // 미래 기록 방어.
            if (targetLastNormalMove.Tick > currentTick)
                return false;
            // 조건 3: 타격 시점이 유예 시간 안이어야 한다 (논리 이동 완료 틱 기준).
            if (currentTick - targetLastNormalMove.Tick > graceTicks)
                return false;
            // 공격자가 접수 후 이동했다면 실패 원인이 대상 이동만이 아니므로 유예 배제.
            if (attackerLastNormalMoveTick >= 0 && attackerLastNormalMoveTick > attackQueuedTick)
                return false;

            return true;
        }

        /// <summary>
        /// 기물 종류·팀 방향·사거리와 경로 점유 샘플러로 기존 사거리 규칙(<see cref="CombatPatternRules"/>)
        /// 판정 함수를 만든다. 실전(RuntimeBattleContext)과 예측(PredictionBattleContext)이
        /// 이 함수를 공유해 같은 기물 규칙을 쓰도록 한다 — 점유 샘플러만 각자의 상태(실전/클론)를 본다.
        /// </summary>
        public static Func<Vector2Int, Vector2Int, bool> BuildPieceReachJudge(
            ChessPieceType pieceType,
            bool isEnemy,
            int range,
            Func<Vector2Int, bool> isBlockedCell = null)
        {
            return (attackerCell, targetCell) => CombatPatternRules.IsAttackReachable(
                pieceType,
                isEnemy,
                attackerCell,
                targetCell,
                range,
                isBlockedCell);
        }

        /// <summary>
        /// 유예가 적용되는 사거리 판정 전체. 현재 위치의 정상 판정을 먼저 수행하고,
        /// 실패했을 때만 유예 창과 대상의 이동 직전 위치로 재검사한다.
        /// 재검사도 동일한 <paramref name="reachabilityCheck"/> 규칙을 사용하므로
        /// 사거리 규칙의 이중 구현이 생기지 않는다.
        /// </summary>
        /// <param name="reachabilityCheck">공격자 셀과 대상 셀로 기존 사거리 규칙을 평가하는 함수.</param>
        public static bool IsHitAllowed(
            Vector2Int attackerCell,
            Vector2Int targetCurrentCell,
            TargetMoveRecord targetLastNormalMove,
            int attackerLastNormalMoveTick,
            int attackQueuedTick,
            int currentTick,
            int graceTicks,
            Func<Vector2Int, Vector2Int, bool> reachabilityCheck)
        {
            if (reachabilityCheck == null)
                return false;

            // 1) 현재 위치 기준 기존 판정 우선.
            if (reachabilityCheck(attackerCell, targetCurrentCell))
                return true;

            // 2) 유예 게이트 (graceTicks 0이면 기존 동작과 동일).
            if (!IsGraceWindowOpen(targetLastNormalMove, attackerLastNormalMoveTick, attackQueuedTick, currentTick, graceTicks))
                return false;

            // 3) 대상의 이동 직전 위치 기준 재검사 (같은 규칙, 1회 이동만 소급).
            return reachabilityCheck(attackerCell, targetLastNormalMove.FromCell);
        }
    }
}
