using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Events.ActionEvents;

namespace CheckmateRPG.Core.Actions
{
    public sealed class ActionScheduler : IActionScheduler
    {
        private readonly Dictionary<Guid, BaseActionCommand> _activeActions = new();
        private readonly Queue<Guid> _pendingResolveQueue = new();
        private readonly Dictionary<Guid, PendingInterruptRequest> _pendingInterrupts = new();
        private readonly IEventBus _eventBus;
        private readonly InterruptArbitrationService _interruptArbitrationService;
        private readonly Dictionary<Guid, PendingInterruptRequest> _interruptContextByTarget = new();

        public ActionScheduler(IEventBus eventBus = null)
        {
            _eventBus = eventBus;
            _interruptArbitrationService = new InterruptArbitrationService();
        }

        public int CurrentTick { get; private set; }

        public void ScheduleAction(IActionCommand action)
        {
            if (action is not BaseActionCommand command)
                throw new InvalidOperationException($"Unsupported action type '{action?.GetType().Name ?? "null"}'.");
            if (_activeActions.ContainsKey(command.ActionId))
                throw new InvalidOperationException($"Action '{command.ActionId}' is already scheduled.");
            if (command.StartTick < CurrentTick)
                throw new ArgumentOutOfRangeException(nameof(action), "StartTick cannot be in the past.");
            if (command.ResolveTick < command.StartTick)
                throw new ArgumentOutOfRangeException(nameof(action), "ResolveTick must be greater than or equal to StartTick.");
            if (command.RecoveryEndTick < command.ResolveTick)
                throw new ArgumentOutOfRangeException(nameof(action), "RecoveryEndTick must be greater than or equal to ResolveTick.");

            command.MarkQueued(CurrentTick);
            _activeActions.Add(command.ActionId, command);
            PublishLifecycleEvent(command, ActionState.Queued);
        }

        public void AdvanceTick()
        {
            CurrentTick++;

            BaseActionCommand[] snapshot = new BaseActionCommand[_activeActions.Count];
            _activeActions.Values.CopyTo(snapshot, 0);

            // Queued → Casting
            for (int i = 0; i < snapshot.Length; i++)
            {
                BaseActionCommand action = snapshot[i];
                if (action.State == ActionState.Queued && CurrentTick >= action.StartTick)
                    Transition(action, ActionState.Casting);
            }

            // Casting → Resolving
            for (int i = 0; i < snapshot.Length; i++)
            {
                BaseActionCommand action = snapshot[i];
                if (action.State == ActionState.Casting && CurrentTick >= action.ResolveTick)
                {
                    Transition(action, ActionState.Resolving);
                    _pendingResolveQueue.Enqueue(action.ActionId);
                }
            }

            // Resolving → Recovery / Completed
            for (int i = 0; i < snapshot.Length; i++)
            {
                BaseActionCommand action = snapshot[i];
                if (action.State == ActionState.Resolving && CurrentTick > action.ResolveTick)
                    Transition(action, CurrentTick >= action.RecoveryEndTick ? ActionState.Completed : ActionState.Recovery);
            }

            // Recovery → Completed
            for (int i = 0; i < snapshot.Length; i++)
            {
                BaseActionCommand action = snapshot[i];
                if (action.State == ActionState.Recovery && CurrentTick >= action.RecoveryEndTick)
                    Transition(action, ActionState.Completed);
            }

            ProcessPendingInterrupts();
            PruneInactiveActions();
        }

        public void InterruptAction(Guid actionId)
        {
            InterruptAction(actionId, Guid.Empty, InterruptPriority.Normal);
        }

        public void InterruptAction(
            Guid targetActionId,
            Guid sourceActionId,
            InterruptPriority priority = InterruptPriority.Normal)
        {
            if (targetActionId == Guid.Empty)
                return;
            if (!_activeActions.TryGetValue(targetActionId, out BaseActionCommand action))
                return;
            if (!_interruptArbitrationService.CanInterrupt(action))
                return;

            IActionCommand sourceAction = sourceActionId != Guid.Empty &&
                                          _activeActions.TryGetValue(sourceActionId, out BaseActionCommand source)
                ? source
                : null;
            PendingInterruptRequest request = _interruptArbitrationService.BuildRequest(
                sourceActionId,
                targetActionId,
                priority,
                CurrentTick,
                sourceAction);

            if (!_pendingInterrupts.TryGetValue(targetActionId, out PendingInterruptRequest existing) ||
                _interruptArbitrationService.IsIncomingRequestHigher(existing, request))
            {
                _pendingInterrupts[targetActionId] = request;
            }
        }

        public void CancelAction(Guid actionId)
        {
            if (actionId == Guid.Empty)
                return;
            if (!_activeActions.TryGetValue(actionId, out BaseActionCommand action))
                return;

            // Cancellation is only permitted before the Resolving phase.
            if (!ActionStateMachine.CanCancel(action.State))
                return;

            ActionState previousState = action.State;
            action.TransitionTo(ActionState.Cancelled);
            PublishLifecycleEvent(action, previousState);
            _activeActions.Remove(actionId);
            _pendingInterrupts.Remove(actionId);
            _interruptContextByTarget.Remove(actionId);
        }

        public IReadOnlyCollection<IActionCommand> GetActiveActions()
        {
            return new List<IActionCommand>(_activeActions.Values);
        }

        public IReadOnlyList<IActionCommand> DrainResolveQueue()
        {
            List<IActionCommand> ready = new(_pendingResolveQueue.Count);
            while (_pendingResolveQueue.Count > 0)
            {
                Guid actionId = _pendingResolveQueue.Dequeue();
                if (_activeActions.TryGetValue(actionId, out BaseActionCommand action) && action.State == ActionState.Resolving)
                    ready.Add(action);
            }

            return ready;
        }

