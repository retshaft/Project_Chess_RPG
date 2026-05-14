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
        private const int MaxReactionsPerChain = 128;

        private readonly IEventBus _eventBus;
        private readonly MutationCommitService _mutationCommitService;
        private readonly Func<IReadOnlySimulationRuntime> _runtimeProvider;
        private readonly List<RegisteredReactionTrigger> _registeredTriggers = new();

        private int _nextRegistrationOrder;
        private int _executedReactionCountInChain;
        private bool _isAttached;

        public ReactionSystem(
            IEventBus eventBus,
            MutationCommitService mutationCommitService,
            Func<IReadOnlySimulationRuntime> runtimeProvider)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _mutationCommitService = mutationCommitService ?? throw new ArgumentNullException(nameof(mutationCommitService));
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
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

            if (resolvable.EventDepth <= 1)
                _executedReactionCountInChain = 0;

            if (resolvable.EventDepth > MaxReactionEventDepth)
            {
                Debug.LogWarning($"[ReactionSystem] Reaction skipped for {typeof(TEvent).Name}: EventDepth({resolvable.EventDepth}) exceeds {MaxReactionEventDepth}.");
                return;
            }

            IReadOnlySimulationRuntime runtime = _runtimeProvider();
            if (runtime == null)
                return;

            var evaluationContext = new ReactionEvaluationContext(runtime.CurrentTick, runtime, gameEvent);
            List<PendingReactionExecution> pendingExecutions = BuildPendingExecutions(gameEvent, evaluationContext);
            if (pendingExecutions.Count == 0)
                return;

            pendingExecutions.Sort(ReactionOrderingComparer.Default);
            ExecuteReactions(pendingExecutions);
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

        private void ExecuteReactions(IReadOnlyList<PendingReactionExecution> pendingExecutions)
        {
            for (int i = 0; i < pendingExecutions.Count; i++)
            {
                if (_executedReactionCountInChain >= MaxReactionsPerChain)
                {
                    Debug.LogWarning($"[ReactionSystem] MaxReactionsPerChain({MaxReactionsPerChain}) reached. Remaining reactions skipped.");
                    return;
                }

                PendingReactionExecution execution = pendingExecutions[i];
                _executedReactionCountInChain++;
                ExecuteSingleReaction(execution);
            }
        }

        private void ExecuteSingleReaction(PendingReactionExecution execution)
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
