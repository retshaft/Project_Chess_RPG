using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    public sealed class AttackActionResolver : IActionResolver<AttackActionCommand>
    {
        public ActionResolutionResult Resolve(
            AttackActionCommand action,
            IBattleContext battleContext)
        {
            if (action == null || battleContext == null)
                return ActionResolutionResult.Failed();
            if (!battleContext.TryGetUnit(action.ActorId, out BattleUnitSnapshot attacker))
                return ActionResolutionResult.Failed();
            if (!battleContext.TryGetUnit(action.TargetId, out BattleUnitSnapshot target))
                return ActionResolutionResult.Failed();
            if ((attacker.StatusFlags & UnitStatusFlags.Dead) != 0)
                return ActionResolutionResult.Failed();
            if ((attacker.StatusFlags & UnitStatusFlags.AttackLocked) != 0)
                return ActionResolutionResult.Failed();
            if ((target.StatusFlags & UnitStatusFlags.Dead) != 0)
                return ActionResolutionResult.Failed();
            if (!battleContext.IsTargetInAttackRange(action.ActorId, action.TargetId))
                return ActionResolutionResult.Failed();

            // 공격 시전 직전 패시브/버프 연동
            battleContext.NotifyAttackStart(action.ActorId, action.TargetId);

            int attackCount = battleContext.GetAttackCount(action.ActorId);
            float damageRatio = battleContext.GetAttackDamageRatio(action.ActorId);
            float defPenetration = battleContext.GetDefPenetrationRatio(action.ActorId, action.TargetId);
            float damageMultiplier = battleContext.GetAttackDamageMultiplier(action.ActorId);

            int baseDamage = Math.Max(0, action.Damage);
            int singleHitDamage = Mathf.RoundToInt(baseDamage * damageRatio * damageMultiplier);
            int criticalMultiplier = Math.Max(1, battleContext.CriticalDamageMultiplier);
            bool isCritical = action.IsCritical;
            int finalDamage = isCritical ? singleHitDamage * criticalMultiplier : singleHitDamage;

            var mutations = new List<IRuntimeMutation>();
            for (int i = 0; i < attackCount; i++)
            {
                mutations.Add(new DamageMutation(
                    SeededRandomProvider.Shared.NextGuid(),
                    action.TargetId,
                    action.ActorId,
                    finalDamage,
                    isCritical,
                    new MutationContext(
                        action.ResolveTick,
                        action.ActionId,
                        action.TargetId,
                        "BasicAttackDamage"),
                    DamageType: action.DamageType,
                    IsTrueDamage: action.DamageType == DamageType.True,
                    DefPenetrationRatio: defPenetration));

                // 출혈 적중 시 부여
                int bleedStacks = battleContext.GetOnHitEffectCount(action.ActorId, CheckmateRPG.Core.StatusEffectType.Bleed);
                if (bleedStacks > 0)
                {
                    mutations.Add(new ApplyEffectMutation(
                        Guid.NewGuid(),
                        "Bleed",
                        action.ActorId,
                        action.TargetId,
                        DurationTicks: 2,
                        TickInterval: 1,
                        InitialTickIn: 1,
                        StackCount: bleedStacks,
                        Magnitude: 300f,
                        Context: new MutationContext(action.ResolveTick, action.ActionId, action.TargetId, "OnHitBleed")
                    ));
                }
            }

            // 소모 처리
            battleContext.NotifyAttackPerformed(action.ActorId);

            SPMutation spGainMutation = new(
                SeededRandomProvider.Shared.NextGuid(),
                action.ActorId,
                Math.Max(0, action.SPGain),
                new MutationContext(
                    action.ResolveTick,
                    action.ActionId,
                    action.ActorId,
                    nameof(SPMutation)));
            mutations.Add(spGainMutation);

            AttackActionResolvedEvent attackResolvedEvent = new(
                new AttackActionResolvedPayload(action.ActionId, action.ActorId, action.TargetId, finalDamage * attackCount, isCritical),
                action.ActionId.ToString("N"),
                action.TargetId.ToString("N"));

            return new ActionResolutionResult(
                true,
                mutations,
                new IGameEvent[] { attackResolvedEvent });
        }
    }
}
