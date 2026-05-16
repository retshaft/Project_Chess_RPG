using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Abilities;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Actions.Resolvers;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Units;
using CheckmateRPG.Grid;
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
        float Magnitude,
        EffectStackPolicy StackPolicy,
        int MaxStackCap);

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

            var mutationQueue = new MutationQueue();
            EnqueueApConsumeMutation(request, mutationQueue);

            int effectMutationCount;
            if (!TryEnqueueRuntimeEffects(request, mutationQueue, out effectMutationCount))
            {
                AbilityResolveResult fallbackResolveResult = Resolve(request);
                if (!fallbackResolveResult.Succeeded)
                    return ActionResolutionResult.Failed();

                IReadOnlyList<IRuntimeMutation> fallbackEffectMutations = EffectApply(request, fallbackResolveResult);
                for (int i = 0; i < fallbackEffectMutations.Count; i++)
                {
                    IRuntimeMutation mutation = fallbackEffectMutations[i];
                    if (mutation != null)
                        mutationQueue.Enqueue(mutation);
                }

                effectMutationCount = fallbackResolveResult.EffectIntents.Count;
            }

            EnqueuePostResolveMutations(request, mutationQueue);
            IReadOnlyList<IGameEvent> events = PostProcessResolve(
                request,
                ResolvePrimaryTargetCount(request),
                effectMutationCount,
                true);
            return new ActionResolutionResult(true, mutationQueue.CreateSnapshot(), events);
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
                    intent.StackPolicy,
                    intent.MaxStackCap,
                    Context: new MutationContext(
                        request.Action.ResolveTick,
                        request.Action.ActionId,
                        intent.TargetActorId,
                        nameof(ApplyEffectMutation))));
            }

            return mutations;
        }

        private static bool TryEnqueueRuntimeEffects(
            AbilityResolveRequest request,
            MutationQueue mutationQueue,
            out int enqueuedEffectMutationCount)
        {
            enqueuedEffectMutationCount = 0;
            if (request.Action == null ||
                request.Action.RuntimeEffects == null ||
                request.Action.RuntimeEffects.Count == 0)
            {
                return false;
            }

            IReadOnlyList<Vector2Int> targetCells = request.Action.TargetCells ?? Array.Empty<Vector2Int>();
            if (targetCells.Count == 0)
                return false;

            foreach (Vector2Int targetCell in targetCells)
            {
                if (!TryGetTargetUnitAtCell(targetCell, out UnitBrain targetUnit))
                    continue;

                foreach (EffectRuntimeState runtimeEffect in request.Action.RuntimeEffects)
                {
                    IRuntimeMutation translated = TranslateRuntimeEffectToMutation(request, runtimeEffect, targetUnit.ActorId);
                    if (translated == null)
                        continue;

                    mutationQueue.Enqueue(translated);
                    enqueuedEffectMutationCount++;
                }
            }

            return true;
        }

        private static bool TryGetTargetUnitAtCell(Vector2Int cell, out UnitBrain targetUnit)
        {
            targetUnit = null;
            GridSystem gridSystem = GridSystem.Instance;
            if (gridSystem == null || !gridSystem.IsValidCell(cell))
                return false;

            GameObject occupant = gridSystem.GetOccupant(cell);
            if (occupant == null || !occupant.TryGetComponent(out UnitBrain occupantUnit))
                return false;
            if (occupantUnit.IsDead || occupantUnit.ActorId == Guid.Empty)
                return false;

            targetUnit = occupantUnit;
            return true;
        }

        private static IRuntimeMutation TranslateRuntimeEffectToMutation(
            AbilityResolveRequest request,
            EffectRuntimeState runtimeEffect,
            Guid targetActorId)
        {
            if (runtimeEffect == null || targetActorId == Guid.Empty)
                return null;

            Guid sourceId = runtimeEffect.SourceId != Guid.Empty
                ? runtimeEffect.SourceId
                : request.Action.ActorId;
            int amount = Mathf.Max(0, Mathf.RoundToInt(runtimeEffect.Magnitude));
            MutationContext context = new(
                request.Action.ResolveTick,
                request.Action.ActionId,
                targetActorId,
                nameof(AbilityActionCommand));

            EffectType effectType = ResolveEffectType(runtimeEffect.EffectId);
            switch (effectType)
            {
                case EffectType.Damage:
                    return new DamageMutation(
                        SeededRandomProvider.Shared.NextGuid(),
                        targetActorId,
                        sourceId,
                        amount,
                        IsCritical: false,
                        Context: context);
                case EffectType.Heal:
                    return new HealMutation(
                        SeededRandomProvider.Shared.NextGuid(),
                        targetActorId,
                        sourceId,
                        amount,
                        Context: context);
                case EffectType.Buff:
                case EffectType.Dot:
                case EffectType.Cc:
                default:
                    return new ApplyEffectMutation(
                        SeededRandomProvider.Shared.NextGuid(),
                        runtimeEffect.EffectId,
                        sourceId,
                        targetActorId,
                        runtimeEffect.RemainingTick,
                        runtimeEffect.TickInterval,
                        runtimeEffect.NextTickIn,
                        runtimeEffect.StackCount,
                        runtimeEffect.Magnitude,
                        runtimeEffect.StackPolicy,
                        runtimeEffect.MaxStackCap,
                        runtimeEffect.TimingPhase,
                        runtimeEffect.ActionSpeedLevel,
                        runtimeEffect.IsReaction,
                        context);
            }
        }

        private static EffectType ResolveEffectType(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
                return EffectType.Buff;

            string[] tokens = effectId.Split(':');
            if (tokens.Length < 2)
                return EffectType.Buff;

            return Enum.TryParse(tokens[1], true, out EffectType parsedType)
                ? parsedType
                : EffectType.Buff;
        }

        private static void EnqueueApConsumeMutation(AbilityResolveRequest request, MutationQueue mutationQueue)
        {
            if (request.Action == null || request.Action.ApCost <= 0 || request.Action.ActorId == Guid.Empty)
                return;

            mutationQueue.Enqueue(new ResourceMutation(
                SeededRandomProvider.Shared.NextGuid(),
                request.Action.ActorId,
                ResourceMutationType.ActionPoint,
                -request.Action.ApCost,
                $"Ability:{request.Action.AbilityId}",
                new MutationContext(
                    request.Action.ResolveTick,
                    request.Action.ActionId,
                    request.Action.ActorId,
                    nameof(ResourceMutation))));
        }

        private static void EnqueuePostResolveMutations(AbilityResolveRequest request, MutationQueue mutationQueue)
        {
            IReadOnlyList<IRuntimeMutation> postResolveMutations = PostProcessResolveMutations(request);
            for (int i = 0; i < postResolveMutations.Count; i++)
            {
                IRuntimeMutation mutation = postResolveMutations[i];
                if (mutation != null)
                    mutationQueue.Enqueue(mutation);
            }
        }

        private static int ResolvePrimaryTargetCount(AbilityResolveRequest request)
        {
            int targetCellCount = request.Action.TargetCells?.Count ?? 0;
            if (targetCellCount > 0)
                return targetCellCount;

            return request.Action.TargetIds?.Count ?? 0;
        }

        private static IReadOnlyList<IRuntimeMutation> PostProcessResolveMutations(AbilityResolveRequest request)
        {
            if (request.Action == null)
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
            return PostProcessResolve(
                request,
                request.Action.TargetIds?.Count ?? 0,
                resolveResult.EffectIntents.Count,
                resolveResult.Succeeded);
        }

        private static IReadOnlyList<IGameEvent> PostProcessResolve(
            AbilityResolveRequest request,
            int primaryTargetCount,
            int effectIntentCount,
            bool succeeded)
        {
            AbilityActionResolvedEvent resolvedEvent = new(
                new AbilityActionResolvedPayload(
                    request.Action.ActionId,
                    request.Action.ActorId,
                    request.Action.AbilityId,
                    primaryTargetCount,
                    effectIntentCount,
                    succeeded),
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
                Mathf.Max(0f, effect.Magnitude),
                effect.StackPolicy,
                effect.MaxStackCap);
        }
    }
}
