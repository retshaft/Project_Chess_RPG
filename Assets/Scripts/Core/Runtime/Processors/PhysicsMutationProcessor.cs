using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class PhysicsMutationProcessor
    {
        private readonly Func<Guid, UnitBrain> _unitLookup;
        private readonly SimulationRuntime _simulationRuntime;

        public PhysicsMutationProcessor(Func<Guid, UnitBrain> unitLookup, SimulationRuntime simulationRuntime)
        {
            _unitLookup = unitLookup ?? throw new ArgumentNullException(nameof(unitLookup));
            _simulationRuntime = simulationRuntime ?? throw new ArgumentNullException(nameof(simulationRuntime));
        }

        public PhysicsProcessResult ApplyWithGeneratedMutations(KnockbackMutation mutation)
        {
            UnitBrain target = _unitLookup(mutation.TargetId);
            if (target == null || target.IsDead || target.Movement == null)
                return PhysicsProcessResult.Empty;

            int effectiveWeight = target.UnitData != null ? target.UnitData.Weight : 0;

            // 비틀거림(Stagger) 체크
            if (target.StatusEffects != null && target.StatusEffects.HasStatus(StatusEffectType.Stagger))
            {
                bool isBoss = target.UnitData != null && target.UnitData.IsBoss;
                if (!isBoss)
                {
                    effectiveWeight = Mathf.Max(0, effectiveWeight - 1);
                }
            }

            int distance = Mathf.Max(0, mutation.Force - effectiveWeight);
            if (distance <= 0)
                return PhysicsProcessResult.Empty;

            Vector2Int origin = target.Movement.GridPosition;
            Vector2Int destination = origin + mutation.Direction * distance;

            MutationContext context = new MutationContext(
                mutation.Context.Tick,
                mutation.Context.SourceAction,
                mutation.Context.TargetRuntime,
                "Knockback" // Splat 트리거를 위해 MovementMutation이 아닌 이유 지정
            );

            MovementMutation moveMut = new MovementMutation(
                SeededRandomProvider.Shared.NextGuid(),
                mutation.TargetId,
                origin,
                destination,
                context,
                UseDirectDestinationResolution: false);

            return new PhysicsProcessResult(
                Array.Empty<IGameEvent>(),
                new IRuntimeMutation[] { moveMut });
        }

        public PhysicsProcessResult ApplyWithGeneratedMutations(GrabMutation mutation)
        {
            UnitBrain target = _unitLookup(mutation.TargetId);
            UnitBrain source = _unitLookup(mutation.SourceId);

            if (target == null || target.IsDead || source == null)
                return PhysicsProcessResult.Empty;

            Vector2Int origin = target.Movement.GridPosition;
            Vector2Int sourcePos = source.Movement.GridPosition;

            Vector2Int delta = sourcePos - origin;
            Vector2Int direction;

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), 0);
            else
                direction = new Vector2Int(0, Mathf.Clamp(delta.y, -1, 1));

            if (direction == Vector2Int.zero)
                return PhysicsProcessResult.Empty;

            var generated = new List<IRuntimeMutation>();

            if (target.StatusEffects != null && target.StatusEffects.HasStatus(StatusEffectType.Stagger))
            {
                // 치명타 취약 상태 부여 (다음 1회 공격 확정 치명타 + 40% 피해)
                // 이를 구현하기 위해 ApplyEffectMutation을 생성 (GrabVulnerabilityEffect)
                generated.Add(new ApplyEffectMutation(
                    MutationId: SeededRandomProvider.Shared.NextGuid(),
                    EffectId: "GrabVulnerability",
                    SourceId: mutation.SourceId,
                    TargetId: mutation.TargetId,
                    DurationTicks: 10, // 임의의 10틱으로 설정
                    TickInterval: 10,
                    InitialTickIn: 10,
                    StackCount: 1,
                    Magnitude: 1.4f,
                    Context: mutation.Context));
            }

            // 그랩 자체도 물리 강제 이동이므로 넉백 뮤테이션으로 연계 (Force는 그대로 적용)
            generated.Add(new KnockbackMutation(
                SeededRandomProvider.Shared.NextGuid(),
                mutation.TargetId,
                direction,
                mutation.Force,
                ApplySplatDamage: true,
                mutation.Context));

            return new PhysicsProcessResult(
                Array.Empty<IGameEvent>(),
                generated);
        }
    }

    public readonly record struct PhysicsProcessResult(
        IReadOnlyList<IGameEvent> Events,
        IReadOnlyList<IRuntimeMutation> GeneratedMutations)
    {
        public static PhysicsProcessResult Empty =>
            new(Array.Empty<IGameEvent>(), Array.Empty<IRuntimeMutation>());
    }
}
