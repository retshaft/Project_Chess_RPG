using System;
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

            int baseDamage = Math.Max(0, action.Damage);
            int criticalMultiplier = Math.Max(1, battleContext.CriticalDamageMultiplier);
            bool isCritical = action.IsCritical;
            int finalDamage = isCritical ? baseDamage * criticalMultiplier : baseDamage;
            int remainingHp = Math.Max(0, target.HP - finalDamage);

            DamageMutation mutation = new(action.TargetId, finalDamage);
            AttackActionResolvedEvent attackResolvedEvent = new(
                new AttackActionResolvedPayload(action.ActionId, action.ActorId, action.TargetId, finalDamage, isCritical),
                action.ActorId.ToString("N"),
                action.TargetId.ToString("N"));
            DamageAppliedEvent damageAppliedEvent = new(
                new DamageAppliedPayload(action.ActionId, action.ActorId, action.TargetId, finalDamage, remainingHp, isCritical),
                action.ActorId.ToString("N"),
                action.TargetId.ToString("N"));

            return new ActionResolutionResult(
                true,
                new IRuntimeMutation[] { mutation },
                new IGameEvent[] { attackResolvedEvent, damageAppliedEvent });
        }
    }
}
