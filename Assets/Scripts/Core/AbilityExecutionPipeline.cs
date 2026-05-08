using System.Collections.Generic;
using CheckmateRPG.Units;

namespace CheckmateRPG.Core
{
    public readonly record struct AbilityEffectIntent(
        string EffectId,
        string SourceActorId,
        string TargetActorId,
        float Magnitude);

    public readonly record struct AbilityResolveResult(
        bool Succeeded,
        IReadOnlyList<AbilityEffectIntent> EffectIntents);

    public readonly record struct AbilityExecutionContext(
        AbilityAction Action,
        UnitBrain Actor,
        IReadOnlyList<UnitBrain> Targets);

    /// <summary>
    /// Skeleton pipeline for ability execution.
    /// Stage order: Validate -> Commit -> Resolve -> ApplyEffects -> PostProcess.
    /// Resolve must not directly mutate runtime HP/SP or status state.
    /// </summary>
    public sealed class AbilityExecutionPipeline
    {
        public bool TryExecute(
            AbilityAction action,
            UnitBrain actor,
            IReadOnlyDictionary<string, UnitBrain> unitsById)
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
            AbilityAction action,
            UnitBrain actor,
            IReadOnlyDictionary<string, UnitBrain> unitsById,
            out AbilityExecutionContext context)
        {
            context = default;
            if (action == null || actor == null || actor.IsDead || unitsById == null)
                return false;

            var targets = new List<UnitBrain>(action.Targets.Count);
            for (int i = 0; i < action.Targets.Count; i++)
            {
                string targetActorId = action.Targets[i];
                if (string.IsNullOrWhiteSpace(targetActorId))
                    continue;

                if (!unitsById.TryGetValue(targetActorId, out UnitBrain target) || target == null || target.IsDead)
                    continue;

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
            IReadOnlyList<AbilityEffectIntent> effectIntents = System.Array.Empty<AbilityEffectIntent>();
            return new AbilityResolveResult(true, effectIntents);
        }

        private static void ApplyEffects(
            AbilityExecutionContext context,
            AbilityResolveResult result,
            IReadOnlyDictionary<string, UnitBrain> unitsById)
        {
            _ = context;
            _ = unitsById;

            if (!result.Succeeded || result.EffectIntents == null)
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
