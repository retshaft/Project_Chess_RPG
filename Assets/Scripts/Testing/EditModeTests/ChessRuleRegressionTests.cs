using System.Collections.Generic;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
using CheckmateRPG.MovementPatterns;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    /// <summary>
    /// M1 회귀 (MILESTONES 항목: "Pawn~King의 이동·공격 판정과 막힌 경로 처리를 회귀 점검").
    /// 6종 체스 기물의 이동 패턴, 공격 도달 판정(CombatPatternRules), 막힌 경로 처리를
    /// 논리 레벨에서 고정한다. EditMode 전용 테스트이며 Play Mode를 요구하지 않는다.
    ///
    /// 판정 계약:
    ///  - MovePattern.CanMove는 기물 형태, 중간 경로 점유, 보드 경계를 판정한다.
    ///  - 대상 셀의 점유 여부는 호출자(MovementComponent.CanReachCell)가 검사한다.
    ///  - CombatPatternRules.IsAttackReachable은 공격 도달 가능성과 시야 차폐를 판정한다.
    /// </summary>
    [TestFixture]
    public sealed class ChessRuleRegressionTests
    {
        private GameObject _gridObject;
        private bool _ownsGrid;
        private readonly List<Object> _testObjects = new List<Object>();

        [OneTimeSetUp]
        public void SetUpGrid()
        {
            if (GridSystem.Instance == null)
            {
                _gridObject = new GameObject("ChessRuleTestGrid");
                GridSystem grid = _gridObject.AddComponent<GridSystem>();
                grid.Initialise(); // 편집 모드에서는 AddComponent가 Awake를 호출하지 않는다.
                _ownsGrid = true;
            }
        }

        [OneTimeTearDown]
        public void TearDownGrid()
        {
            if (!_ownsGrid)
                return;

            if (GridSystem.Instance != null)
            {
                for (int x = 0; x < GridSystem.GridWidth; x++)
                    for (int y = 0; y < GridSystem.GridHeight; y++)
                        GridSystem.Instance.ClearCell(new Vector2Int(x, y));
            }

            if (_gridObject != null)
                Object.DestroyImmediate(_gridObject);
        }

        [SetUp]
        public void ResetTrackedObjects() => _testObjects.Clear();

        [TearDown]
        public void DestroyTrackedObjects()
        {
            foreach (Object obj in _testObjects)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }

            _testObjects.Clear();

            if (GridSystem.Instance != null)
            {
                for (int x = 0; x < GridSystem.GridWidth; x++)
                    for (int y = 0; y < GridSystem.GridHeight; y++)
                        GridSystem.Instance.ClearCell(new Vector2Int(x, y));
            }
        }

        private GameObject CreateUnit(bool isEnemy = false)
        {
            var unit = new GameObject("ChessRuleTestUnit");
            if (isEnemy)
                unit.AddComponent<TeamComponent>().SetIsEnemy(true);

            _testObjects.Add(unit);
            return unit;
        }

        private void Occupy(Vector2Int cell)
        {
            var blocker = new GameObject("Blocker");
            _testObjects.Add(blocker);
            GridSystem.Instance.SetOccupant(cell, blocker);
        }

        // ─── Knight ───────────────────────────────────────────────────────────────

        [Test]
        public void Knight_AcceptsAllEightLShapedJumps()
        {
            var pattern = new KnightMovePattern();
            var unit = CreateUnit();
            var origin = new Vector2Int(3, 3);
            var targets = new[]
            {
                new Vector2Int(4, 5), new Vector2Int(5, 4), new Vector2Int(5, 2), new Vector2Int(4, 1),
                new Vector2Int(2, 1), new Vector2Int(1, 2), new Vector2Int(1, 4), new Vector2Int(2, 5)
            };

            foreach (Vector2Int target in targets)
                Assert.IsTrue(pattern.CanMove(origin, target, unit), $"Knight는 L자 점프 {origin}->{target}을 허용해야 한다.");
        }

        [Test]
        public void Knight_RejectsNonLShapes()
        {
            var pattern = new KnightMovePattern();
            var unit = CreateUnit();
            var origin = new Vector2Int(3, 3);
            var targets = new[]
            {
                new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 5),
                new Vector2Int(6, 3), new Vector2Int(3, 3), new Vector2Int(2, 6)
            };

            foreach (Vector2Int target in targets)
                Assert.IsFalse(pattern.CanMove(origin, target, unit), $"Knight는 비-L자 이동 {origin}->{target}을 거부해야 한다.");
        }

        [Test]
        public void Knight_RejectsOutOfBoundsTargets()
        {
            var pattern = new KnightMovePattern();
            var unit = CreateUnit();

            Assert.IsFalse(pattern.CanMove(new Vector2Int(0, 0), new Vector2Int(-2, 1), unit), "보드 밖 L자 점프는 거부된다.");
            Assert.IsFalse(pattern.CanMove(new Vector2Int(6, 0), new Vector2Int(8, 1), unit), "보드 오른쪽 밖 L자 점프는 거부된다.");
            Assert.IsFalse(pattern.CanMove(new Vector2Int(0, 0), new Vector2Int(1, -2), unit), "보드 아래 밖 L자 점프는 거부된다.");
        }

        // ─── King ─────────────────────────────────────────────────────────────────

        [Test]
        public void King_AcceptsAllEightAdjacentCells()
        {
            var pattern = new KingMovePattern();
            var unit = CreateUnit();
            var origin = new Vector2Int(3, 3);
            var targets = new[]
            {
                new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(4, 2), new Vector2Int(2, 3),
                new Vector2Int(4, 3), new Vector2Int(2, 4), new Vector2Int(3, 4), new Vector2Int(4, 4)
            };

            foreach (Vector2Int target in targets)
                Assert.IsTrue(pattern.CanMove(origin, target, unit), $"King은 인접 1칸 {origin}->{target}을 허용해야 한다.");
        }

        [Test]
        public void King_RejectsZeroAndTwoStepMoves()
        {
            var pattern = new KingMovePattern();
            var unit = CreateUnit();
            var origin = new Vector2Int(3, 3);

            Assert.IsFalse(pattern.CanMove(origin, origin, unit), "제자리 이동은 거부된다.");
            Assert.IsFalse(pattern.CanMove(origin, new Vector2Int(5, 3), unit), "직선 2칸은 거부된다.");
            Assert.IsFalse(pattern.CanMove(origin, new Vector2Int(5, 5), unit), "대각 2칸은 거부된다.");
            Assert.IsFalse(pattern.CanMove(origin, new Vector2Int(3, 1), unit), "직선 2칸(수직)은 거부된다.");
        }

        [Test]
        public void King_RejectsOutOfBoundsTargets()
        {
            var pattern = new KingMovePattern();
            var unit = CreateUnit();
            var corner = new Vector2Int(0, 0);

            Assert.IsFalse(pattern.CanMove(corner, new Vector2Int(-1, 0), unit), "보드 왼쪽 밖은 거부된다.");
            Assert.IsFalse(pattern.CanMove(corner, new Vector2Int(-1, -1), unit), "보드 왼쪽 아래 밖은 거부된다.");
            Assert.IsFalse(pattern.CanMove(corner, new Vector2Int(0, -1), unit), "보드 아래 밖은 거부된다.");
        }

        // ─── Pawn ─────────────────────────────────────────────────────────────────

        [Test]
        public void PlayerPawn_SingleForwardAlwaysAllowed()
        {
            var pattern = new PawnMovePattern();
            var pawn = CreateUnit();

            Assert.IsTrue(pattern.CanMove(new Vector2Int(3, 1), new Vector2Int(3, 2), pawn), "초기 행의 전진 1칸은 허용된다.");
            Assert.IsTrue(pattern.CanMove(new Vector2Int(3, 4), new Vector2Int(3, 5), pawn), "중반 이후에도 전진 1칸은 허용된다.");
        }

        [Test]
        public void PlayerPawn_DoubleStepOnlyFromInitialRow()
        {
            var pattern = new PawnMovePattern();
            var pawn = CreateUnit();

            Assert.IsTrue(pattern.CanMove(new Vector2Int(3, 0), new Vector2Int(3, 2), pawn), "초기 행(row 0)의 전진 2칸은 허용된다.");
            Assert.IsTrue(pattern.CanMove(new Vector2Int(3, 1), new Vector2Int(3, 3), pawn), "초기 행(row 1)의 전진 2칸은 허용된다.");
            Assert.IsFalse(pattern.CanMove(new Vector2Int(3, 2), new Vector2Int(3, 4), pawn), "초기 행이 아닌 전진 2칸은 거부된다.");
        }

        [Test]
        public void PlayerPawn_DoubleStepBlockedByIntermediateOccupant()
        {
            var pattern = new PawnMovePattern();
            var pawn = CreateUnit();
            Occupy(new Vector2Int(3, 2));

            Assert.IsFalse(pattern.CanMove(new Vector2Int(3, 1), new Vector2Int(3, 3), pawn),
                "중간 칸이 막힌 전진 2칸은 거부된다.");
        }

        [Test]
        public void Pawn_RejectsDiagonalAndBackwardMoves()
        {
            var pattern = new PawnMovePattern();
            var pawn = CreateUnit();

            Assert.IsFalse(pattern.CanMove(new Vector2Int(3, 1), new Vector2Int(4, 2), pawn), "이동으로서의 대각 전진은 거부된다(공격은 별도 판정).");
            Assert.IsFalse(pattern.CanMove(new Vector2Int(3, 1), new Vector2Int(3, 0), pawn), "후진은 거부된다.");
            Assert.IsFalse(pattern.CanMove(new Vector2Int(3, 1), new Vector2Int(5, 3), pawn), "대각 2칸은 거부된다.");
        }

        [Test]
        public void EnemyPawn_MovesInvertedDirection()
        {
            var pattern = new PawnMovePattern();
            var pawn = CreateUnit(isEnemy: true);

            Assert.IsTrue(pattern.CanMove(new Vector2Int(3, 6), new Vector2Int(3, 5), pawn), "적 폰은 아래로 전진한다.");
            Assert.IsTrue(pattern.CanMove(new Vector2Int(3, 6), new Vector2Int(3, 4), pawn), "적 폰 초기 행(row 6)의 전진 2칸은 허용된다.");
            Assert.IsFalse(pattern.CanMove(new Vector2Int(3, 5), new Vector2Int(3, 3), pawn), "적 폰도 초기 행이 아니면 전진 2칸이 거부된다.");
            Assert.IsFalse(pattern.CanMove(new Vector2Int(3, 6), new Vector2Int(3, 7), pawn), "적 폰의 후진(위로)은 거부된다.");
        }

        [Test]
        public void Pawn_RejectsOutOfBoundsTargets()
        {
            var pattern = new PawnMovePattern();
            var enemyPawn = CreateUnit(isEnemy: true);

            Assert.IsFalse(pattern.CanMove(new Vector2Int(3, 0), new Vector2Int(3, -1), enemyPawn),
                "적 폰이 보드 아래 밖으로 전진하는 것은 거부된다.");
        }

        // ─── Sliding (Rook / Bishop / Queen) ──────────────────────────────────────

        [Test]
        public void RookPattern_AcceptsClearOrthogonalSlides()
        {
            var pattern = new SlidingMovePattern(allowOrthogonal: true, allowDiagonal: false);
            var unit = CreateUnit();

            Assert.IsTrue(pattern.CanMove(new Vector2Int(0, 0), new Vector2Int(7, 0), unit), "Rook은 가로 직선 슬라이딩을 허용한다.");
            Assert.IsTrue(pattern.CanMove(new Vector2Int(0, 0), new Vector2Int(0, 7), unit), "Rook은 세로 직선 슬라이딩을 허용한다.");
            Assert.IsTrue(pattern.CanMove(new Vector2Int(7, 7), new Vector2Int(0, 7), unit), "Rook은 반대 방향 슬라이딩도 허용한다.");
        }

        [Test]
        public void RookPattern_RejectsDiagonalAndNonLinearShapes()
        {
            var pattern = new SlidingMovePattern(allowOrthogonal: true, allowDiagonal: false);
            var unit = CreateUnit();
            var origin = new Vector2Int(3, 3);

            Assert.IsFalse(pattern.CanMove(origin, new Vector2Int(5, 5), unit), "Rook은 대각 이동을 거부한다.");
            Assert.IsFalse(pattern.CanMove(origin, new Vector2Int(4, 5), unit), "Rook은 나이트 형태를 거부한다.");
        }

        [Test]
        public void BishopPattern_AcceptsClearDiagonalSlides()
        {
            var pattern = new SlidingMovePattern(allowOrthogonal: false, allowDiagonal: true);
            var unit = CreateUnit();

            Assert.IsTrue(pattern.CanMove(new Vector2Int(0, 0), new Vector2Int(7, 7), unit), "Bishop은 대각 슬라이딩을 허용한다.");
            Assert.IsTrue(pattern.CanMove(new Vector2Int(7, 0), new Vector2Int(0, 7), unit), "Bishop은 역대각 슬라이딩을 허용한다.");
        }

        [Test]
        public void BishopPattern_RejectsOrthogonalSlides()
        {
            var pattern = new SlidingMovePattern(allowOrthogonal: false, allowDiagonal: true);
            var unit = CreateUnit();
            var origin = new Vector2Int(3, 3);

            Assert.IsFalse(pattern.CanMove(origin, new Vector2Int(7, 3), unit), "Bishop은 직선 이동을 거부한다.");
            Assert.IsFalse(pattern.CanMove(origin, new Vector2Int(3, 7), unit), "Bishop은 수직 이동을 거부한다.");
        }

        [Test]
        public void QueenPattern_AcceptsOrthogonalAndDiagonalSlides()
        {
            var pattern = new SlidingMovePattern(allowOrthogonal: true, allowDiagonal: true);
            var unit = CreateUnit();
            var origin = new Vector2Int(0, 0);

            Assert.IsTrue(pattern.CanMove(origin, new Vector2Int(7, 0), unit), "Queen은 직선을 허용한다.");
            Assert.IsTrue(pattern.CanMove(origin, new Vector2Int(7, 7), unit), "Queen은 대각을 허용한다.");
            Assert.IsTrue(pattern.CanMove(origin, new Vector2Int(0, 5), unit), "Queen은 수직을 허용한다.");
        }

        [Test]
        public void QueenPattern_RejectsNonLinearShapes()
        {
            var pattern = new SlidingMovePattern(allowOrthogonal: true, allowDiagonal: true);
            var unit = CreateUnit();

            Assert.IsFalse(pattern.CanMove(new Vector2Int(0, 0), new Vector2Int(1, 2), unit), "Queen은 나이트 형태를 거부한다.");
            Assert.IsFalse(pattern.CanMove(new Vector2Int(0, 0), new Vector2Int(0, 0), unit), "제자리 이동은 거부된다.");
        }

        [Test]
        public void Sliding_RejectsBlockedIntermediatePath()
        {
            var rook = new SlidingMovePattern(allowOrthogonal: true, allowDiagonal: false);
            var bishop = new SlidingMovePattern(allowOrthogonal: false, allowDiagonal: true);
            var unit = CreateUnit();

            Occupy(new Vector2Int(4, 0));
            Occupy(new Vector2Int(2, 2));

            Assert.IsFalse(rook.CanMove(new Vector2Int(0, 0), new Vector2Int(7, 0), unit),
                "중간에 기물이 있으면 직선 슬라이딩은 거부된다(뛰어넘기 금지).");
            Assert.IsFalse(bishop.CanMove(new Vector2Int(0, 0), new Vector2Int(7, 7), unit),
                "중간에 기물이 있으면 대각 슬라이딩은 거부된다.");
        }

        [Test]
        public void Sliding_RejectsOutOfBoundsTargets()
        {
            var pattern = new SlidingMovePattern(allowOrthogonal: true, allowDiagonal: true);
            var unit = CreateUnit();

            Assert.IsFalse(pattern.CanMove(new Vector2Int(0, 0), new Vector2Int(-1, 0), unit), "보드 왼쪽 밖 인접 이동은 거부된다.");
            Assert.IsFalse(pattern.CanMove(new Vector2Int(0, 0), new Vector2Int(0, -1), unit), "보드 아래 밖 인접 이동은 거부된다.");
        }

        // ─── 공격 도달 판정 (CombatPatternRules) ──────────────────────────────────

        [Test]
        public void Attack_PawnOnlyDiagonalForward()
        {
            Assert.IsTrue(CombatPatternRules.IsAttackReachable(ChessPieceType.Pawn, false, new Vector2Int(3, 1), new Vector2Int(4, 2), 1),
                "아군 폰은 오른쪽 앞 대각을 공격한다.");
            Assert.IsTrue(CombatPatternRules.IsAttackReachable(ChessPieceType.Pawn, false, new Vector2Int(3, 1), new Vector2Int(2, 2), 1),
                "아군 폰은 왼쪽 앞 대각을 공격한다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(ChessPieceType.Pawn, false, new Vector2Int(3, 1), new Vector2Int(3, 2), 1),
                "폰은 정면을 공격할 수 없다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(ChessPieceType.Pawn, false, new Vector2Int(3, 1), new Vector2Int(4, 0), 1),
                "폰은 뒤쪽 대각을 공격할 수 없다.");
            Assert.IsTrue(CombatPatternRules.IsAttackReachable(ChessPieceType.Pawn, true, new Vector2Int(3, 6), new Vector2Int(4, 5), 1),
                "적 폰은 아래 대각을 공격한다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(ChessPieceType.Pawn, true, new Vector2Int(3, 6), new Vector2Int(3, 5), 1),
                "적 폰도 정면은 공격할 수 없다.");
        }

        [Test]
        public void Attack_KnightOnlyLShape()
        {
            Assert.IsTrue(CombatPatternRules.IsAttackReachable(ChessPieceType.Knight, false, new Vector2Int(3, 3), new Vector2Int(4, 5), 1),
                "Knight는 L자 형태로 공격한다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(ChessPieceType.Knight, false, new Vector2Int(3, 3), new Vector2Int(3, 6), 7),
                "Knight는 직선 상의 먼 적을 공격할 수 없다(사거리 무관).");
        }

        [Test]
        public void Attack_KingOnlyAdjacent()
        {
            Assert.IsTrue(CombatPatternRules.IsAttackReachable(ChessPieceType.King, false, new Vector2Int(3, 3), new Vector2Int(4, 4), 1),
                "King은 인접 대각을 공격한다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(ChessPieceType.King, false, new Vector2Int(3, 3), new Vector2Int(5, 5), 1),
                "King은 2칸 떨어진 대각을 공격할 수 없다.");
        }

        [Test]
        public void Attack_RookRequiresClearOrthogonalPath()
        {
            Assert.IsTrue(CombatPatternRules.IsAttackReachable(ChessPieceType.Rook, false, new Vector2Int(0, 0), new Vector2Int(5, 0), 1),
                "시야가 막히지 않으면 직선 공격이 가능하다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(
                    ChessPieceType.Rook, false, new Vector2Int(0, 0), new Vector2Int(5, 0), 1,
                    cell => cell == new Vector2Int(2, 0)),
                "중간에 기물이 있으면 직선 공격은 차폐된다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(ChessPieceType.Rook, false, new Vector2Int(0, 0), new Vector2Int(4, 4), 1),
                "Rook은 대각 공격을 할 수 없다.");
        }

        [Test]
        public void Attack_BishopRequiresClearDiagonalPath()
        {
            Assert.IsTrue(CombatPatternRules.IsAttackReachable(ChessPieceType.Bishop, false, new Vector2Int(0, 0), new Vector2Int(5, 5), 1),
                "시야가 막히지 않으면 대각 공격이 가능하다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(
                    ChessPieceType.Bishop, false, new Vector2Int(0, 0), new Vector2Int(5, 5), 1,
                    cell => cell == new Vector2Int(2, 2)),
                "중간에 기물이 있으면 대각 공격은 차폐된다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(ChessPieceType.Bishop, false, new Vector2Int(0, 0), new Vector2Int(5, 0), 1),
                "Bishop은 직선 공격을 할 수 없다.");
        }

        [Test]
        public void Attack_QueenCoversLinearShapesAndBlockedPath()
        {
            Assert.IsTrue(CombatPatternRules.IsAttackReachable(ChessPieceType.Queen, false, new Vector2Int(0, 0), new Vector2Int(5, 5), 1),
                "Queen은 대각 공격이 가능하다.");
            Assert.IsTrue(CombatPatternRules.IsAttackReachable(ChessPieceType.Queen, false, new Vector2Int(0, 0), new Vector2Int(5, 0), 1),
                "Queen은 직선 공격이 가능하다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(ChessPieceType.Queen, false, new Vector2Int(0, 0), new Vector2Int(1, 2), 1),
                "Queen은 나이트 형태를 공격할 수 없다.");
            Assert.IsFalse(CombatPatternRules.IsAttackReachable(
                    ChessPieceType.Queen, false, new Vector2Int(0, 0), new Vector2Int(5, 5), 1,
                    cell => cell == new Vector2Int(3, 3)),
                "중간에 기물이 있으면 Queen의 대각 공격은 차폐된다.");
        }

        // ─── 패턴 팩토리 ─────────────────────────────────────────────────────────

        [Test]
        public void Factory_MapsEveryMovePatternType()
        {
            Assert.IsInstanceOf<PawnMovePattern>(MovePatternFactory.Create(CreateData(MovePatternType.Pawn)));
            Assert.IsInstanceOf<KnightMovePattern>(MovePatternFactory.Create(CreateData(MovePatternType.Knight)));
            Assert.IsInstanceOf<KingMovePattern>(MovePatternFactory.Create(CreateData(MovePatternType.King)));
            Assert.IsInstanceOf<SlidingMovePattern>(MovePatternFactory.Create(CreateData(MovePatternType.SlidingOrthogonal)));
            Assert.IsInstanceOf<SlidingMovePattern>(MovePatternFactory.Create(CreateData(MovePatternType.SlidingDiagonal)));
            Assert.IsInstanceOf<SlidingMovePattern>(MovePatternFactory.Create(CreateData(MovePatternType.SlidingOmni)));
            Assert.IsNull(MovePatternFactory.Create(null), "UnitData가 없으면 패턴도 생성되지 않는다.");
        }

        private UnitData CreateData(MovePatternType movePatternType)
        {
            var data = ScriptableObject.CreateInstance<UnitData>();
            data.MovePattern = movePatternType;
            _testObjects.Add(data);
            return data;
        }
    }
}
