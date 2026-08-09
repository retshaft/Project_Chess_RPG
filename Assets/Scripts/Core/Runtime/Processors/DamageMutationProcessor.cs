using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class DamageMutationProcessor
    {
        private const int HitTakenSPBonus = 10;
        private readonly Func<Guid, UnitBrain> _unitLookup;
        private readonly SimulationRuntime _simulationRuntime;

        public DamageMutationProcessor(Func<Guid, UnitBrain> unitLookup, SimulationRuntime simulationRuntime)
        {
            _unitLookup = unitLookup ?? throw new ArgumentNullException(nameof(unitLookup));
            _simulationRuntime = simulationRuntime ?? throw new ArgumentNullException(nameof(simulationRuntime));
        }

        public DamageProcessResult ApplyWithGeneratedMutations(DamageMutation mutation)
        {
            UnitBrain target = _unitLookup(mutation.TargetId);
            if (target == null || target.Health == null)
                return DamageProcessResult.Empty;

            int amount = Mathf.Max(0, mutation.Amount);

            // 쇄빙(Shatter) 기믹 처리
            if (mutation.DamageType == DamageType.Physical && !mutation.IsTrueDamage)
            {
                string freezeKey = SimulationRuntime.BuildEffectKey(mutation.TargetId, StatusEffectType.Freeze.ToString());
                if (_simulationRuntime.TryGetMutableEffect(freezeKey, out var freezeEffect) && freezeEffect.Lifecycle != EffectLifecycle.Removed)
                {
                    // 빙결 해제 및 방어력 연산을 가정한 막대한 추가 물리 피해
                    freezeEffect.TransitionLifecycle("ShatterSystem", EffectLifecycle.Removed);
                    amount += 50; // TODO: 기획 확정 시 대상의 방어력 스탯을 기반으로 계산식 변경
                    Debug.Log($"[Shatter] {target.name} 빙결이 깨지며 쇄빙 보너스 물리 피해 적용!");
                    // TODO: 쇄빙 파티클 이펙트 스폰 (얼음 파편이 튀는 PC 전용 파티클)
                }
            }

            float beforeHp = target.Health.CurrentHealth;
            float defPenetrationRatio = Mathf.Clamp01(mutation.DefPenetrationRatio);
            if (mutation.IsTrueDamage)
            {
                target.Health.ApplyTrueDamage(amount);
            }
            else
            {
                switch (mutation.DamageType)
                {
                    case DamageType.Physical:
                        target.Health.TakeDamage(amount, defPenetrationRatio);
                        break;
                    case DamageType.Magical:
                        target.Health.ApplyMagicDamage(amount);
                        break;
                    default:
                        target.Health.ApplyTrueDamage(amount);
                        break;
                }
            }

            float afterHp = target.Health.CurrentHealth;
            int actualDamage = Mathf.Max(0, Mathf.RoundToInt(beforeHp - afterHp));
            int actualRemainingHp = Mathf.RoundToInt(afterHp);
            bool isDead = target.Health.IsDead;

            _simulationRuntime.SetUnitHP(mutation.TargetId, actualRemainingHp, OwnershipOwners.DamageMutationProcessor);
            if (isDead)
                _simulationRuntime.AddUnitStatusFlag(mutation.TargetId, UnitStatusFlags.Dead);

            DamageAppliedEvent damageAppliedEvent = new(
                new DamageAppliedPayload(
                    mutation.MutationId,
                    mutation.SourceId,
                    mutation.TargetId,
                    actualDamage,
                    actualRemainingHp,
                    mutation.IsCritical,
                    mutation.DamageType,
                    mutation.IsTrueDamage),
                mutation.MutationId.ToString("N"),
                mutation.TargetId.ToString("N"));

            Debug.Log(
                $"[DamageMutationProcessor] Type={mutation.DamageType}, True={mutation.IsTrueDamage}, " +
                $"Raw={amount}, Applied={actualDamage}, HP:{Mathf.RoundToInt(beforeHp)}->{actualRemainingHp}");

            var mutations = new List<IRuntimeMutation>();
            mutations.Add(new SPMutation(
                SeededRandomProvider.Shared.NextGuid(),
                mutation.TargetId,
                HitTakenSPBonus,
                new MutationContext(
                    mutation.Context.Tick,
                    mutation.Context.SourceAction,
                    mutation.TargetId,
                    nameof(SPMutation))));

            if (isDead && mutation.Context.MutationReason == "BasicAttackDamage")
            {
                UnitBrain attacker = _unitLookup(mutation.SourceId);
                if (attacker != null && !attacker.IsDead && attacker.Movement != null)
                {
                    Vector2Int targetPos = target.Movement.GridPosition;
                    mutations.Add(new MovementMutation(
                        SeededRandomProvider.Shared.NextGuid(),
                        mutation.SourceId,
                        attacker.Movement.GridPosition,
                        targetPos,
                        new MutationContext(
                            mutation.Context.Tick,
                            mutation.Context.SourceAction,
                            mutation.SourceId,
                            "CaptureMove"
                        ),
                        UseDirectDestinationResolution: true // 무조건 사망한 유닛의 위치로 바로 진입
                    ));
                }
            }

            return new DamageProcessResult(
                new IGameEvent[] { damageAppliedEvent },
                mutations);
        }

        public IReadOnlyList<IGameEvent> Apply(DamageMutation mutation)
        {
            return ApplyWithGeneratedMutations(mutation).Events;
        }

        public IReadOnlyList<IGameEvent> Apply(HealMutation mutation)
        {
            UnitBrain target = _unitLookup(mutation.TargetId);
            if (target == null || target.Health == null)
                return Array.Empty<IGameEvent>();

            int amount = Mathf.Max(0, mutation.Amount);
            target.Health.Heal(amount);
            int actualRemainingHp = Mathf.RoundToInt(target.Health.CurrentHealth);
            _simulationRuntime.SetUnitHP(mutation.TargetId, actualRemainingHp, OwnershipOwners.DamageMutationProcessor);
            return Array.Empty<IGameEvent>();
        }

        public IReadOnlyList<IGameEvent> Apply(DeathMutation mutation)
        {
            UnitBrain target = _unitLookup(mutation.TargetId);
            if (target == null)
                return Array.Empty<IGameEvent>();

            int currentTick = mutation.Tick;
            _simulationRuntime.ApplyDeadUnitLifecycle(mutation.TargetId, currentTick, OwnershipOwners.ActionScheduler);
            UnitKilledEvent unitKilledEvent = new(
                new CheckmateRPG.Core.Events.ActionEvents.UnitKilledPayload(mutation.TargetId, mutation.SourceId, currentTick),
                mutation.SourceId.ToString("N"),
                mutation.TargetId.ToString("N"));

            var events = new List<IGameEvent> { unitKilledEvent };
            if (target.UnitData != null && target.UnitData.PieceType == Data.ChessPieceType.King)
            {
                var team = target.GetComponent<CheckmateRPG.Components.TeamComponent>();
                bool isPlayerKing = team != null && team.IsPlayer;
                events.Add(new KingDiedEvent(new KingDiedPayload(isPlayerKing), target.gameObject.name));
            }

            return events;
        }

        public readonly record struct DamageProcessResult(
            IReadOnlyList<IGameEvent> Events,
            IReadOnlyList<IRuntimeMutation> GeneratedMutations)
        {
            public static DamageProcessResult Empty =>
                new(Array.Empty<IGameEvent>(), Array.Empty<IRuntimeMutation>());
        }
    }
}
