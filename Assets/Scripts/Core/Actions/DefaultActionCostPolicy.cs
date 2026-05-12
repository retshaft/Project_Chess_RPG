using CheckmateRPG.Grid;
using UnityEngine;

namespace CheckmateRPG.Core.Actions
{
    public sealed class DefaultActionCostPolicy : IActionCostPolicy
    {
        public ActionCostBreakdown Evaluate(IActionCommand action, ActionCostContext context)
        {
            if (action == null || context.Actor == null)
                return new ActionCostBreakdown(0f, 0, 0, APActionReason.System);

            float multiplier = Mathf.Max(0f, context.Actor.StatusEffects != null ? context.Actor.StatusEffects.ActionCostMultiplier : 1f);

            return action switch
            {
                MoveActionCommand move => new ActionCostBreakdown(
                    Mathf.Max(0f, GetMoveApCost(context.Actor, move) * multiplier),
                    0,
                    0,
                    APActionReason.Move),
                AttackActionCommand => new ActionCostBreakdown(
                    Mathf.Max(0f, GetAttackApCost(context.Actor) * multiplier),
                    0,
                    0,
                    APActionReason.Attack),
                AbilityActionCommand => new ActionCostBreakdown(
                    Mathf.Max(0f, GetAbilityApCost(context.AbilityDefinition) * multiplier),
                    0,
                    Mathf.Max(0, context.AbilityDefinition != null ? context.AbilityDefinition.Cooldown : 0),
                    APActionReason.Skill),
                _ => new ActionCostBreakdown(0f, 0, 0, APActionReason.System)
            };
        }

        private static float GetMoveApCost(CheckmateRPG.Units.UnitBrain actor, MoveActionCommand move)
        {
            float baseCost = actor.UnitData != null ? actor.UnitData.MoveCostAP : 0f;
            float tileMultiplier = 1f;
            if (GridSystem.Instance != null)
                tileMultiplier *= GridSystem.Instance.GetMoveCostMultiplier(move.To);
            return Mathf.Max(0f, baseCost * tileMultiplier);
        }

        private static float GetAttackApCost(CheckmateRPG.Units.UnitBrain actor)
        {
            return actor.UnitData != null ? Mathf.Max(0f, actor.UnitData.AttackCostAP) : 0f;
        }

        private static float GetAbilityApCost(AbilityDefinition definition)
        {
            return definition != null ? Mathf.Max(0f, definition.Cost) : 0f;
        }
    }
}
