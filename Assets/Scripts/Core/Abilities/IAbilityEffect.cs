using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Abilities
{
    public interface IAbilityEffect
    {
        IEnumerable<IRuntimeMutation> CreateMutations(AbilityResolveRequest request);
    }

    [Serializable]
    public class ApplyBuffAbilityEffect : IAbilityEffect
    {
        [Tooltip("부여할 버프/스탯 프로필 ID")]
        public string StatModifierProfileId;
        
        [Tooltip("해당 레벨에서 특정 스탯 수치만 덮어씌울 경우 추가")]
        public List<CheckmateRPG.Core.StatModifiers.StatModifierOverride> ValueOverrides = new();

        [Tooltip("시전자에게 부여할지 여부")]
        public bool ApplyToCaster = false;

        public IEnumerable<IRuntimeMutation> CreateMutations(AbilityResolveRequest request)
        {
            var mutations = new List<IRuntimeMutation>();
            if (string.IsNullOrWhiteSpace(StatModifierProfileId))
                return mutations;

            // 시전자에게 부여
            if (ApplyToCaster)
            {
                mutations.Add(CreateMutation(request, request.Actor.ActorId));
            }
            // 타겟들에게 부여
            else if (request.Action.TargetIds != null)
            {
                foreach (var targetId in request.Action.TargetIds)
                {
                    if (targetId != Guid.Empty)
                        mutations.Add(CreateMutation(request, targetId));
                }
            }

            return mutations;
        }

        private IRuntimeMutation CreateMutation(AbilityResolveRequest request, Guid targetId)
        {
            return new ApplyEffectMutation(
                SeededRandomProvider.Shared.NextGuid(),
                StatModifierProfileId, // EffectSystem에서 EffectId로 사용됨
                request.Actor.ActorId,
                targetId,
                DurationTicks: 10, // 임시 (기존 로직은 EffectSystem 설정 따름)
                TickInterval: 1,
                InitialTickIn: 1,
                StackCount: 1,
                Magnitude: 1f,
                Context: new MutationContext(request.CurrentTick, request.Action.ActionId, targetId, "ApplyBuffAbilityEffect"),
                StatOverrides: ValueOverrides?.Count > 0 ? ValueOverrides : null
            );
        }
    }

    [Serializable]
    public class ApplyStatusAbilityEffect : IAbilityEffect
    {
        public string EffectId;
        public int DurationTicks = 3;
        public int StackCount = 1;
        public float Magnitude = 1f;

        [Tooltip("해당 레벨에서의 지속 시간 (0이면 원본 사용)")]
        public int OverrideDurationTicks = 0;

        [Tooltip("해당 레벨에서의 틱당 대미지 크기 (0이면 원본 사용)")]
        public float OverrideMagnitude = 0f;

        public bool ApplyToCaster = false;

        public IEnumerable<IRuntimeMutation> CreateMutations(AbilityResolveRequest request)
        {
            var mutations = new List<IRuntimeMutation>();
            if (string.IsNullOrWhiteSpace(EffectId)) return mutations;

            if (ApplyToCaster)
            {
                mutations.Add(CreateMutation(request, request.Actor.ActorId));
            }
            else if (request.Action.TargetIds != null)
            {
                foreach (var targetId in request.Action.TargetIds)
                {
                    if (targetId != Guid.Empty)
                        mutations.Add(CreateMutation(request, targetId));
                }
            }
            return mutations;
        }

        private IRuntimeMutation CreateMutation(AbilityResolveRequest request, Guid targetId)
        {
            return new ApplyEffectMutation(
                SeededRandomProvider.Shared.NextGuid(),
                EffectId,
                request.Actor.ActorId,
                targetId,
                DurationTicks: OverrideDurationTicks > 0 ? OverrideDurationTicks : DurationTicks,
                TickInterval: 1,
                InitialTickIn: 1,
                StackCount,
                Magnitude: OverrideMagnitude > 0f ? OverrideMagnitude : Magnitude,
                Context: new MutationContext(request.CurrentTick, request.Action.ActionId, targetId, "ApplyStatusAbilityEffect")
            );
        }
    }

    [Serializable]
    public class DealAreaDamageAbilityEffect : IAbilityEffect
    {
        public AreaTargetShape Shape = AreaTargetShape.SelfAttackRange;
        public float DamageRatio = 1f;

        public IEnumerable<IRuntimeMutation> CreateMutations(AbilityResolveRequest request)
        {
            var mutations = new List<IRuntimeMutation>();

            // 시전자의 공격력을 가져옴
            int baseDamage = 0;
            float defPen = 0f;
            if (ActionRuntimeController.Instance.TryGetUnitBrain(request.Actor.ActorId, out var attackerBrain))
            {
                if (attackerBrain.UnitData != null)
                {
                    baseDamage = Mathf.RoundToInt(attackerBrain.UnitData.AttackDamage * DamageRatio);
                }
                var modComp = attackerBrain.GetComponent<CheckmateRPG.Core.StatModifiers.UnitStatModifierComponent>();
                if (modComp != null)
                {
                    defPen = modComp.GetDefPenetration();
                    baseDamage = Mathf.RoundToInt(baseDamage * modComp.GetAttackDamageMultiplier());
                }
            }

            var targetIds = request.Action.TargetIds ?? Array.Empty<Guid>();

            foreach (var targetId in targetIds)
            {
                if (targetId == Guid.Empty) continue;
                
                mutations.Add(new DamageMutation(
                    SeededRandomProvider.Shared.NextGuid(),
                    targetId,
                    request.Actor.ActorId,
                    baseDamage,
                    IsCritical: false,
                    new MutationContext(request.CurrentTick, request.Action.ActionId, targetId, "AreaDamage"),
                    DamageType: DamageType.Physical,
                    IsTrueDamage: false,
                    DefPenetrationRatio: defPen
                ));
            }

            return mutations;
        }
    }

    [Serializable]
    public class DealDamageAbilityEffect : IAbilityEffect
    {
        public float DamageRatio = 1f;
        public DamageType DamageType = DamageType.Physical;

        public IEnumerable<IRuntimeMutation> CreateMutations(AbilityResolveRequest request)
        {
            var mutations = new List<IRuntimeMutation>();

            int baseDamage = 0;
            float defPen = 0f;
            if (ActionRuntimeController.Instance.TryGetUnitBrain(request.Actor.ActorId, out var attackerBrain))
            {
                if (attackerBrain.UnitData != null)
                {
                    baseDamage = Mathf.RoundToInt(attackerBrain.UnitData.AttackDamage * DamageRatio);
                }
                var modComp = attackerBrain.GetComponent<CheckmateRPG.Core.StatModifiers.UnitStatModifierComponent>();
                if (modComp != null)
                {
                    defPen = modComp.GetDefPenetration();
                    baseDamage = Mathf.RoundToInt(baseDamage * modComp.GetAttackDamageMultiplier());
                }
            }

            var targetIds = request.Action.TargetIds ?? Array.Empty<Guid>();

            foreach (var targetId in targetIds)
            {
                if (targetId == Guid.Empty) continue;
                
                mutations.Add(new DamageMutation(
                    SeededRandomProvider.Shared.NextGuid(),
                    targetId,
                    request.Actor.ActorId,
                    baseDamage,
                    IsCritical: false,
                    new MutationContext(request.CurrentTick, request.Action.ActionId, targetId, "DealDamage"),
                    DamageType: DamageType,
                    IsTrueDamage: false,
                    DefPenetrationRatio: defPen
                ));
            }

            return mutations;
        }
    }
}
