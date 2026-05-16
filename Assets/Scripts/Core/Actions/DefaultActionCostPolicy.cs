using CheckmateRPG.Grid;
using CheckmateRPG.Core.Runtime.Processors;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core.Actions
{
    public sealed class DefaultActionCostPolicy : IActionCostPolicy
    {
        private static readonly HashSet<string> JumpAbilityTokens = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "jump",
            "leap"
        };

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
                AbilityActionCommand ability => new ActionCostBreakdown(
                    Mathf.Max(0f, GetAbilityApCost(context.Actor, ability, context.AbilityDefinition) * multiplier),
                    0,
                    Mathf.Max(0, context.AbilityDefinition != null ? context.AbilityDefinition.Cooldown : 0),
                    APActionReason.Skill),
                _ => new ActionCostBreakdown(0f, 0, 0, APActionReason.System)
            };
        }

        private static float GetMoveApCost(CheckmateRPG.Units.UnitBrain actor, MoveActionCommand move)
        {
            float baseCost = actor.UnitData != null ? actor.UnitData.MoveCostAP : 0f;
            float tileMultiplier = MovementMutationProcessor.ResolveSwampApMultiplier(move.From, move.To);
            return Mathf.Max(0f, baseCost * tileMultiplier);
        }

        private static float GetAttackApCost(CheckmateRPG.Units.UnitBrain actor)
        {
            return actor.UnitData != null ? Mathf.Max(0f, actor.UnitData.AttackCostAP) : 0f;
        }

        private static float GetAbilityApCost(CheckmateRPG.Units.UnitBrain actor, AbilityActionCommand action, AbilityDefinition definition)
        {
            float baseCost = definition != null ? Mathf.Max(0f, definition.Cost) : 0f;
            if (baseCost <= 0f || actor == null || action == null || !IsJumpAbility(action))
                return baseCost;

            if (action.TargetCells == null || action.TargetCells.Count == 0)
                return baseCost;

            Vector2Int landing = action.TargetCells[0];
            Vector2Int from = actor.Movement != null ? actor.Movement.GridPosition : landing;
            float swampMultiplier = MovementMutationProcessor.ResolveSwampApMultiplier(from, landing, isJumpSkill: true);
            return Mathf.Max(0f, baseCost * swampMultiplier);
        }

        private static bool IsJumpAbility(AbilityActionCommand action)
        {
            string abilityId = action?.AbilityId ?? string.Empty;
            foreach (string token in JumpAbilityTokens)
            {
                if (abilityId.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
