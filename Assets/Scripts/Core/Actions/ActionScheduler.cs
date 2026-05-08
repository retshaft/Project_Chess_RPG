using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Events.ActionEvents;

namespace CheckmateRPG.Core.Actions
{
    public sealed class ActionScheduler : IActionScheduler
    {
        private readonly Dictionary<Guid, BaseActionCommand> _activeActions = new();
        private readonly Queue<Guid> _pendingResolveQueue = new();
        private readonly HashSet<Guid> _pendingInterrupts = new();
        private readonly IEventBus _eventBus;

        public ActionScheduler(IEventBus eventBus = null)
        {
            _eventBus = eventBus;
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

            for (int i = 0; i < snapshot.Length; i++)
            {
                BaseActionCommand action = snapshot[i];
                if (action.State == ActionState.Queued && CurrentTick >= action.StartTick)
                    Transition(action, ActionState.Executing);
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                BaseActionCommand action = snapshot[i];
                if (action.State == ActionState.Executing && CurrentTick >= action.ResolveTick)
                {
                    Transition(action, ActionState.Resolving);
                    _pendingResolveQueue.Enqueue(action.ActionId);
                }
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                BaseActionCommand action = snapshot[i];
                if (action.State == ActionState.Resolving && CurrentTick > action.ResolveTick)
                    Transition(action, CurrentTick >= action.RecoveryEndTick ? ActionState.Completed : ActionState.Recovery);
            }

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
            if (actionId == Guid.Empty)
                return;
            if (!_activeActions.TryGetValue(actionId, out BaseActionCommand action))
                return;
            if (!action.IsInterruptible || action.State != ActionState.Executing)
                return;

            _pendingInterrupts.Add(actionId);
        }

        public void CancelAction(Guid actionId)
        {
            if (actionId == Guid.Empty)
                return;
            if (!_activeActions.TryGetValue(actionId, out BaseActionCommand action))
                return;
            if (action.State is not (ActionState.Queued or ActionState.Executing))
                return;

            ActionState previousState = action.State;
            action.TransitionTo(ActionState.Cancelled);
            PublishLifecycleEvent(action, previousState);
            _activeActions.Remove(actionId);
            _pendingInterrupts.Remove(actionId);
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

        private void ProcessPendingInterrupts()
        {
            if (_pendingInterrupts.Count == 0)
                return;

            Guid[] actionIds = new Guid[_pendingInterrupts.Count];
            _pendingInterrupts.CopyTo(actionIds);
            _pendingInterrupts.Clear();

            for (int i = 0; i < actionIds.Length; i++)
            {
                Guid actionId = actionIds[i];
                if (!_activeActions.TryGetValue(actionId, out BaseActionCommand action))
                    continue;
                if (!action.IsInterruptible || action.State != ActionState.Executing)
                    continue;

                Transition(action, ActionState.Interrupted);
                ActionState previousState = action.State;
                action.TransitionTo(ActionState.Cancelled);
                PublishLifecycleEvent(action, previousState);
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
                _activeActions.Remove(inactiveIds[i]);
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

            switch (action.State)
            {
                case ActionState.Queued:
                    _eventBus.Publish(new ActionQueuedEvent(payload, source, target));
                    break;
                case ActionState.Executing:
                    _eventBus.Publish(new ActionStartedEvent(payload, source, target));
                    break;
                case ActionState.Resolving:
                    _eventBus.Publish(new ActionResolvedEvent(payload, source, target));
                    break;
                case ActionState.Interrupted:
                    _eventBus.Publish(new ActionInterruptedEvent(payload, source, target));
                    break;
                case ActionState.Completed:
                    _eventBus.Publish(new ActionCompletedEvent(payload, source, target));
                    break;
            }
        }
    }
}
