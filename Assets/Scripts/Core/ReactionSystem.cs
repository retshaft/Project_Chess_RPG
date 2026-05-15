using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Processors;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Core.Simulation.Validation;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public sealed class ReactionSystem
    {
        private const int MaxReactionEventDepth = 16;
        // Milestone 13-1 enforcement: cap chained trigger->reaction recursion depth to prevent runaway loops.
        private const int MaxReactionDepth = 5;
        private const int MaxReactionsPerChain = 128;
        private const string UnknownReactionId = "<unknown-reaction>";

        private readonly IEventBus _eventBus;
        private readonly MutationCommitService _mutationCommitService;
        private readonly Func<IReadOnlySimulationRuntime> _runtimeProvider;
        private readonly Func<RuntimeValidationSystem> _runtimeValidationProvider;
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
            Func<IReadOnlySimulationRuntime> runtimeProvider,
            Func<RuntimeValidationSystem> runtimeValidationProvider = null)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _mutationCommitService = mutationCommitService ?? throw new ArgumentNullException(nameof(mutationCommitService));
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            _runtimeValidationProvider = runtimeValidationProvider;
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

                int currentReactionDepth = _reactionStack.Count;
                int nextReactionDepth = currentReactionDepth + 1;
                if (!TryValidateReactionDepth(
                        reactionChainId,
                        nextReactionDepth,
                        typeof(TEvent).Name,
                        runtime.CurrentTick))
                {
                    return;
                }

                string parentReactionId = currentReactionDepth > 0 ? _reactionStack.Peek() : string.Empty;
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
            if (!IsRecursiveSignature(signatures, signature))
                return false;

            Debug.LogWarning(
                $"[ReactionSystem] Recursive reaction event blocked. Chain={reactionChainId:N} Signature={signature}.");
            return true;
        }

        private static bool IsRecursiveSignature(HashSet<string> signatures, string signature)
        {
            return !signatures.Add(signature);
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
                int nextReactionDepth = _reactionStack.Count + 1;
                if (!TryValidateReactionDepth(
                        reactionChainId,
                        nextReactionDepth,
                        pendingExecutions[i].ReactionId,
                        _runtimeProvider()?.CurrentTick ?? -1))
                {
                    // Enforcement policy: stop the current chain immediately when depth exceeds max.
                    return;
                }

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

        private void ReportDepthExceededValidationIssue(
            Guid reactionChainId,
            int depth,
            string triggerName,
            int tick)
        {
            RuntimeValidationSystem validationSystem = _runtimeValidationProvider != null
                ? _runtimeValidationProvider()
                : null;

            if (validationSystem == null)
                return;

            validationSystem.ReportIssue(new ValidationIssue(
                ValidationSeverity.Warning,
                $"Reaction depth exceeded. Chain={reactionChainId:N} Trigger={triggerName} Depth={depth} Max={_reactionDepthGuard.MaxReactionDepth}.",
                UnitId: null,
                Tick: tick));
        }

        private bool TryValidateReactionDepth(
            Guid reactionChainId,
            int depth,
            string triggerName,
            int tick)
        {
            if (_reactionDepthGuard.IsDepthAllowed(depth, triggerName))
                return true;

            ReportDepthExceededValidationIssue(reactionChainId, depth, triggerName, tick);
            return false;
        }

        private void ExecuteSingleReaction(PendingReactionExecution execution)
        {
            string reactionId = GetSafeReactionId(execution.ReactionId);
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

        private static string GetSafeReactionId(string reactionId)
        {
            return string.IsNullOrWhiteSpace(reactionId)
                ? UnknownReactionId
                : reactionId;
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