        public void TerminateActionsForActors(IReadOnlyCollection<Guid> actorIds)
        {
            if (actorIds == null || actorIds.Count == 0)
                return;

            BaseActionCommand[] snapshot = new BaseActionCommand[_activeActions.Count];
            _activeActions.Values.CopyTo(snapshot, 0);

            for (int i = 0; i < snapshot.Length; i++)
            {
                BaseActionCommand action = snapshot[i];
                if (action == null || action.IsCompleted || !ContainsActor(actorIds, action.ActorId))
                    continue;

                Transition(action, ActionState.Interrupted);
                _activeActions.Remove(action.ActionId);
                _pendingInterrupts.Remove(action.ActionId);
                _interruptContextByTarget.Remove(action.ActionId);
            }
        }

        private static bool ContainsActor(IReadOnlyCollection<Guid> actorIds, Guid actorId)
        {
            foreach (Guid candidate in actorIds)
            {
                if (candidate == actorId)
                    return true;
            }

            return false;
        }

        private void ProcessPendingInterrupts()
        {
            if (_pendingInterrupts.Count == 0)
                return;

            PendingInterruptRequest[] requests = new PendingInterruptRequest[_pendingInterrupts.Count];
            int requestIndex = 0;
            foreach (KeyValuePair<Guid, PendingInterruptRequest> pair in _pendingInterrupts)
                requests[requestIndex++] = pair.Value;
            _pendingInterrupts.Clear();
            Array.Sort(requests, _interruptArbitrationService.ComparePendingRequestOrder);

            for (int i = 0; i < requests.Length; i++)
            {
                PendingInterruptRequest request = requests[i];
                if (!_activeActions.TryGetValue(request.TargetActionId, out BaseActionCommand action))
                    continue;
                if (!_interruptArbitrationService.CanInterrupt(action))
                    continue;

                IActionCommand sourceAction = request.SourceActionId != Guid.Empty &&
                                              _activeActions.TryGetValue(request.SourceActionId, out BaseActionCommand source)
                    ? source
                    : null;
                if (!_interruptArbitrationService.ShouldInterrupt(request.Priority, sourceAction, action))
                    continue;

                _interruptContextByTarget[request.TargetActionId] = request;
                try
                {
                    Transition(action, ActionState.Interrupted);
                    _activeActions.Remove(request.TargetActionId);
                }
                finally
                {
                    _interruptContextByTarget.Remove(request.TargetActionId);
                }
            }
        }

        private void PruneInactiveActions()
        {
            List<Guid> inactiveIds = null;
            foreach ((Guid actionId, BaseActionCommand action) in _activeActions)
            {
                if (!action.IsCompleted)
                    continue;

                inactiveIds ??= new List<Guid>();
                inactiveIds.Add(actionId);
            }

            if (inactiveIds == null)
                return;

            for (int i = 0; i < inactiveIds.Count; i++)
            {
                _activeActions.Remove(inactiveIds[i]);
                _interruptContextByTarget.Remove(inactiveIds[i]);
            }
        }

        private void Transition(BaseActionCommand action, ActionState newState)
        {
            if (action.State == newState)
                return;

            ActionState previousState = action.State;
            action.TransitionTo(newState);
            PublishLifecycleEvent(action, previousState);
        }

        private void PublishLifecycleEvent(IActionCommand action, ActionState previousState)
        {
            if (_eventBus == null)
                return;

            var payload = new ActionLifecyclePayload(
                action.ActionId,
                action.ActorId,
                previousState,
                action.State,
                CurrentTick,
                action.QueuedTick,
                action.StartTick,
                action.ResolveTick,
                action.RecoveryEndTick,
                action.IsInterruptible);

            string source = action.ActorId.ToString("N");
            string target = action.ActionId.ToString("N");

            // Always publish the generic state-changed event.
            _eventBus.Publish(new ActionStateChangedEvent(payload, source, target));

            // Publish the specific semantic event for the new state.
            switch (action.State)
            {
                case ActionState.Queued:
                    _eventBus.Publish(new ActionQueuedEvent(payload, source, target));
                    break;
                case ActionState.Casting:
                    _eventBus.Publish(new ActionCastingEvent(payload, source, target));
                    break;
                case ActionState.Resolving:
                    _eventBus.Publish(new ActionResolvedEvent(payload, source, target));
                    break;
                case ActionState.Recovery:
                    _eventBus.Publish(new ActionRecoveryEvent(payload, source, target));
                    break;
                case ActionState.Interrupted:
                    PendingInterruptRequest interruptContext =
                        _interruptContextByTarget.TryGetValue(action.ActionId, out PendingInterruptRequest request)
                            ? request
                            : new PendingInterruptRequest(
                                Guid.Empty,
                                action.ActionId,
                                InterruptPriority.Normal,
                                action.SpeedTier,
                                action.StartTick,
                                CurrentTick);
                    _eventBus.Publish(new ActionInterruptedEvent(
                        new ActionInterruptedPayload(
                            interruptContext.SourceActionId,
                            interruptContext.TargetActionId,
                            action.ActorId,
                            interruptContext.Priority,
                            CurrentTick),
                        interruptContext.SourceActionId != Guid.Empty ? interruptContext.SourceActionId.ToString("N") : source,
                        target));
                    break;
                case ActionState.Completed:
                    _eventBus.Publish(new ActionCompletedEvent(payload, source, target));
                    break;
            }
        }
    }
}
