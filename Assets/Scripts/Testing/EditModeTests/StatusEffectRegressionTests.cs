using System;
using System.Collections.Generic;
using System.Reflection;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Effects.Processors;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Data;
using CheckmateRPG.Units;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    /// <summary>
    /// M3 회귀: 상태 효과의 순수 규칙(스택 합산, 지속시간 연장 등)과 StatusEffectProcessor를 검증한다.
    /// ActionRuntimeController 등 싱글턴을 억지로 띄우지 않으며,
    /// EffectRuntimeState와 EffectSystemContext/Processor의 순수 동작만 확인한다.
    /// </summary>
    [TestFixture]
    public sealed class StatusEffectRegressionTests
    {
        private GameObject _testGo;
        private UnitBrain _testUnit;
        private UnitData _testData;

        [SetUp]
        public void SetUp()
        {
            _testData = ScriptableObject.CreateInstance<UnitData>();
            _testData.MaxHealth = 100f; // 체력 퍼센트 비례 계산 검증을 위해 100으로 고정

            _testGo = new GameObject("StatusEffectTestUnit");
            _testUnit = _testGo.AddComponent<UnitBrain>();

            // UnitBrain의 Awake 호출을 통해 ActorId 할당 등을 수행
            typeof(UnitBrain)
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(_testUnit, null);

            _testUnit.Prepare(_testData, new Vector2Int(0, 0));
            // EditMode에서는 UnitBrain.Start가 실행되지 않으므로, 프로세서가
            // 참조하는 최대 체력/생존 상태를 테스트 데이터로 명시 초기화한다.
            _testUnit.Health.Initialise(_testData);
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null)
                UnityEngine.Object.DestroyImmediate(_testGo);

            if (_testData != null)
                UnityEngine.Object.DestroyImmediate(_testData);
        }

        // ─── 순수 상태 효과 런타임 상태(EffectRuntimeState) 규칙 ─────────────────────────

        [Test]
        public void EffectState_StackCount_IsClampedByMaxStackCap()
        {
            // 의도적으로 MaxStackCap(3)을 초과하는 5스택 부여
            var effect = new EffectRuntimeState(
                effectId: "Burn",
                sourceId: Guid.Empty,
                targetId: Guid.Empty,
                remainingTick: 1,
                stackCount: 5,
                maxStackCap: 3);

            Assert.AreEqual(3, effect.StackCount, "초기화 시 StackCount는 MaxStackCap을 넘을 수 없다.");
        }

        [Test]
        public void EffectState_Refresh_ExtendsDurationAndAddsStacks()
        {
            var effect = new EffectRuntimeState(
                effectId: "Burn",
                sourceId: Guid.Empty,
                targetId: Guid.Empty,
                remainingTick: 2,
                stackCount: 1,
                maxStackCap: 5);

            // Refresh 방식으로 추가 스택 및 남은 틱 부여 (더 긴 지속시간으로 갱신)
            effect.RefreshFromApplication(
                sourceId: Guid.Empty,
                stackToAdd: 2,
                remainingTick: 5,
                tickInterval: 1,
                nextTickIn: 1,
                magnitude: 1f,
                maxStackCap: 5,
                maxApplicationsPerTick: 0,
                ownerName: OwnershipOwners.EffectSystem);

            Assert.AreEqual(3, effect.StackCount, "Refresh는 기존 스택에 새 스택을 더해야 한다.");
            Assert.AreEqual(5, effect.RemainingTick, "Refresh는 남은 틱이 더 클 경우 연장해야 한다.");
        }

        [Test]
        public void EffectState_Replace_OverwritesProperties()
        {
            var effect = new EffectRuntimeState(
                effectId: "Burn",
                sourceId: Guid.Empty,
                targetId: Guid.Empty,
                remainingTick: 2,
                stackCount: 1,
                maxStackCap: 5);

            // Replace 방식으로 덮어쓰기
            effect.ReplaceFromApplication(
                sourceId: Guid.Empty,
                stackCount: 2,
                remainingTick: 5,
                tickInterval: 1,
                nextTickIn: 1,
                magnitude: 2f,
                appliedTick: 0,
                stackPolicy: EffectStackPolicy.Replace,
                maxStackCap: 5,
                maxApplicationsPerTick: 0,
                ownerName: OwnershipOwners.EffectSystem);

            Assert.AreEqual(2, effect.StackCount, "Replace는 스택 수를 새 값으로 덮어써야 한다.");
            Assert.AreEqual(5, effect.RemainingTick, "Replace는 남은 틱을 새 값으로 덮어써야 한다.");
            Assert.AreEqual(2f, effect.Magnitude, "Replace는 Magnitude를 새 값으로 덮어써야 한다.");
        }

        // ─── 프로세서: 도트/효과 틱 (DotEffectProcessor — 프로덕션 등록 경로) ─────────
        // D5 결정(2026-09-11): GDD Part 2 중독 항목("틱당 지속적인 마법 피해")에 따라
        // 프로덕션에 등록된 DotEffectProcessor를 검증 대상으로 고정한다.
        // 비율 사전 {Burn 0.02, Ignite 0.03, Poison 0.02}은 ActionRuntimeController.BuildEffectSystem의
        // 프로덕션 등록값과 동일하게 유지한다.

        [Test]
        public void DotProcessor_Burn_TicksMagicDamageByMagnitude()
        {
            var processor = new DotEffectProcessor(new Dictionary<string, float> { ["Burn"] = 0.02f });
            var context = new EffectSystemContext(id => id == _testUnit.ActorId ? _testUnit : null, null);
            var factory = new EffectMutationFactory();

            // Burn: 최대 체력(100)의 2% × Magnitude(1.5) = 3 데미지
            var effect = new EffectRuntimeState("Burn", Guid.Empty, _testUnit.ActorId, 1, 1, magnitude: 1.5f);
            var mutationCtx = context.CreateMutationContext(effect, 1, "Tick");
            var result = processor.OnTick(context, mutationCtx, factory, effect);

            Assert.AreEqual(1, result.Mutations.Count, "Burn은 틱마다 1개의 데미지 뮤테이션을 생성해야 한다.");
            var damageMut = (DamageMutation)result.Mutations[0];
            Assert.AreEqual(3, damageMut.Amount, "데미지는 100 * 0.02 * 1.5 = 3이어야 한다.");
            Assert.AreEqual(DamageType.Magical, damageMut.DamageType, "Burn 피해는 마법 피해여야 한다.");
        }

        [Test]
        public void DotProcessor_Poison_TicksMagicDamage_ScalesWithStacks()
        {
            var processor = new DotEffectProcessor(new Dictionary<string, float> { ["Poison"] = 0.02f });
            var context = new EffectSystemContext(id => id == _testUnit.ActorId ? _testUnit : null, null);
            var factory = new EffectMutationFactory();

            // Poison: 최대 체력(100)의 2% × 스택(2) × Magnitude(2.5) = 10 데미지
            var effect = new EffectRuntimeState("Poison", Guid.Empty, _testUnit.ActorId, 1, 2, magnitude: 2.5f);
            var mutationCtx = context.CreateMutationContext(effect, 1, "Tick");
            var result = processor.OnTick(context, mutationCtx, factory, effect);

            Assert.AreEqual(1, result.Mutations.Count, "Poison은 틱마다 1개의 데미지 뮤테이션을 생성해야 한다.");
            var damageMut = (DamageMutation)result.Mutations[0];
            Assert.AreEqual(10, damageMut.Amount, "데미지는 100 * 0.02 * 스택2 * 2.5 = 10이어야 한다(스택 배수 반영).");
            Assert.AreEqual(DamageType.Magical, damageMut.DamageType, "GDD Part 2: 중독은 지속 마법 피해다.");
            Assert.IsFalse(damageMut.IsTrueDamage, "중독 피해는 고정 피해가 아니다(GDD에 근거 없음).");
        }

        [Test]
        public void DotProcessor_DeadTarget_GeneratesNoDamage()
        {
            var processor = new DotEffectProcessor(new Dictionary<string, float> { ["Poison"] = 0.02f });
            var context = new EffectSystemContext(id => id == _testUnit.ActorId ? _testUnit : null, null);
            var factory = new EffectMutationFactory();

            _testUnit.Health.ApplyTrueDamage(_testUnit.Health.MaxHealth); // 사망 처리
            var effect = new EffectRuntimeState("Poison", Guid.Empty, _testUnit.ActorId, 1, 1, magnitude: 2.5f);
            var mutationCtx = context.CreateMutationContext(effect, 1, "Tick");
            var result = processor.OnTick(context, mutationCtx, factory, effect);

            Assert.AreEqual(0, result.Mutations.Count, "사망한 대상에게는 도트 피해가 없어야 한다.");
        }

        [Test]
        public void StatusProcessor_BleedAndWound_DoNotGenerateDamageMutationsOnTick()
        {
            // 프로덕션에서 Bleed/Wound는 DotEffectProcessor가 처리하지 않아 StatusEffectProcessor까지
            // 내려가며, 해당 프로세서가 틱 피해를 만들지 않기 때문에 "틱당 무피해"가 최종 동작이다.
            var processor = new StatusEffectProcessor();
            var context = new EffectSystemContext(id => id == _testUnit.ActorId ? _testUnit : null, null);
            var factory = new EffectMutationFactory();

            var bleedEffect = new EffectRuntimeState("Bleed", Guid.Empty, _testUnit.ActorId, 1, 1);
            var bleedCtx = context.CreateMutationContext(bleedEffect, 1, "Tick");
            var bleedResult = processor.OnTick(context, bleedCtx, factory, bleedEffect);
            Assert.AreEqual(0, bleedResult.Mutations.Count, "Bleed는 틱에서 데미지 뮤테이션을 생성하지 않는다.");

            var woundEffect = new EffectRuntimeState("Wound", Guid.Empty, _testUnit.ActorId, 1, 1);
            var woundCtx = context.CreateMutationContext(woundEffect, 1, "Tick");
            var woundResult = processor.OnTick(context, woundCtx, factory, woundEffect);
            Assert.AreEqual(0, woundResult.Mutations.Count, "Wound는 틱에서 데미지 뮤테이션을 생성하지 않는다.");
        }
    }
}
