using System;
using System.Collections.Generic;
using System.Linq;
using CheckmateRPG.Core.Events.ActionEvents;

namespace CheckmateRPG.Core.Actions
{
    public sealed class ActionScheduler : IActionScheduler
    {
        private readonly Dictionary<Guid, BaseActionCommand> _activeActions = new();
        private readonly Queue<Guid> _pendingResolveQueue = new();
        private readonly Dictionary<Guid, List<PendingInterruptRequest>> _pendingInterrupts = new();
        private readonly Dictionary<Guid, PendingInterruptRequest> _interruptContextByTarget = new();
        private readonly Dictionary<Guid, InterruptArbitrationReason> _interruptReasonByTarget = new();
        private readonly Dictionary<Guid, UnitActionLockState> _lockStateByActor = new();
        private readonly Dictionary<Guid, Queue<BaseActionCommand>> _deferredActionsByActor = new();
        private readonly HashSet<Guid> _deferredActionIds = new();
        private readonly IEventBus _eventBus;
        private readonly InterruptArbitrationService _interruptArbitrationService;

        public ActionScheduler(IEventBus eventBus = null)
        {
            _eventBus = eventBus;
            _interruptArbitrationService = new InterruptArbitrationService();
        }

        public int CurrentTick { get; private set; }

        public ActionAdmissionResult ScheduleAction(IActionCommand action)
        {
            if (action is not BaseActionCommand command)
            {
                return RejectAdmission(
                    action?.ActionId ?? Guid.Empty,
                    action?.ActorId ?? Guid.Empty,
                    ActionAdmissionRejectionReason.InvalidAction,
                    ActionLockType.None);
            }

            if (_activeActions.ContainsKey(command.ActionId) || _deferredActionIds.Contains(command.ActionId))
            {
                return RejectAdmission(
                    command.ActionId,
                    command.ActorId,
                    ActionAdmissionRejectionReason.DuplicateAction,
                    GetCurrentLock(command.ActorId));
            }

            if (command.StartTick < CurrentTick ||
                command.ResolveTick < command.StartTick ||
                command.RecoveryEndTick < command.ResolveTick)
            {
                return RejectAdmission(
                    command.ActionId,
                    command.ActorId,
                    ActionAdmissionRejectionReason.InvalidTimeline,
                    GetCurrentLock(command.ActorId));
            }

            ActionAdmissionResult admissionResult = EvaluateAdmission(command);
            switch (admissionResult.Status)
            {
                case ActionAdmissionStatus.Scheduled:
                    ActivateAction(command);
                    return admissionResult;
                case ActionAdmissionStatus.Deferred:
                    EnqueueDeferred(command);
                    return admissionResult;
                default:
                    return RejectAdmission(command.ActionId, command.ActorId, admissionResult.Reason, admissionResult.CurrentLock);
            }
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
            TryAdmitDeferredActions();
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

            if (!_pendingInterrupts.TryGetValue(targetActionId, out List<PendingInterruptRequest> requests))
            {
                requests = new List<PendingInterruptRequest>();
                _pendingInterrupts[targetActionId] = requests;
            }

            int existingRequestIndex = -1;
            for (int i = 0; i < requests.Count; i++)
            {
                if (requests[i].SourceActionId == request.SourceActionId)
                {
                    existingRequestIndex = i;
                    break;
                }
            }

            if (existingRequestIndex < 0)
            {
                requests.Add(request);
                return;
            }

            if (_interruptArbitrationService.IsIncomingRequestHigher(requests[existingRequestIndex], request))
                requests[existingRequestIndex] = request;
        }

