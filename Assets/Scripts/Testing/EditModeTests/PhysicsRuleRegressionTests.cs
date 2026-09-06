using System;
using System.Reflection;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Processors;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    /// <summary>
    /// M2 회귀: 물리 엔진 2.0의 넉백/당기기 수식을 검증한다 (GDD Part 2 §2).
    ///  - 넉백 거리 = 힘 − 실효 무게, 비틀거림 시 무게 임시 −1(보스 면역), 하한 0.
    ///  - 당기기(Grab)는 소스 쪽 지배축 방향으로 끌어당기고, 비틀거림 대상에게 치명타 취약 효과를 부여한다.
    ///
    /// Stagger 상태는 StatusEffectComponent.HasStatus가 ActionRuntimeController 싱글턴을 요구하므로
    /// EditMode에서 주입할 수 없다. Stagger/보스 분기는 순수 규칙 PhysicsRules.ComputeKnockbackDistance로
    /// 직접 검증하고, 프로세서 수준 테스트는 Stagger 없는 경로(방향·거리·거부)를 검증한다.
    /// </summary>
    [TestFixture]
    public sealed class PhysicsRuleRegressionTests
    {
        private GameObject _gridObject;
        private readonly System.Collections.Generic.List<UnityEngine.Object> _testObjects = new System.Collections.Generic.List<UnityEngine.Object>();

        [OneTimeSetUp]
        public void SetUpGrid()
        {
            if (GridSystem.Instance == null)
            {
                _gridObject = new GameObject("PhysicsRuleTestGrid");
                GridSystem grid = _gridObject.AddComponent<GridSystem>();
                grid.Initialise(); // 편집 모드에서는 AddComponent가 Awake를 호출하지 않는다.
            }
        }

        [OneTimeTearDown]
        public void TearDownGrid()
        {
            if (GridSystem.Instance != null)
            {
                for (int x = 0; x < GridSystem.GridWidth; x++)
                    for (int y = 0; y < GridSystem.GridHeight; y++)
                        GridSystem.Instance.ClearCell(new Vector2Int(x, y));
            }

            if (_gridObject != null)
                UnityEngine.Object.DestroyImmediate(_gridObject);
        }

        [TearDown]
        public void DestroyTrackedObjects()
        {
            foreach (UnityEngine.Object obj in _testObjects)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }

            _testObjects.Clear();

            if (GridSystem.Instance != null)
            {
                for (int x = 0; x < GridSystem.GridWidth; x++)
                    for (int y = 0; y < GridSystem.GridHeight; y++)
                        GridSystem.Instance.ClearCell(new Vector2Int(x, y));
            }
        }

        private UnitData CreateData(int weight, bool isBoss)
        {
            var data = ScriptableObject.CreateInstance<UnitData>();
            data.Weight = weight;
            data.IsBoss = isBoss;
            _testObjects.Add(data);
            return data;
        }

        private UnitBrain CreateUnit(UnitData data, Vector2Int cell)
        {
            var go = new GameObject("PhysicsRuleTestUnit");
            _testObjects.Add(go);

            var brain = go.AddComponent<UnitBrain>();
            // 편집 모드에서는 Awake가 호출되지 않으므로 컴포넌트 와이어링을 명시 실행한다.
            typeof(UnitBrain)
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(brain, null);

            brain.Prepare(data, cell);
            brain.Movement.Initialise(data, cell);
            brain.StatusEffects.Initialise(data);
            return brain;
        }

        private PhysicsMutationProcessor CreateProcessor(Func<Guid, UnitBrain> lookup)
        {
            return new PhysicsMutationProcessor(lookup, new SimulationRuntime(currentTick: 0));
        }

        // ─── 순수 규칙: 넉백 거리 수식 ────────────────────────────────────────────

        [Test]
        public void KnockbackDistance_EqualsForceMinusWeight()
        {
            Assert.AreEqual(2, PhysicsRules.ComputeKnockbackDistance(force: 3, weight: 1, staggered: false, isBoss: false));
            Assert.AreEqual(1, PhysicsRules.ComputeKnockbackDistance(force: 1, weight: 0, staggered: false, isBoss: false));
            Assert.AreEqual(0, PhysicsRules.ComputeKnockbackDistance(force: 2, weight: 2, staggered: false, isBoss: false));
        }

        [Test]
        public void KnockbackDistance_NeverNegative()
        {
            Assert.AreEqual(0, PhysicsRules.ComputeKnockbackDistance(force: 1, weight: 4, staggered: false, isBoss: false));
            Assert.AreEqual(0, PhysicsRules.ComputeKnockbackDistance(force: 0, weight: 0, staggered: false, isBoss: false));
        }

        [Test]
        public void KnockbackDistance_StaggerReducesEffectiveWeight()
        {
            // 비틀거림 대상은 무게 1 감소 → 밀려나는 거리 1 증가
            Assert.AreEqual(2, PhysicsRules.ComputeKnockbackDistance(force: 2, weight: 1, staggered: true, isBoss: false));
            Assert.AreEqual(1, PhysicsRules.ComputeKnockbackDistance(force: 1, weight: 1, staggered: true, isBoss: false));
        }

        [Test]
        public void KnockbackDistance_BossIgnoresStaggerWeightReduction()
        {
            Assert.AreEqual(0, PhysicsRules.ComputeKnockbackDistance(force: 1, weight: 1, staggered: true, isBoss: true));
            Assert.AreEqual(1, PhysicsRules.ComputeKnockbackDistance(force: 2, weight: 1, staggered: true, isBoss: true));
        }

        [Test]
        public void KnockbackDistance_StaggerWeightFloorsAtZero()
        {
            Assert.AreEqual(1, PhysicsRules.ComputeKnockbackDistance(force: 1, weight: 0, staggered: true, isBoss: false));
        }

        [Test]
        public void KnockbackDistance_ClampsWeightAboveFour()
        {
            Assert.AreEqual(0, PhysicsRules.ComputeKnockbackDistance(force: 4, weight: 9, staggered: false, isBoss: false));
        }

        // ─── 프로세서: 넉백 (비-스태거 경로) ─────────────────────────────────────

        [Test]
        public void Processor_KnockbackMovesTargetByForceMinusWeight()
        {
            var data = CreateData(weight: 1, isBoss: false);
            var origin = new Vector2Int(3, 3);
            UnitBrain target = CreateUnit(data, origin);
            Guid targetId = Guid.NewGuid();

            var processor = CreateProcessor(id => id == targetId ? target : null);
            var mutation = new KnockbackMutation(
                Guid.NewGuid(), targetId, new Vector2Int(1, 0), Force: 3, Context: new MutationContext(5, Guid.Empty, targetId, "Test"));

            PhysicsProcessResult result = processor.ApplyWithGeneratedMutations(mutation);

            Assert.AreEqual(1, result.GeneratedMutations.Count, "넉백은 이동 뮤테이션 1개를 생성해야 한다.");
            var move = (MovementMutation)result.GeneratedMutations[0];
            Assert.AreEqual(new Vector2Int(5, 3), move.To, "거리는 힘(3) − 무게(1) = 2칸이어야 한다.");
        }

        [Test]
        public void Processor_KnockbackRejectedWhenForceLteWeight()
        {
            var data = CreateData(weight: 2, isBoss: false);
            UnitBrain target = CreateUnit(data, new Vector2Int(3, 3));
            Guid targetId = Guid.NewGuid();

            var processor = CreateProcessor(id => id == targetId ? target : null);
            var mutation = new KnockbackMutation(
                Guid.NewGuid(), targetId, new Vector2Int(1, 0), Force: 1, Context: new MutationContext(5, Guid.Empty, targetId, "Test"));

            PhysicsProcessResult result = processor.ApplyWithGeneratedMutations(mutation);

            Assert.AreEqual(0, result.GeneratedMutations.Count, "힘이 무게 이하이면 밀리지 않는다.");
        }

        [Test]
        public void Processor_KnockbackPreservesMovementReasonForSplat()
        {
            var data = CreateData(weight: 0, isBoss: false);
            UnitBrain target = CreateUnit(data, new Vector2Int(3, 3));
            Guid targetId = Guid.NewGuid();

            var processor = CreateProcessor(id => id == targetId ? target : null);
            var mutation = new KnockbackMutation(
                Guid.NewGuid(), targetId, new Vector2Int(0, 1), Force: 1, Context: new MutationContext(5, Guid.Empty, targetId, "Test"));

            PhysicsProcessResult result = processor.ApplyWithGeneratedMutations(mutation);

            var move = (MovementMutation)result.GeneratedMutations[0];
            Assert.AreEqual("Knockback", move.Context.MutationReason, "Splat 트리거 판별을 위해 넉백 이동은 'Knockback' 사유여야 한다.");
        }

        // ─── 프로세서: 당기기 (Grab) ─────────────────────────────────────────────

        [Test]
        public void Processor_GrabPullsTowardSourceOnDominantAxis()
        {
            var targetData = CreateData(weight: 1, isBoss: false);
            var sourceData = CreateData(weight: 1, isBoss: false);
            UnitBrain target = CreateUnit(targetData, new Vector2Int(6, 4));
            UnitBrain source = CreateUnit(sourceData, new Vector2Int(3, 4));
            Guid targetId = Guid.NewGuid();

            var processor = CreateProcessor(id => id == targetId ? target : (id == source.ActorId ? source : null));
            var mutation = new GrabMutation(
                Guid.NewGuid(), targetId, source.ActorId, Force: 2, Context: new MutationContext(5, Guid.Empty, targetId, "Test"));

            PhysicsProcessResult result = processor.ApplyWithGeneratedMutations(mutation);

            Assert.GreaterOrEqual(result.GeneratedMutations.Count, 1, "당기기는 연계 넉백 뮤테이션을 생성해야 한다.");
            var knockback = FindKnockback(result);
            Assert.IsNotNull(knockback, "당기기는 소스 방향 넉백 뮤테이션으로 연계된다.");
            Assert.AreEqual(new Vector2Int(-1, 0), knockback.Value.Direction, "소스 쪽(왼쪽)으로 끌어당겨야 한다.");
        }

        [Test]
        public void Processor_GrabDiagonalResolvesToSingleAxis()
        {
            var targetData = CreateData(weight: 1, isBoss: false);
            var sourceData = CreateData(weight: 1, isBoss: false);
            UnitBrain target = CreateUnit(targetData, new Vector2Int(6, 6));
            UnitBrain source = CreateUnit(sourceData, new Vector2Int(4, 5));
            Guid targetId = Guid.NewGuid();

            var processor = CreateProcessor(id => id == targetId ? target : (id == source.ActorId ? source : null));
            var mutation = new GrabMutation(
                Guid.NewGuid(), targetId, source.ActorId, Force: 2, Context: new MutationContext(5, Guid.Empty, targetId, "Test"));

            PhysicsProcessResult result = processor.ApplyWithGeneratedMutations(mutation);

            var knockback = FindKnockback(result);
            Assert.IsNotNull(knockback, "당기기 연계 넉백이 생성되어야 한다.");
            Vector2Int direction = knockback.Value.Direction;
            Assert.IsTrue(direction.x == 0 || direction.y == 0, $"당기기 방향은 단일 축이어야 한다: {direction}");
        }

        [Test]
        public void Processor_GrabWithoutStaggerGrantsNoVulnerabilityEffect()
        {
            var targetData = CreateData(weight: 1, isBoss: false);
            var sourceData = CreateData(weight: 1, isBoss: false);
            UnitBrain target = CreateUnit(targetData, new Vector2Int(6, 4));
            UnitBrain source = CreateUnit(sourceData, new Vector2Int(3, 4));
            Guid targetId = Guid.NewGuid();

            var processor = CreateProcessor(id => id == targetId ? target : (id == source.ActorId ? source : null));
            var mutation = new GrabMutation(
                Guid.NewGuid(), targetId, source.ActorId, Force: 2, Context: new MutationContext(5, Guid.Empty, targetId, "Test"));

            PhysicsProcessResult result = processor.ApplyWithGeneratedMutations(mutation);

            foreach (IRuntimeMutation generated in result.GeneratedMutations)
            {
                if (generated is ApplyEffectMutation effect)
                    Assert.AreNotEqual("GrabVulnerability", effect.EffectId, "비틀거림이 없으면 치명타 취약 효과를 부여하지 않는다.");
            }
        }

        private static KnockbackMutation? FindKnockback(PhysicsProcessResult result)
        {
            foreach (IRuntimeMutation generated in result.GeneratedMutations)
            {
                if (generated is KnockbackMutation knockback)
                    return knockback;
            }

            return null;
        }
    }
}
