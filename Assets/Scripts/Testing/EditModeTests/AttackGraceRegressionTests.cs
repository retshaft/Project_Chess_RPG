using System;
using System.Collections.Generic;
using System.Linq;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Actions.Resolvers;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Prediction;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Data;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    /// <summary>
    /// 이동 직후 공격 판정 유예(AttackGraceRules) 회귀 테스트.
    /// Docs/Parallel/Handoffs/attack-move-grace.md §5 매트릭스의 자동화 케이스를 고정한다.
    /// </summary>
    [TestFixture]
    public sealed class AttackGraceRegressionTests
    {
        private static readonly Guid TargetId = new Guid("00000000-0000-0000-0000-000000000002");

        // 킹 규칙: 인접 8칸만 사거리. 유예 게이트 로직 자체는 기물 종류와 무관한 공통 경로다.
        private static bool KingReach(Vector2Int attackerCell, Vector2Int targetCell)
        {
            return CombatPatternRules.IsAttackReachable(ChessPieceType.King, false, attackerCell, targetCell, 1);
        }

        private static AttackGraceRules.TargetMoveRecord Move(Vector2Int fromCell, int tick)
        {
            return new AttackGraceRules.TargetMoveRecord(fromCell, tick);
        }

        // ── 순수 판정 (요구사항 §8 케이스 1, 2, 3, 6, 7, 8의 판정부) ──

        [Test]
        public void GraceHit_Allowed_WhenTargetMovedAfterAdmissionWithinWindow()
        {
            // 접수 틱 5 → 대상이 틱 10에 (1,0)→(3,0) 이동 → 타격 판정 틱 12 (유예 5틱 안).
            bool allowed = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(3, 0),
                Move(new Vector2Int(1, 0), 10),
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 5,
                currentTick: 12,
                graceTicks: 5,
                reachabilityCheck: KingReach);

            Assert.IsTrue(allowed, "이동만을 이유로 유효하게 시작한 공격이 무효화되면 안 된다.");
        }

        [Test]
        public void GraceHit_Denied_AfterWindowExpiry()
        {
            bool allowed = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(3, 0),
                Move(new Vector2Int(1, 0), 10),
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 5,
                currentTick: 16,
                graceTicks: 5,
                reachabilityCheck: KingReach);

            Assert.IsFalse(allowed, "유예 만료 후에는 현재 위치의 정상 판정만 사용한다.");
        }

        [Test]
        public void GraceHit_Denied_WhenTargetMovePrecededAdmission()
        {
            // 대상 이동(틱 10)이 공격 접수(틱 12)보다 앞선다 — 과거 위치 공격 금지.
            bool allowed = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(3, 0),
                Move(new Vector2Int(1, 0), 10),
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 12,
                currentTick: 14,
                graceTicks: 5,
                reachabilityCheck: KingReach);

            Assert.IsFalse(allowed, "이동이 끝난 뒤 시작한 공격은 이전 위치를 공격할 수 없다.");
        }

        [Test]
        public void GraceHit_Disabled_WhenGraceTicksZero()
        {
            bool allowed = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(3, 0),
                Move(new Vector2Int(1, 0), 10),
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 5,
                currentTick: 12,
                graceTicks: 0,
                reachabilityCheck: KingReach);

            Assert.IsFalse(allowed, "유예값 0은 기존 동작(사거리 밖 거부)과 동일해야 한다.");
        }

        [Test]
        public void GraceHit_NormalJudgmentTakesPrecedence_WhenTargetStillInRange()
        {
            bool allowed = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                Move(new Vector2Int(2, 0), 10),
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 5,
                currentTick: 40,
                graceTicks: 5,
                reachabilityCheck: KingReach);

            Assert.IsTrue(allowed, "현재 위치가 사거리 안이면 유예와 무관하게 명중한다.");
        }

        [Test]
        public void GraceHit_Denied_WhenAttackerMovedAfterAdmission()
        {
            bool allowed = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(3, 0),
                Move(new Vector2Int(1, 0), 10),
                attackerLastNormalMoveTick: 11,
                attackQueuedTick: 5,
                currentTick: 12,
                graceTicks: 5,
                reachabilityCheck: KingReach);

            Assert.IsFalse(allowed, "실패 원인이 대상 이동만이 아닌 경우(공격자 이동) 유예하지 않는다.");
        }

        [Test]
        public void GraceHit_Denied_WhenNoMoveRecord()
        {
            bool allowed = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(3, 0),
                AttackGraceRules.TargetMoveRecord.Empty,
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 5,
                currentTick: 12,
                graceTicks: 5,
                reachabilityCheck: KingReach);

            Assert.IsFalse(allowed, "관련된 정상 이동 기록이 없으면 유예하지 않는다.");
        }

        [Test]
        public void GraceHit_Denied_WhenMoveTickIsInFuture()
        {
            bool allowed = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(3, 0),
                Move(new Vector2Int(1, 0), 20),
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 5,
                currentTick: 12,
                graceTicks: 5,
                reachabilityCheck: KingReach);

            Assert.IsFalse(allowed, "판정 시점보다 미래의 이동 기록으로 유예할 수 없다.");
        }

        // ── 사거리 규칙 재사용 확인 (재검사가 기존 기물 규칙을 그대로 쓰는지) ──

        [Test]
        public void GraceRecheck_UsesPiecePatternRules_NotPlainDistance()
        {
            // 비숍: (0,0) → 직전 위치 (3,3)은 대각선이라 도달 가능, 현재 위치 (5,1)은 대각선이 아니라 불가.
            bool allowed = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(5, 1),
                Move(new Vector2Int(3, 3), 10),
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 5,
                currentTick: 12,
                graceTicks: 5,
                reachabilityCheck: (attackerCell, targetCell) => CombatPatternRules.IsAttackReachable(
                    ChessPieceType.Bishop, false, attackerCell, targetCell, 5));

            Assert.IsTrue(allowed, "유예 재검사는 기존 기물 사거리 규칙을 그대로 사용해야 한다(직선 거리로 우회 금지).");
        }

        // ── 런타임 기록 (요구사항 §8 케이스 6·7의 데이터부: 점유 불변, 1회 소급, 클론 전파) ──

        [Test]
        public void RecordNormalMove_KeepsOccupancy_AndPropagatesToSnapshotClone()
        {
            SimulationRuntime runtime = BuildRuntime(new Vector2Int(3, 0));

            runtime.RecordNormalMove(TargetId, new Vector2Int(1, 0), 10, OwnershipOwners.MovementMutationProcessor);

            Assert.IsTrue(runtime.TryGetUnit(TargetId, out IReadOnlyUnitRuntimeState recorded));
            Assert.AreEqual(new Vector2Int(1, 0), recorded.LastNormalMoveFromCell);
            Assert.AreEqual(10, recorded.LastNormalMoveTick);
            Assert.IsTrue(runtime.IsOccupied(new Vector2Int(3, 0)), "기록 갱신은 그리드 점유를 변경하지 않는다.");
            Assert.IsFalse(runtime.IsOccupied(new Vector2Int(1, 0)), "이전 타일에 유령 점유가 생기면 안 된다.");

            SimulationRuntime clone = runtime.CreateSnapshot(12);
            Assert.IsTrue(clone.TryGetUnit(TargetId, out IReadOnlyUnitRuntimeState clonedState));
            Assert.AreEqual(new Vector2Int(1, 0), clonedState.LastNormalMoveFromCell);
            Assert.AreEqual(10, clonedState.LastNormalMoveTick);
            Assert.AreEqual(new Vector2Int(3, 0), clonedState.Position);
            Assert.IsTrue(clone.IsOccupied(new Vector2Int(3, 0)));
        }

        [Test]
        public void RecordNormalMove_OverwritesPreviousMove_SoOnlyOneMoveBack()
        {
            SimulationRuntime runtime = BuildRuntime(new Vector2Int(4, 0));

            runtime.RecordNormalMove(TargetId, new Vector2Int(2, 0), 8, OwnershipOwners.MovementMutationProcessor);
            runtime.RecordNormalMove(TargetId, new Vector2Int(3, 0), 12, OwnershipOwners.MovementMutationProcessor);

            Assert.IsTrue(runtime.TryGetUnit(TargetId, out IReadOnlyUnitRuntimeState recorded));
            Assert.AreEqual(new Vector2Int(3, 0), recorded.LastNormalMoveFromCell);
            Assert.AreEqual(12, recorded.LastNormalMoveTick);
        }

        private static SimulationRuntime BuildRuntime(Vector2Int targetCell)
        {
            var states = new Dictionary<Guid, UnitRuntimeState>();
            var targetState = new UnitRuntimeState();
            targetState.SeedBaseline(TargetId, 10, 0, 0, targetCell, null, 0, UnitStatusFlags.None);
            states[TargetId] = targetState;

            // 점유 사전도 실제 상태처럼 시드해야 기록 갱신이 점유를 건드리지 않음을 검증할 수 있다.
            var occupied = new Dictionary<Vector2Int, Guid> { [targetCell] = TargetId };

            return new SimulationRuntime(
                0,
                states,
                new Dictionary<Guid, IActionCommand>(),
                new Dictionary<string, EffectRuntimeState>(),
                occupied);
        }

        // ── 2차 리뷰: 실전-예측 기물 규칙 일치, 강제 이동 무효화, 리졸버 경유 판정 ──

        private static readonly Guid AttackerId = new Guid("00000000-0000-0000-0000-000000000001");
        private static readonly Guid BlockerId = new Guid("00000000-0000-0000-0000-000000000003");

        private static SimulationRuntime BuildOccupiedRuntime(params (Guid id, Vector2Int cell)[] units)
        {
            var states = new Dictionary<Guid, UnitRuntimeState>();
            var occupied = new Dictionary<Vector2Int, Guid>();
            foreach ((Guid id, Vector2Int cell) in units)
            {
                var state = new UnitRuntimeState();
                state.SeedBaseline(id, 10, 0, 0, cell, null, 0, UnitStatusFlags.None);
                states[id] = state;
                occupied[cell] = id;
            }

            return new SimulationRuntime(
                0,
                states,
                new Dictionary<Guid, IActionCommand>(),
                new Dictionary<string, EffectRuntimeState>(),
                occupied);
        }

        private static PredictionBattleContext BuildPieceContext(
            SimulationRuntime runtime,
            Func<Guid, (ChessPieceType piece, bool isEnemy, int range)> attackerSpec,
            int graceTicks = 5,
            bool withPieceLookups = true)
        {
            return new PredictionBattleContext(
                runtime,
                _ => true,
                id => attackerSpec(id).range,
                attackGraceTicksLookup: _ => graceTicks,
                attackPieceTypeLookup: withPieceLookups ? id => attackerSpec(id).piece : null,
                attackIsEnemyLookup: withPieceLookups ? id => attackerSpec(id).isEnemy : null);
        }

        /// <summary>실전 규칙(CombatPatternRules)을 예측과 같은 점유 샘플러로 직접 평가한다.</summary>
        private static bool RealRulesReach(
            SimulationRuntime runtime,
            ChessPieceType pieceType,
            bool isEnemy,
            Vector2Int attackerCell,
            Vector2Int targetCell,
            int range)
        {
            return CombatPatternRules.IsAttackReachable(pieceType, isEnemy, attackerCell, targetCell, range,
                cell =>
                {
                    IReadOnlyList<IReadOnlyUnitRuntimeState> occupants = runtime.GetUnitsAtPosition(cell);
                    return occupants != null && occupants.Count > 0;
                });
        }

        [Test]
        public void Prediction_Bishop_NonDiagonalDenied_LikeRealRules_NormalAndGrace()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(3, 1)));
            PredictionBattleContext context = BuildPieceContext(runtime, id => (ChessPieceType.Bishop, false, 5));

            Assert.IsFalse(context.IsTargetInAttackRange(AttackerId, TargetId), "비숍은 비대각선을 못 맞힌다(체비셰프 폴백이면 허용될 판정).");
            Assert.IsFalse(RealRulesReach(runtime, ChessPieceType.Bishop, false, new Vector2Int(0, 0), new Vector2Int(3, 1), 5), "실전 규칙도 같은 입력에서 거부해야 한다.");

            // 대상이 비대각선 셀에서 다른 비대각선 셀로 정상 이동 → 정상·유예 재검사 모두 거부.
            const string owner = OwnershipOwners.MovementMutationProcessor;
            runtime.SetUnitPosition(TargetId, new Vector2Int(3, 1), owner);
            runtime.RecordNormalMove(TargetId, new Vector2Int(4, 0), 15, owner);

            Assert.IsFalse(context.IsTargetInAttackRangeWithGrace(AttackerId, TargetId, 5, 19), "이전 셀 (4,0)도 비대각선이라 유예 재검사도 거부해야 한다.");

            // 대각선은 허용 — 같은 규칙 함수의 양방향 확인.
            runtime.SetUnitPosition(TargetId, new Vector2Int(3, 3), owner);
            runtime.RecordNormalMove(TargetId, new Vector2Int(4, 4), 15, owner);
            Assert.IsTrue(context.IsTargetInAttackRangeWithGrace(AttackerId, TargetId, 5, 19), "대각선 이전 셀로의 유예 재검사는 기물 규칙대로 허용된다.");
        }

        [Test]
        public void Prediction_Rook_DiagonalDeniedStraightAllowed_PathBlockedLikeRealRules()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(3, 3)));
            PredictionBattleContext context = BuildPieceContext(runtime, id => (ChessPieceType.Rook, false, 5));

            Assert.IsFalse(context.IsTargetInAttackRange(AttackerId, TargetId), "룩은 대각선을 못 맞힌다(체비셰프 폴백이면 허용될 판정).");
            Assert.IsFalse(RealRulesReach(runtime, ChessPieceType.Rook, false, new Vector2Int(0, 0), new Vector2Int(3, 3), 5));

            runtime.SetUnitPosition(TargetId, new Vector2Int(3, 0), OwnershipOwners.MovementMutationProcessor);
            Assert.IsTrue(context.IsTargetInAttackRange(AttackerId, TargetId), "직선 대상은 허용된다.");

            // 경로 중간 점유 — 예측 클론 점유 샘플러가 실전 규칙과 같게 작동하는지.
            SimulationRuntime blocked = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (BlockerId, new Vector2Int(1, 0)),
                (TargetId, new Vector2Int(3, 0)));
            PredictionBattleContext blockedContext = BuildPieceContext(blocked, id => (ChessPieceType.Rook, false, 5));
            Assert.IsFalse(blockedContext.IsTargetInAttackRange(AttackerId, TargetId), "경로가 막히면 예측도 거부해야 한다.");
            Assert.IsFalse(RealRulesReach(blocked, ChessPieceType.Rook, false, new Vector2Int(0, 0), new Vector2Int(3, 0), 5));
        }

        [Test]
        public void Prediction_Knight_LShapeOnly_BeyondChebyshevRange()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(2, 2)),
                (TargetId, new Vector2Int(4, 3)));
            PredictionBattleContext context = BuildPieceContext(runtime, id => (ChessPieceType.Knight, false, 1));

            Assert.IsTrue(context.IsTargetInAttackRange(AttackerId, TargetId), "나이트는 L자 모양이면 사거리 1로도 맞힌다(체비셰프 2 > 1).");
            Assert.IsTrue(RealRulesReach(runtime, ChessPieceType.Knight, false, new Vector2Int(2, 2), new Vector2Int(4, 3), 1));

            runtime.SetUnitPosition(TargetId, new Vector2Int(3, 3), OwnershipOwners.MovementMutationProcessor);
            Assert.IsFalse(context.IsTargetInAttackRange(AttackerId, TargetId), "대각선 인접은 나이트 규칙이 아니다.");
        }

        [Test]
        public void Prediction_Pawn_ForwardDiagonalOnly_RespectsTeamDirection()
        {
            SimulationRuntime allyRuntime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(1, 1)),
                (TargetId, new Vector2Int(2, 2)));
            PredictionBattleContext allyContext = BuildPieceContext(allyRuntime, id => (ChessPieceType.Pawn, false, 1));
            Assert.IsTrue(allyContext.IsTargetInAttackRange(AttackerId, TargetId), "아군 폰은 전방 대각선을 맞힌다.");

            SimulationRuntime allyBack = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(1, 1)),
                (TargetId, new Vector2Int(2, 0)));
            PredictionBattleContext allyBackContext = BuildPieceContext(allyBack, id => (ChessPieceType.Pawn, false, 1));
            Assert.IsFalse(allyBackContext.IsTargetInAttackRange(AttackerId, TargetId), "아군 폰은 후방 대각선을 못 맞힌다.");

            SimulationRuntime enemyRuntime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(1, 1)),
                (TargetId, new Vector2Int(2, 0)));
            PredictionBattleContext enemyContext = BuildPieceContext(enemyRuntime, id => (ChessPieceType.Pawn, true, 1));
            Assert.IsTrue(enemyContext.IsTargetInAttackRange(AttackerId, TargetId), "적 폰은 자기 전방(하단) 대각선을 맞힌다.");

            SimulationRuntime enemyFront = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(1, 1)),
                (TargetId, new Vector2Int(2, 2)));
            PredictionBattleContext enemyFrontContext = BuildPieceContext(enemyFront, id => (ChessPieceType.Pawn, true, 1));
            Assert.IsFalse(enemyFrontContext.IsTargetInAttackRange(AttackerId, TargetId), "적 폰에게 위쪽 대각선은 후방이다.");
        }

        [Test]
        public void Prediction_GraceRecheck_RookUsesPieceRulesNotChebyshev()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(3, 3)));
            const string owner = OwnershipOwners.MovementMutationProcessor;
            // 대상이 직선 셀 (0,3)에서 대각선 셀 (3,3)으로 정상 이동.
            runtime.SetUnitPosition(TargetId, new Vector2Int(3, 3), owner);
            runtime.RecordNormalMove(TargetId, new Vector2Int(0, 3), 15, owner);

            PredictionBattleContext context = BuildPieceContext(runtime, id => (ChessPieceType.Rook, false, 5));
            Assert.IsFalse(context.IsTargetInAttackRange(AttackerId, TargetId), "현재 위치(대각선)는 정상 판정으로 거부.");
            Assert.IsTrue(context.IsTargetInAttackRangeWithGrace(AttackerId, TargetId, 5, 19), "이전 셀(0,3)은 직선이라 유예 재검사가 허용된다 — 재검사도 기물 규칙을 따른다.");

            // 이전 셀로 가는 경로를 막으면 유예 재검사도 거부된다(같은 점유 샘플러).
            SimulationRuntime blocked = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (BlockerId, new Vector2Int(0, 1)),
                (TargetId, new Vector2Int(3, 3)));
            blocked.RecordNormalMove(TargetId, new Vector2Int(0, 3), 15, OwnershipOwners.MovementMutationProcessor);
            PredictionBattleContext blockedContext = BuildPieceContext(blocked, id => (ChessPieceType.Rook, false, 5));
            Assert.IsFalse(blockedContext.IsTargetInAttackRangeWithGrace(AttackerId, TargetId, 5, 19), "재검사 경로가 막히면 유예도 거부된다.");
        }

        [Test]
        public void Prediction_LegacyChebyshevFallback_KeptForHeadlessCallers()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(3, 1)));
            PredictionBattleContext context = BuildPieceContext(runtime, id => (ChessPieceType.Bishop, false, 5), withPieceLookups: false);

            Assert.IsTrue(context.IsTargetInAttackRange(AttackerId, TargetId), "기물 룩업이 없는 헤드리스 호출자는 기존 체비셰프 폴백을 유지한다(호환성).");
        }

        [Test]
        public void Grace_Denied_WhenTargetMoveTickEqualsQueuedTick()
        {
            bool sameTick = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(3, 0),
                Move(new Vector2Int(1, 0), 6),
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 6,
                currentTick: 9,
                graceTicks: 5,
                reachabilityCheck: KingReach);

            Assert.IsFalse(sameTick, "동일 틱의 이동은 접수 이후 이동이 아니므로 유예하지 않는다.");

            bool nextTick = AttackGraceRules.IsHitAllowed(
                new Vector2Int(0, 0), new Vector2Int(3, 0),
                Move(new Vector2Int(1, 0), 7),
                attackerLastNormalMoveTick: -1,
                attackQueuedTick: 6,
                currentTick: 9,
                graceTicks: 5,
                reachabilityCheck: KingReach);

            Assert.IsTrue(nextTick, "접수 다음 틱의 정상 이동은 유예 대상이다.");
        }

        [Test]
        public void ForcedMoveAfterNormalMove_InvalidatesGraceRecord_RealAndPrediction()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(3, 0)));
            const string owner = OwnershipOwners.MovementMutationProcessor;
            runtime.RecordNormalMove(TargetId, new Vector2Int(1, 0), 8, owner);

            // 같은 위치로의 재설정(동기화 중복 호출)은 기록을 유지한다.
            runtime.SetUnitPosition(TargetId, new Vector2Int(3, 0), owner);
            runtime.TryGetUnit(TargetId, out IReadOnlyUnitRuntimeState same);
            Assert.AreEqual(8, same.LastNormalMoveTick, "위치가 바뀌지 않았으면 기록은 유효하다.");

            // 실제 위치 변경(넉백 등 강제 이동의 런타임 반영)은 기록을 무효화한다.
            runtime.SetUnitPosition(TargetId, new Vector2Int(2, 0), owner);
            runtime.TryGetUnit(TargetId, out IReadOnlyUnitRuntimeState moved);
            Assert.AreEqual(-1, moved.LastNormalMoveTick, "강제 이동 후에는 이전 정상 이동 기록이 남으면 안 된다.");

            PredictionBattleContext context = BuildPieceContext(runtime, id => (ChessPieceType.King, false, 1));
            Assert.IsFalse(context.IsTargetInAttackRangeWithGrace(AttackerId, TargetId, 5, 9), "무효화된 기록으로는 이전 칸 유예가 일어나지 않는다.");

            // 이후의 정상 이동은 다시 기록된다(적용 프로세서의 SetPosition 직후 기록과 동일 순서).
            runtime.SetUnitPosition(TargetId, new Vector2Int(3, 0), owner);
            runtime.RecordNormalMove(TargetId, new Vector2Int(2, 0), 9, owner);
            runtime.TryGetUnit(TargetId, out IReadOnlyUnitRuntimeState reRecorded);
            Assert.AreEqual(9, reRecorded.LastNormalMoveTick, "정상 이동은 무효화 뒤에도 정상적으로 재기록된다.");
        }

        [Test]
        public void Resolver_ThroughGraceContext_HitsOriginalTargetOnly_WithUnitOnOldCell()
        {
            // 대상은 (3,3)→(3,1)로 정상 이동했고, 블로커가 대상의 이전 칸 (3,3)에 서 있다.
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(3, 1)),
                (BlockerId, new Vector2Int(3, 3)));
            runtime.RecordNormalMove(TargetId, new Vector2Int(3, 3), 15, OwnershipOwners.MovementMutationProcessor);

            PredictionBattleContext context = BuildPieceContext(runtime, id => (ChessPieceType.Bishop, false, 5));
            AttackActionCommand command = new AttackActionCommand(
                AttackerId, TargetId, 10, DamageType.Physical, false, 5, ActionSpeedTier.Normal);
            command.QueuedTick = 5;

            ActionResolutionResult result = new AttackActionResolver().Resolve(command, context);

            Assert.IsTrue(result.Success, "비숍은 대상의 이전 대각선 셀 기준 유예 명중을 해야 한다(경로 (1,1),(2,2)는 비어 있음).");
            List<DamageMutation> damages = result.RuntimeMutations.OfType<DamageMutation>().ToList();
            Assert.AreEqual(1, damages.Count, "한 번의 공격은 정확히 한 번의 피해 뮤테이션을 만든다.");
            Assert.AreEqual(TargetId, damages[0].TargetId, "피해는 원래 대상 ID로만 간다 — 이전 칸의 다른 유닛은 피해 대상이 아니다.");
        }

        [Test]
        public void Resolver_DeadTarget_FailsWithoutDamageMutations()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(1, 0)));
            runtime.AddUnitStatusFlag(TargetId, UnitStatusFlags.Dead);

            PredictionBattleContext context = BuildPieceContext(runtime, id => (ChessPieceType.King, false, 1));
            AttackActionCommand command = new AttackActionCommand(
                AttackerId, TargetId, 10, DamageType.Physical, false, 5, ActionSpeedTier.Normal);
            command.QueuedTick = 5;

            ActionResolutionResult result = new AttackActionResolver().Resolve(command, context);

            Assert.IsFalse(result.Success, "죽은 대상에는 해석이 실패한다.");
            Assert.AreEqual(0, result.RuntimeMutations.OfType<DamageMutation>().Count(), "사망 대상에게 피해 뮤테이션이 없어야 한다.");
        }

        [Test]
        public void Pipeline_OutOfRangeUnderPieceRules_CancelsLikeRealPreValidation()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(3, 3)));
            PredictionPipeline pipeline = new PredictionPipeline(
                _ => true,
                _ => 5,
                attackGraceTicksLookup: _ => 5,
                attackPieceTypeLookup: _ => ChessPieceType.Rook,
                attackIsEnemyLookup: _ => false);
            AttackActionCommand command = new AttackActionCommand(
                AttackerId, TargetId, 10, DamageType.Physical, false, 5, ActionSpeedTier.Normal);
            command.QueuedTick = 5;

            PredictionResult result = pipeline.Execute(new IActionCommand[] { command }, runtime, 19);

            Assert.AreEqual(0, result.Damages.Count, "룩의 대각선 대상은 예측에서도 피해가 없어야 한다(체비셰프라면 피해가 생겼을 판정).");
            Assert.AreEqual(1, result.Interrupts.Count, "실전 사전검증과 같이 OutOfRange로 취소된다.");
            Assert.AreEqual(ActionCancellationReason.OutOfRange, result.Interrupts[0].Reason);
        }

        [Test]
        public void Pipeline_InRangeStraightLine_ProducesSingleDamage()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(3, 0)));
            PredictionPipeline pipeline = new PredictionPipeline(
                _ => true,
                _ => 5,
                attackGraceTicksLookup: _ => 5,
                attackPieceTypeLookup: _ => ChessPieceType.Rook,
                attackIsEnemyLookup: _ => false);
            AttackActionCommand command = new AttackActionCommand(
                AttackerId, TargetId, 10, DamageType.Physical, false, 5, ActionSpeedTier.Normal);
            command.QueuedTick = 5;

            PredictionResult result = pipeline.Execute(new IActionCommand[] { command }, runtime, 19);

            Assert.AreEqual(1, result.Damages.Count);
            Assert.AreEqual(TargetId, result.Damages[0].TargetId);
            Assert.AreEqual(0, result.Interrupts.Count);
        }

        [Test]
        public void Pipeline_GraceHit_MatchesRealRuleFromTargetMoveRecord()
        {
            SimulationRuntime runtime = BuildOccupiedRuntime(
                (AttackerId, new Vector2Int(0, 0)),
                (TargetId, new Vector2Int(3, 3)));
            runtime.RecordNormalMove(TargetId, new Vector2Int(0, 3), 15, OwnershipOwners.MovementMutationProcessor);

            PredictionPipeline pipeline = new PredictionPipeline(
                _ => true,
                _ => 5,
                attackGraceTicksLookup: _ => 5,
                attackPieceTypeLookup: _ => ChessPieceType.Rook,
                attackIsEnemyLookup: _ => false);
            AttackActionCommand command = new AttackActionCommand(
                AttackerId, TargetId, 10, DamageType.Physical, false, 5, ActionSpeedTier.Normal);
            command.QueuedTick = 5;

            PredictionResult result = pipeline.Execute(new IActionCommand[] { command }, runtime, 19);

            Assert.AreEqual(1, result.Damages.Count, "대상이 직선 셀(0,3)에서 대각선 셀로 이동한 경우 유예 명중은 예측에서도 동일하게 계산된다.");
            Assert.AreEqual(TargetId, result.Damages[0].TargetId);
        }
    }
}