        public void CancelAction(Guid actionId)
        {
            InterruptAction(actionId, Guid.Empty, InterruptPriority.Absolute);
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
                _interruptReasonByTarget.Remove(action.ActionId);
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

        private ActionAdmissionResult EvaluateAdmission(BaseActionCommand command)
        {
            if (!TryGetActiveLock(command.ActorId, out UnitActionLockState currentLockState))
                return ActionAdmissionResult.Scheduled();

            ActionLockType currentLock = currentLockState.ActiveLock;
            return command.ConcurrencyPolicy switch
            {
                ActionConcurrencyPolicy.Queue => ActionAdmissionResult.Deferred(currentLock),
                ActionConcurrencyPolicy.Replace => TryReplaceCurrentLock(currentLockState, command.ActionId)
                    ? ActionAdmissionResult.Scheduled()
                    : ActionAdmissionResult.Rejected(ActionAdmissionRejectionReason.ReplaceNotAllowed, currentLock),
                ActionConcurrencyPolicy.ParallelAllowed => CanRunParallel(command, currentLock)
                    ? ActionAdmissionResult.Scheduled()
                    : ActionAdmissionResult.Rejected(ActionAdmissionRejectionReason.ParallelNotAllowed, currentLock),
                _ => ActionAdmissionResult.Rejected(ToLockRejectionReason(currentLock), currentLock)
            };
        }

        private static ActionAdmissionRejectionReason ToLockRejectionReason(ActionLockType lockType)
        {
            return lockType switch
            {
                ActionLockType.CastLock => ActionAdmissionRejectionReason.CastLocked,
                ActionLockType.RecoveryLock => ActionAdmissionRejectionReason.RecoveryLocked,
                ActionLockType.MovementLock => ActionAdmissionRejectionReason.MovementLocked,
                ActionLockType.GlobalLock => ActionAdmissionRejectionReason.GlobalLocked,
                _ => ActionAdmissionRejectionReason.None
            };
        }

        private static bool CanRunParallel(BaseActionCommand command, ActionLockType currentLock)
        {
            if (command == null)
                return false;
            if (command.IntentLockType != ActionLockType.None)
                return false;
            return currentLock != ActionLockType.GlobalLock;
        }

        private ActionAdmissionResult RejectAdmission(
            Guid actionId,
            Guid actorId,
            ActionAdmissionRejectionReason reason,
            ActionLockType currentLock)
        {
            PublishRejectedEvent(actionId, actorId, reason, currentLock);
            return ActionAdmissionResult.Rejected(reason, currentLock);
        }

        private void ActivateAction(BaseActionCommand command)
        {
            command.MarkQueued(CurrentTick);
            _activeActions.Add(command.ActionId, command);
            AcquireQueuedLock(command);
            PublishLifecycleEvent(command, ActionState.Queued);
        }

        private void EnqueueDeferred(BaseActionCommand command)
        {
            if (!_deferredActionsByActor.TryGetValue(command.ActorId, out Queue<BaseActionCommand> queue))
            {
                queue = new Queue<BaseActionCommand>();
                _deferredActionsByActor[command.ActorId] = queue;
            }

            queue.Enqueue(command);
            _deferredActionIds.Add(command.ActionId);
        }

        private void TryAdmitDeferredActions()
        {
            if (_deferredActionsByActor.Count == 0)
                return;

            Guid[] actorIds = _deferredActionsByActor.Keys.ToArray();
            for (int i = 0; i < actorIds.Length; i++)
            {
                Guid actorId = actorIds[i];
                if (!_deferredActionsByActor.TryGetValue(actorId, out Queue<BaseActionCommand> queue) || queue == null || queue.Count == 0)
                    continue;

                while (queue.Count > 0)
                {
                    BaseActionCommand command = queue.Peek();
                    if (command == null)
                    {
                        queue.Dequeue();
                        continue;
                    }

                    if (command.StartTick <= CurrentTick)
                        command.RebaseTimeline(CurrentTick + 1);

                    ActionAdmissionResult admissionResult = EvaluateAdmission(command);
                    if (admissionResult.Status == ActionAdmissionStatus.Scheduled)
                    {
                        queue.Dequeue();
                        _deferredActionIds.Remove(command.ActionId);
                        ActivateAction(command);
                        continue;
                    }

                    if (admissionResult.Status == ActionAdmissionStatus.Rejected)
                    {
                        queue.Dequeue();
                        _deferredActionIds.Remove(command.ActionId);
                        PublishRejectedEvent(command.ActionId, command.ActorId, admissionResult.Reason, admissionResult.CurrentLock);
                        continue;
                    }

                    break;
                }

                if (queue.Count == 0)
                    _deferredActionsByActor.Remove(actorId);
            }
        }

        private bool TryReplaceCurrentLock(UnitActionLockState lockState, Guid incomingActionId)
        {
            if (lockState == null || !lockState.SourceActionId.HasValue)
                return true;

            Guid sourceActionId = lockState.SourceActionId.Value;
            if (sourceActionId == incomingActionId)
                return true;

            if (!_activeActions.TryGetValue(sourceActionId, out BaseActionCommand sourceAction) || sourceAction == null)
            {
                lockState.Release();
                return true;
            }

            if (_interruptArbitrationService.CanInterrupt(sourceAction))
            {
                _interruptContextByTarget[sourceActionId] =
                    _interruptArbitrationService.BuildFallbackRequest(sourceAction, CurrentTick);
                _interruptReasonByTarget[sourceActionId] = InterruptArbitrationReason.DeterministicActionIdOrdering;
                try
                {
                    Transition(sourceAction, ActionState.Interrupted);
                    _activeActions.Remove(sourceActionId);
                    _pendingInterrupts.Remove(sourceActionId);
                }
                finally
                {
                    _interruptContextByTarget.Remove(sourceActionId);
                    _interruptReasonByTarget.Remove(sourceActionId);
                }

                return true;
            }

            return false;
        }

        private bool TryGetActiveLock(Guid actorId, out UnitActionLockState lockState)
        {
            if (!_lockStateByActor.TryGetValue(actorId, out lockState) || lockState == null)
                return false;
            if (!lockState.IsActiveAt(CurrentTick))
            {
                lockState.Release();
                return false;
            }

            return true;
        }

        private ActionLockType GetCurrentLock(Guid actorId)
        {
            return TryGetActiveLock(actorId, out UnitActionLockState lockState)
                ? lockState.ActiveLock
                : ActionLockType.None;
        }

        private UnitActionLockState GetOrCreateLockState(Guid actorId)
        {
            if (!_lockStateByActor.TryGetValue(actorId, out UnitActionLockState lockState) || lockState == null)
            {
                lockState = new UnitActionLockState();
                _lockStateByActor[actorId] = lockState;
            }

            return lockState;
        }

        private void AcquireQueuedLock(BaseActionCommand action)
        {
            if (action == null || action.IntentLockType == ActionLockType.None)
                return;

            int expirationTick = action.IntentLockType switch
            {
                ActionLockType.RecoveryLock => action.RecoveryEndTick,
                ActionLockType.GlobalLock => action.RecoveryEndTick,
                _ => action.ResolveTick + 1
            };
            GetOrCreateLockState(action.ActorId).Acquire(action.IntentLockType, action.ActionId, expirationTick);
        }

        private void UpdateLockForTransition(BaseActionCommand action, ActionState newState)
        {
            if (action == null)
                return;

            UnitActionLockState lockState = GetOrCreateLockState(action.ActorId);
            switch (newState)
            {
                case ActionState.Queued:
                case ActionState.Casting:
                case ActionState.Resolving:
                    AcquireQueuedLock(action);
                    break;
                case ActionState.Recovery:
                    if (action.IntentLockType != ActionLockType.None)
                        lockState.Acquire(ActionLockType.RecoveryLock, action.ActionId, action.RecoveryEndTick);
                    break;
                case ActionState.Completed:
                case ActionState.Cancelled:
                case ActionState.Interrupted:
                    lockState.ReleaseIfSource(action.ActionId);
                    break;
            }
        }

        private void ReleaseActionLock(BaseActionCommand action)
        {
            if (action == null)
                return;
            if (_lockStateByActor.TryGetValue(action.ActorId, out UnitActionLockState lockState) && lockState != null)
                lockState.ReleaseIfSource(action.ActionId);
        }

        private void ProcessPendingInterrupts()
        {
            if (_pendingInterrupts.Count == 0)
                return;

            var pendingByTarget = new Dictionary<Guid, List<PendingInterruptRequest>>(_pendingInterrupts.Count);
            foreach ((Guid targetActionId, List<PendingInterruptRequest> requests) in _pendingInterrupts)
            {
                if (requests == null || requests.Count == 0)
                    continue;

                var copied = new List<PendingInterruptRequest>(requests.Count);
                for (int i = 0; i < requests.Count; i++)
                    copied.Add(requests[i]);
                pendingByTarget[targetActionId] = copied;
            }

            Guid[] targetActionIds = pendingByTarget.Keys.ToArray();
            Array.Sort(targetActionIds);
            var interruptedThisPass = new HashSet<Guid>();
            _pendingInterrupts.Clear();

            for (int i = 0; i < targetActionIds.Length; i++)
            {
                Guid targetActionId = targetActionIds[i];
                if (!_activeActions.TryGetValue(targetActionId, out BaseActionCommand action))
                    continue;
                if (!_interruptArbitrationService.CanInterrupt(action))
                    continue;

                if (!pendingByTarget.TryGetValue(targetActionId, out List<PendingInterruptRequest> requestsForTarget) ||
                    requestsForTarget == null ||
                    requestsForTarget.Count == 0)
                    continue;

                var candidates = new List<PendingInterruptRequest>(requestsForTarget.Count);
                for (int j = 0; j < requestsForTarget.Count; j++)
                {
                    PendingInterruptRequest candidate = requestsForTarget[j];
                    if (candidate.SourceActionId == targetActionId)
                        continue;
                    // Prevent same-tick recursive chains (A interrupts B, then B interrupts C).
                    if (candidate.SourceActionId != Guid.Empty && interruptedThisPass.Contains(candidate.SourceActionId))
                        continue;

                    candidates.Add(candidate);
                }

                if (candidates.Count == 0)
                    continue;

                InterruptResult arbitrationResult = _interruptArbitrationService.Arbitrate(candidates, out PendingInterruptRequest winningRequest);
                IActionCommand sourceAction = null;
                if (winningRequest.SourceActionId != Guid.Empty)
                {
                    if (!_activeActions.TryGetValue(winningRequest.SourceActionId, out BaseActionCommand source))
                        continue;
                    if (!_interruptArbitrationService.CanInterruptOthers(source))
                        continue;
                    sourceAction = source;
                }

                if (!_interruptArbitrationService.ShouldInterrupt(winningRequest.Priority, sourceAction, action))
                    continue;

                _interruptContextByTarget[targetActionId] = winningRequest;
                _interruptReasonByTarget[targetActionId] = arbitrationResult.Reason;
                action.RegisterInterruptSource(winningRequest.SourceActionId);
                try
                {
                    Transition(action, ActionState.Interrupted);
                    _activeActions.Remove(targetActionId);
                    interruptedThisPass.Add(targetActionId);
                }
                finally
                {
                    _interruptContextByTarget.Remove(targetActionId);
                    _interruptReasonByTarget.Remove(targetActionId);
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
                _interruptReasonByTarget.Remove(inactiveIds[i]);
            }
        }

        private void Transition(BaseActionCommand action, ActionState newState)
        {
            if (action.State == newState)
                return;

            ActionState previousState = action.State;
            action.TransitionTo(newState);
            UpdateLockForTransition(action, newState);
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

            _eventBus.Publish(new ActionStateChangedEvent(payload, source, target));

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
                            : _interruptArbitrationService.BuildFallbackRequest(action, CurrentTick);
                    InterruptArbitrationReason interruptReason =
                        _interruptReasonByTarget.TryGetValue(action.ActionId, out InterruptArbitrationReason reason)
                            ? reason
                            : InterruptArbitrationReason.DeterministicActionIdOrdering;
                    _eventBus.Publish(new ActionInterruptedEvent(
                        new ActionInterruptedPayload(
                            interruptContext.SourceActionId,
                            interruptContext.TargetActionId,
                            action.ActorId,
                            interruptReason,
                            CurrentTick),
                        interruptContext.SourceActionId != Guid.Empty ? interruptContext.SourceActionId.ToString("N") : source,
                        target));
                    break;
                case ActionState.Completed:
                    _eventBus.Publish(new ActionCompletedEvent(payload, source, target));
                    break;
            }
        }

        private void PublishRejectedEvent(
            Guid requestedActionId,
            Guid actorId,
            ActionAdmissionRejectionReason reason,
            ActionLockType currentLock)
        {
            if (_eventBus == null)
                return;

            string source = actorId != Guid.Empty ? actorId.ToString("N") : string.Empty;
            string target = requestedActionId != Guid.Empty ? requestedActionId.ToString("N") : string.Empty;
            _eventBus.Publish(new ActionRejectedEvent(
                new ActionRejectedPayload(requestedActionId, actorId, reason, currentLock, CurrentTick),
                source,
                target));
        }
    }
}
