using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Processors;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public sealed class ReactionSystem
    {
        private const int MaxReactionEventDepth = 16;
        private const int MaxReactionDepth = 12;
        private const int MaxReactionsPerChain = 128;
        private const string UnknownReactionId = "<unknown-reaction>";

        private readonly IEventBus _eventBus;
        private readonly MutationCommitService _mutationCommitService;
        private readonly Func<IReadOnlySimulationRuntime> _runtimeProvider;
        private readonly ReactionDepthGuard _reactionDepthGuard;
        private readonly List<RegisteredReactionTrigger> _registeredTriggers = new();
        private readonly Dictionary<Guid, int> _executedReactionCountByChain = new();
        private readonly Dictionary<Guid, HashSet<string>> _eventSignatureHistoryByChain = new();
        private readonly Dictionary<Guid, int> _activeDispatchDepthByChain = new();
        private readonly Stack<string> _reactionStack = new();

        private int _nextRegistrationOrder;
        private bool _isAttached;

        public ReactionSystem(
            IEventBus eventBus,
            MutationCommitService mutationCommitService,
            Func<IReadOnlySimulationRuntime> runtimeProvider)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _mutationCommitService = mutationCommitService ?? throw new ArgumentNullException(nameof(mutationCommitService));
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            _reactionDepthGuard = new ReactionDepthGuard(MaxReactionDepth);
        }

        public void Attach()
        {
            if (_isAttached)
                return;

            _eventBus.Subscribe<DamageAppliedEvent>(HandleTriggerEvent);
            _eventBus.Subscribe<MoveCompletedEvent>(HandleTriggerEvent);
            _eventBus.Subscribe<AbilityActionResolvedEvent>(HandleTriggerEvent);
            _eventBus.Subscribe<UnitKilledEvent>(HandleTriggerEvent);
            _isAttached = true;
        }

        public void Detach()
        {
            if (!_isAttached)
                return;

            _eventBus.Unsubscribe<DamageAppliedEvent>(HandleTriggerEvent);
            _eventBus.Unsubscribe<MoveCompletedEvent>(HandleTriggerEvent);
            _eventBus.Unsubscribe<AbilityActionResolvedEvent>(HandleTriggerEvent);
            _eventBus.Unsubscribe<UnitKilledEvent>(HandleTriggerEvent);
            _isAttached = false;
        }

        public void Register(IReactionTrigger trigger)
        {
            if (trigger == null)
                throw new ArgumentNullException(nameof(trigger));

            if (_registeredTriggers.Exists(entry => ReferenceEquals(entry.Trigger, trigger)))
                return;

            _registeredTriggers.Add(new RegisteredReactionTrigger(_nextRegistrationOrder++, trigger));
        }

        public void Unregister(IReactionTrigger trigger)
        {
            if (trigger == null)
                throw new ArgumentNullException(nameof(trigger));

            _registeredTriggers.RemoveAll(entry => ReferenceEquals(entry.Trigger, trigger));
        }

        private void HandleTriggerEvent<TEvent>(TEvent gameEvent) where TEvent : class, IGameEvent
        {
            if (gameEvent == null || _registeredTriggers.Count == 0)
                return;

            if (gameEvent is not IResolvableGameEvent resolvable || resolvable.Phase != EventPhase.PostResolve)
                return;

            if (resolvable.EventDepth > MaxReactionEventDepth)
            {
                Debug.LogWarning($"[ReactionSystem] Reaction skipped for {typeof(TEvent).Name}: EventDepth({resolvable.EventDepth}) exceeds {MaxReactionEventDepth}.");
                return;
            }

            Guid reactionChainId = ResolveReactionChainId(resolvable);
            EnterChainDispatchScope(reactionChainId);
            try
            {
                if (IsRecursiveChainEvent(resolvable, reactionChainId))
                    return;

                IReadOnlySimulationRuntime runtime = _runtimeProvider();
                if (runtime == null)
                    return;

                int parentReactionDepth = _reactionStack.Count;
                int nextReactionDepth = parentReactionDepth + 1;
                if (!_reactionDepthGuard.IsDepthAllowed(nextReactionDepth, typeof(TEvent).Name))
                    return;

                string parentReactionId = parentReactionDepth > 0 ? _reactionStack.Peek() : string.Empty;
                var reactionContext = new ReactionContext(
                    nextReactionDepth,
                    parentReactionId,
                    gameEvent,
                    runtime.CurrentTick,
                    reactionChainId);
                var evaluationContext = new ReactionEvaluationContext(runtime.CurrentTick, runtime, gameEvent, reactionContext);
                List<PendingReactionExecution> pendingExecutions = BuildPendingExecutions(gameEvent, evaluationContext);
                if (pendingExecutions.Count == 0)
                    return;

                pendingExecutions.Sort(ReactionOrderingComparer.Default);
                ExecuteReactions(reactionChainId, pendingExecutions);
            }
            finally
            {
                ExitChainDispatchScope(reactionChainId);
            }
        }

        private Guid ResolveReactionChainId(IResolvableGameEvent resolvable)
        {
            return resolvable.ReactionChainId != Guid.Empty
                ? resolvable.ReactionChainId
                : Guid.NewGuid();
        }

        private void EnterChainDispatchScope(Guid reactionChainId)
        {
            if (_activeDispatchDepthByChain.TryGetValue(reactionChainId, out int depth))
            {
                _activeDispatchDepthByChain[reactionChainId] = depth + 1;
                return;
            }

            _activeDispatchDepthByChain[reactionChainId] = 1;
        }

        private void ExitChainDispatchScope(Guid reactionChainId)
        {
            if (!_activeDispatchDepthByChain.TryGetValue(reactionChainId, out int depth))
                return;

            if (depth > 1)
            {
                _activeDispatchDepthByChain[reactionChainId] = depth - 1;
                return;
            }

            _activeDispatchDepthByChain.Remove(reactionChainId);
            _executedReactionCountByChain.Remove(reactionChainId);
            _eventSignatureHistoryByChain.Remove(reactionChainId);
        }

        private bool IsRecursiveChainEvent(IResolvableGameEvent resolvableEvent, Guid reactionChainId)
        {
            if (!_eventSignatureHistoryByChain.TryGetValue(reactionChainId, out HashSet<string> signatures))
            {
                signatures = new HashSet<string>(StringComparer.Ordinal);
                _eventSignatureHistoryByChain[reactionChainId] = signatures;
            }

            string signature = BuildEventRecursionSignature(resolvableEvent);
            if (signatures.Add(signature))
                return false;

            Debug.LogWarning(
                $"[ReactionSystem] Recursive reaction event blocked. Chain={reactionChainId:N} Signature={signature}.");
            return true;
        }

        private static string BuildEventRecursionSignature(IResolvableGameEvent gameEvent)
        {
            return string.Concat(
                gameEvent.GetType().FullName ?? gameEvent.GetType().Name,
                "|",
                gameEvent.Source ?? string.Empty,
                "|",
                gameEvent.Target ?? string.Empty,
                "|",
                gameEvent.Category,
                "|",
                gameEvent.Phase);
        }

        private int GetExecutedReactionCount(Guid reactionChainId)
        {
            return _executedReactionCountByChain.TryGetValue(reactionChainId, out int count)
                ? count
                : 0;
        }

        private void IncrementExecutedReactionCount(Guid reactionChainId)
        {
            _executedReactionCountByChain[reactionChainId] = GetExecutedReactionCount(reactionChainId) + 1;
        }

        private void ExecuteReactions(Guid reactionChainId, IReadOnlyList<PendingReactionExecution> pendingExecutions)
        {
            for (int i = 0; i < pendingExecutions.Count; i++)
            {
                if (GetExecutedReactionCount(reactionChainId) >= MaxReactionsPerChain)
                {
                    Debug.LogWarning($"[ReactionSystem] MaxReactionsPerChain({MaxReactionsPerChain}) reached. Remaining reactions skipped.");
                    return;
                }

                PendingReactionExecution execution = pendingExecutions[i];
                IncrementExecutedReactionCount(reactionChainId);
                ExecuteSingleReaction(execution);
            }
        }

        private void ExecuteSingleReaction(PendingReactionExecution execution)
        {
            string reactionId = string.IsNullOrWhiteSpace(execution.ReactionId)
                ? UnknownReactionId
                : execution.ReactionId;
            _reactionStack.Push(reactionId);
            try
            {
                ReactionExecutionPlan plan = execution.Plan;
                if (plan.Mutations != null && plan.Mutations.Count > 0)
                {
                    var queue = new MutationQueue();
                    for (int i = 0; i < plan.Mutations.Count; i++)
                    {
                        IRuntimeMutation mutation = plan.Mutations[i];
                        if (mutation == null)
                            continue;
                        queue.Enqueue(mutation, plan.SourceActionSpeedLevel, int.MaxValue);
                    }

                    if (queue.Count > 0)
                    {
                        MutationCommitResult commitResult = _mutationCommitService.Commit(queue);
                        PublishEvents(commitResult.StagedEvents);
                    }
                }

                PublishEvents(plan.Events);
            }
            finally
            {
                _reactionStack.Pop();
            }
        }

        private List<PendingReactionExecution> BuildPendingExecutions(
            IGameEvent triggerEvent,
            ReactionEvaluationContext context)
        {
            var pending = new List<PendingReactionExecution>();
            for (int i = 0; i < _registeredTriggers.Count; i++)
            {
                RegisteredReactionTrigger registered = _registeredTriggers[i];
                IReactionTrigger trigger = registered.Trigger;
                if (trigger == null || !trigger.Supports(triggerEvent))
                    continue;

                if (!trigger.TryBuildReaction(triggerEvent, context, out ReactionExecutionPlan plan))
                    continue;

                string reactionId = string.IsNullOrWhiteSpace(plan.ReactionId) ? trigger.ReactionId : plan.ReactionId;
                if (string.IsNullOrWhiteSpace(reactionId))
                    reactionId = trigger.GetType().FullName ?? nameof(IReactionTrigger);

                pending.Add(new PendingReactionExecution(
                    registered.RegistrationOrder,
                    reactionId,
                    plan.SourceActionSpeedLevel,
                    plan));
            }

            return pending;
        }

        private void PublishEvents(IReadOnlyList<IGameEvent> events)
        {
            if (events == null || events.Count == 0)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null)
                    _eventBus.Publish(events[i]);
            }
        }

        private readonly record struct RegisteredReactionTrigger(int RegistrationOrder, IReactionTrigger Trigger);
    }
}
