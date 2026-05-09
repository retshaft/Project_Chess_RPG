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
        public bool TryQueueAbility(AbilityQueueRequest request, out AbilityQueueResult result)
        {
            result = default;
            if (!Validate(request))
                return false;
            if (!CostCommit(request))
                return false;
            if (!ActionQueue(request, out AbilityActionCommand action))
                return false;

            PostProcessQueue(request, action);
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

            IReadOnlyList<IRuntimeMutation> mutations = EffectApply(resolveResult);
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

        private static bool CostCommit(AbilityQueueRequest request)
        {
            float cost = Mathf.Max(0f, request.Definition.Cost);
            if (cost <= 0f)
                return true;

            if (APManager.Instance == null)
                return false;

            return APManager.Instance.TrySpend(new ActionPointCost(cost, APActionReason.Skill), out _);
        }

        private static bool ActionQueue(AbilityQueueRequest request, out AbilityActionCommand action)
        {
            action = new AbilityActionCommand(
                request.Actor.ActorId,
                request.Definition.name,
                request.TargetIds ?? Array.Empty<Guid>(),
                request.CurrentTick + 1,
                request.Definition.CastSpeed);

            return request.QueueAction(action);
        }

        private static AbilityResolveResult Resolve(AbilityResolveRequest request)
        {
            List<AbilityEffectIntent> intents = new();
            IReadOnlyList<Guid> targetIds = request.Action.TargetIds ?? Array.Empty<Guid>();
            IReadOnlyList<AbilityEffectDefinition> effectList = request.Definition.EffectList ?? Array.Empty<AbilityEffectDefinition>();

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

        private static IReadOnlyList<IRuntimeMutation> EffectApply(AbilityResolveResult resolveResult)
        {
            if (!resolveResult.Succeeded || resolveResult.EffectIntents.Count == 0)
                return Array.Empty<IRuntimeMutation>();

            var mutations = new List<IRuntimeMutation>(resolveResult.EffectIntents.Count);
            for (int i = 0; i < resolveResult.EffectIntents.Count; i++)
            {
                AbilityEffectIntent intent = resolveResult.EffectIntents[i];
                mutations.Add(new ApplyEffectMutation(
                    Guid.NewGuid(),
                    intent.EffectId,
                    intent.SourceActorId,
                    intent.TargetActorId,
                    intent.DurationTicks,
                    intent.TickInterval,
                    intent.InitialTickIn,
                    intent.StackCount,
                    intent.Magnitude));
            }

            return mutations;
        }

        private static void PostProcessQueue(AbilityQueueRequest request, AbilityActionCommand action)
        {
            AbilityRuntimeState runtimeState = request.RuntimeState;
            runtimeState.AbilityId = request.Definition.name;
            runtimeState.PendingActionId = action.ActionId;
            runtimeState.Locked = true;
            runtimeState.CooldownEndTick = request.CurrentTick + Mathf.Max(0, request.Definition.Cooldown);
            runtimeState.CooldownRemaining = Mathf.Max(0, runtimeState.CooldownEndTick - request.CurrentTick);
            runtimeState.LastCommittedTick = request.CurrentTick;
        }

        private static IReadOnlyList<IGameEvent> PostProcessResolve(
            AbilityResolveRequest request,
            AbilityResolveResult resolveResult)
        {
            if (request.RuntimeState != null && request.RuntimeState.PendingActionId == request.Action.ActionId)
            {
                request.RuntimeState.PendingActionId = null;
                request.RuntimeState.Locked = false;
                request.RuntimeState.CooldownRemaining = Mathf.Max(0, request.RuntimeState.CooldownEndTick - request.CurrentTick);
            }

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
