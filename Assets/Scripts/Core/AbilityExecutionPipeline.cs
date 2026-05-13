using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Actions.Resolvers;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public readonly record struct AbilityQueueRequest(
        UnitBrain Actor,
        AbilityDefinition Definition,
        AbilityRuntimeState RuntimeState,
        IReadOnlyList<Guid> TargetIds,
        int CurrentTick,
        Func<AbilityActionCommand, bool> QueueAction,
        IReadOnlyDictionary<Guid, UnitBrain> UnitsById);

    public readonly record struct AbilityQueueResult(
        bool Succeeded,
        AbilityActionCommand Action);

    public readonly record struct AbilityResolveRequest(
        AbilityActionCommand Action,
        UnitBrain Actor,
        AbilityDefinition Definition,
        AbilityRuntimeState RuntimeState,
        IReadOnlyDictionary<Guid, UnitBrain> UnitsById,
        int CurrentTick);

    public readonly record struct AbilityResolveResult(
        bool Succeeded,
        IReadOnlyList<AbilityEffectIntent> EffectIntents);

    public readonly record struct AbilityEffectIntent(
        string EffectId,
        Guid SourceActorId,
        Guid TargetActorId,
        int DurationTicks,
        int TickInterval,
        int InitialTickIn,
        int StackCount,
        float Magnitude);

    public sealed class AbilityExecutionPipeline : IAbilityExecutor
    {
        private static readonly ActionDefinition AbilityActionDefinition =
            new(InterruptPriority.Normal, InterruptWindow.CastingInterruptible, true, true);

        public bool TryQueueAbility(AbilityQueueRequest request, out AbilityQueueResult result)
        {
            result = default;
            if (!Validate(request))
                return false;
            if (!ActionQueue(request, out AbilityActionCommand action))
                return false;
            result = new AbilityQueueResult(true, action);
            return true;
        }

        public ActionResolutionResult ResolveAbility(AbilityResolveRequest request)
        {
            if (!Validate(request))
                return ActionResolutionResult.Failed();

            AbilityResolveResult resolveResult = Resolve(request);
            if (!resolveResult.Succeeded)
                return ActionResolutionResult.Failed();

            IReadOnlyList<IRuntimeMutation> effectMutations = EffectApply(request, resolveResult);
            IReadOnlyList<IRuntimeMutation> stateMutations = PostProcessResolveMutations(request, resolveResult);
            IReadOnlyList<IRuntimeMutation> mutations = MergeMutations(effectMutations, stateMutations);
            IReadOnlyList<IGameEvent> events = PostProcessResolve(request, resolveResult);
            return new ActionResolutionResult(true, mutations, events);
        }

        private static bool Validate(AbilityQueueRequest request)
        {
            if (request.Actor == null ||
                request.Actor.IsDead ||
                request.Definition == null ||
                request.RuntimeState == null ||
                request.QueueAction == null ||
                request.UnitsById == null)
            {
                return false;
            }

            if (request.CurrentTick < request.RuntimeState.CooldownEndTick)
                return false;
            if (request.RuntimeState.PendingActionId.HasValue)
                return false;

            return ValidateTargets(
                request.Definition.TargetingRule,
                request.Actor.ActorId,
                request.TargetIds,
                request.UnitsById);
        }

        private static bool Validate(AbilityResolveRequest request)
        {
            if (request.Action == null ||
                request.Actor == null ||
                request.Actor.IsDead ||
                request.Definition == null ||
                request.UnitsById == null)
            {
                return false;
            }

            return ValidateTargets(
                request.Definition.TargetingRule,
                request.Actor.ActorId,
                request.Action.TargetIds,
                request.UnitsById);
        }

        private static bool ActionQueue(AbilityQueueRequest request, out AbilityActionCommand action)
        {
            action = new AbilityActionCommand(
                request.Actor.ActorId,
                request.Definition.name,
                request.TargetIds ?? Array.Empty<Guid>(),
                request.CurrentTick + 1,
                request.Definition.CastSpeed,
                definition: AbilityActionDefinition);

            return request.QueueAction(action);
        }

        private static AbilityResolveResult Resolve(AbilityResolveRequest request)
        {
            List<AbilityEffectIntent> intents = new();
            IReadOnlyList<Guid> targetIds = request.Action.TargetIds ?? Array.Empty<Guid>();
            IReadOnlyList<AbilityEffectDefinition> effectList = (IReadOnlyList<AbilityEffectDefinition>)request.Definition.EffectList ?? Array.Empty<AbilityEffectDefinition>();

            for (int i = 0; i < effectList.Count; i++)
            {
                AbilityEffectDefinition effect = effectList[i];
                if (effect == null || string.IsNullOrWhiteSpace(effect.EffectId))
                    continue;

                if (effect.ApplyToCaster)
                {
                    intents.Add(ToIntent(effect, request.Actor.ActorId, request.Actor.ActorId));
                    continue;
                }

                for (int t = 0; t < targetIds.Count; t++)
                {
                    Guid targetId = targetIds[t];
                    if (targetId == Guid.Empty)
                        continue;
                    intents.Add(ToIntent(effect, request.Actor.ActorId, targetId));
                }
            }

            return new AbilityResolveResult(true, intents);
        }

        private static IReadOnlyList<IRuntimeMutation> EffectApply(AbilityResolveRequest request, AbilityResolveResult resolveResult)
        {
            if (!resolveResult.Succeeded || resolveResult.EffectIntents.Count == 0)
                return Array.Empty<IRuntimeMutation>();

            var mutations = new List<IRuntimeMutation>(resolveResult.EffectIntents.Count);
            for (int i = 0; i < resolveResult.EffectIntents.Count; i++)
            {
                AbilityEffectIntent intent = resolveResult.EffectIntents[i];
                mutations.Add(new ApplyEffectMutation(
                    SeededRandomProvider.Shared.NextGuid(),
                    intent.EffectId,
                    intent.SourceActorId,
                    intent.TargetActorId,
                    intent.DurationTicks,
                    intent.TickInterval,
                    intent.InitialTickIn,
                    intent.StackCount,
                    intent.Magnitude,
                    Context: new MutationContext(
                        request.Action.ResolveTick,
                        request.Action.ActionId,
                        intent.TargetActorId,
                        nameof(ApplyEffectMutation))));
            }

            return mutations;
        }

        private static IReadOnlyList<IRuntimeMutation> PostProcessResolveMutations(
            AbilityResolveRequest request,
            AbilityResolveResult resolveResult)
        {
            if (!resolveResult.Succeeded || request.Action == null)
                return Array.Empty<IRuntimeMutation>();

            return new IRuntimeMutation[]
            {
                new AbilityActionCompleteMutation(
                    SeededRandomProvider.Shared.NextGuid(),
                    request.Action.ActorId,
                    request.Action.ActionId,
                    request.Action.AbilityId,
                    request.CurrentTick,
                    Context: new MutationContext(
                        request.Action.ResolveTick,
                        request.Action.ActionId,
                        request.Action.ActorId,
                        nameof(AbilityActionCompleteMutation)))
            };
        }

        private static IReadOnlyList<IGameEvent> PostProcessResolve(
            AbilityResolveRequest request,
            AbilityResolveResult resolveResult)
        {
            int primaryTargetCount = request.Action.TargetIds?.Count ?? 0;
            AbilityActionResolvedEvent resolvedEvent = new(
                new AbilityActionResolvedPayload(
                    request.Action.ActionId,
                    request.Action.ActorId,
                    request.Action.AbilityId,
                    primaryTargetCount,
                    resolveResult.EffectIntents.Count,
                    resolveResult.Succeeded),
                request.Action.ActorId.ToString("N"),
                request.Action.ActionId.ToString("N"));

            return new IGameEvent[] { resolvedEvent };
        }

        private static IReadOnlyList<IRuntimeMutation> MergeMutations(
            IReadOnlyList<IRuntimeMutation> first,
            IReadOnlyList<IRuntimeMutation> second)
        {
            int firstCount = first?.Count ?? 0;
            int secondCount = second?.Count ?? 0;
            if (firstCount == 0)
                return second ?? Array.Empty<IRuntimeMutation>();
            if (secondCount == 0)
                return first;

            var merged = new List<IRuntimeMutation>(firstCount + secondCount);
            for (int i = 0; i < firstCount; i++)
            {
                if (first[i] != null)
                    merged.Add(first[i]);
            }

            for (int i = 0; i < secondCount; i++)
            {
                if (second[i] != null)
                    merged.Add(second[i]);
            }

            return merged;
        }

        private static bool ValidateTargets(
            AbilityTargetingRule rule,
            Guid actorId,
            IReadOnlyList<Guid> targetIds,
            IReadOnlyDictionary<Guid, UnitBrain> unitsById)
        {
            targetIds ??= Array.Empty<Guid>();
            switch (rule)
            {
                case AbilityTargetingRule.None:
                    if (targetIds.Count != 0)
                        return false;
                    break;
                case AbilityTargetingRule.Self:
                    if (targetIds.Count != 1 || targetIds[0] != actorId)
                        return false;
                    break;
                case AbilityTargetingRule.SingleTarget:
                    if (targetIds.Count != 1)
                        return false;
                    break;
                case AbilityTargetingRule.MultiTarget:
                    if (targetIds.Count == 0)
                        return false;
                    break;
            }

            for (int i = 0; i < targetIds.Count; i++)
            {
                Guid targetId = targetIds[i];
                if (targetId == Guid.Empty)
                    return false;
                if (!unitsById.TryGetValue(targetId, out UnitBrain target) || target == null || target.IsDead)
                    return false;
            }

            return true;
        }

        private static AbilityEffectIntent ToIntent(AbilityEffectDefinition effect, Guid sourceId, Guid targetId)
        {
            int interval = Mathf.Max(1, effect.TickInterval);
            return new AbilityEffectIntent(
                effect.EffectId,
                sourceId,
                targetId,
                Mathf.Max(1, effect.DurationTicks),
                interval,
                Mathf.Clamp(effect.InitialTickIn, 1, interval),
                Mathf.Max(1, effect.StackCount),
                Mathf.Max(0f, effect.Magnitude));
        }
    }
}
