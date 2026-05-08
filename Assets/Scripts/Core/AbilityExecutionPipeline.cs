using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Units;

namespace CheckmateRPG.Core
{
    public readonly record struct AbilityEffectIntent(
        string EffectId,
        Guid SourceActorId,
        Guid TargetActorId,
        float Magnitude);

    public readonly record struct AbilityResolveResult(
        bool Succeeded,
        IReadOnlyList<AbilityEffectIntent> EffectIntents);

    public readonly record struct AbilityExecutionContext(
        AbilityActionCommand Action,
        UnitBrain Actor,
        IReadOnlyList<UnitBrain> Targets);

    public sealed class AbilityExecutionPipeline
    {
        public bool TryExecute(
            AbilityActionCommand action,
            UnitBrain actor,
            IReadOnlyDictionary<Guid, UnitBrain> unitsById)
        {
            if (!Validate(action, actor, unitsById, out AbilityExecutionContext context))
                return false;

            Commit(context);
            AbilityResolveResult result = Resolve(context);
            ApplyEffects(context, result, unitsById);
            PostProcess(context, result);
            return result.Succeeded;
        }

        private static bool Validate(
            AbilityActionCommand action,
            UnitBrain actor,
            IReadOnlyDictionary<Guid, UnitBrain> unitsById,
            out AbilityExecutionContext context)
        {
            context = default;
            if (action == null || actor == null || actor.IsDead || unitsById == null)
                return false;

            var targets = new List<UnitBrain>(action.TargetIds.Count);
            for (int i = 0; i < action.TargetIds.Count; i++)
            {
                Guid targetActorId = action.TargetIds[i];
                if (targetActorId == Guid.Empty)
                    return false;
                if (!unitsById.TryGetValue(targetActorId, out UnitBrain target) || target.IsDead)
                    return false;

                targets.Add(target);
            }

            context = new AbilityExecutionContext(action, actor, targets);
            return true;
        }

        private static void Commit(AbilityExecutionContext context)
        {
            _ = context;
        }

        private static AbilityResolveResult Resolve(AbilityExecutionContext context)
        {
            _ = context;
            IReadOnlyList<AbilityEffectIntent> effectIntents = Array.Empty<AbilityEffectIntent>();
            return new AbilityResolveResult(true, effectIntents);
        }

        private static void ApplyEffects(
            AbilityExecutionContext context,
            AbilityResolveResult result,
            IReadOnlyDictionary<Guid, UnitBrain> unitsById)
        {
            _ = context;
            _ = unitsById;

            if (!result.Succeeded)
                return;

            for (int i = 0; i < result.EffectIntents.Count; i++)
            {
                AbilityEffectIntent intent = result.EffectIntents[i];
                _ = intent;
            }
        }

        private static void PostProcess(AbilityExecutionContext context, AbilityResolveResult result)
        {
            _ = context;
            _ = result;
        }
    }
}
