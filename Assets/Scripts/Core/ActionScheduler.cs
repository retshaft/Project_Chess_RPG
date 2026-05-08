using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    /// <summary>
    /// Centralized scheduler responsible for action lifecycle state changes.
    /// </summary>
    public sealed class ActionScheduler
    {
        private readonly Dictionary<string, IActionCommand> _actions = new();
        private readonly List<string> _removalBuffer = new();
        private readonly IEventBus _eventBus;

        public ActionScheduler(IEventBus eventBus = null)
        {
            _eventBus = eventBus;
        }

        public int CurrentTick { get; private set; }

        public IReadOnlyCollection<IActionCommand> Actions => _actions.Values;

        public IActionCommand ScheduleAction(IActionCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            ValidateTimeline(command);

            if (_actions.ContainsKey(command.ActionId))
                throw new InvalidOperationException($"Action with id '{command.ActionId}' is already scheduled.");

            IActionCommand queued = NormalizeAndQueue(command);
            _actions[queued.ActionId] = queued;
            PublishStateChange(queued, command.State);
            return queued;
        }

        public void AdvanceTick()
        {
            CurrentTick++;

            foreach (string actionId in new List<string>(_actions.Keys))
            {
                IActionCommand action = _actions[actionId];

                switch (action.State)
                {
                    case ActionCommandState.Queued when CurrentTick >= action.StartTick:
                        _actions[actionId] = TransitionStateAndNotify(action, ActionCommandState.Executing);
                        break;
                    case ActionCommandState.Resolving:
                        _actions[actionId] = ResolvePostResolveState(action);
                        break;
                    case ActionCommandState.Recovery when CurrentTick >= action.RecoveryEndTick:
                        _actions[actionId] = TransitionStateAndNotify(action, ActionCommandState.Completed);
                        break;
                    case ActionCommandState.Interrupted:
                        _actions[actionId] = TransitionStateAndNotify(action, ActionCommandState.Completed);
                        break;
                }
            }

            PruneTerminalActions();
        }

        public IReadOnlyList<IActionCommand> ResolveReadyActions()
        {
            List<IActionCommand> ready = new();

            foreach (string actionId in new List<string>(_actions.Keys))
            {
                IActionCommand action = _actions[actionId];
                if (action.State != ActionCommandState.Executing)
                    continue;

                if (CurrentTick < action.ResolveTick)
                    continue;

                IActionCommand resolving = TransitionStateAndNotify(action, ActionCommandState.Resolving);
                _actions[actionId] = resolving;
                ready.Add(resolving);
            }

            return ready;
        }

        public bool InterruptAction(string actionId)
        {
            if (!_actions.TryGetValue(actionId, out IActionCommand action))
                return false;

            if (action.State is ActionCommandState.Completed or ActionCommandState.Cancelled or ActionCommandState.Interrupted)
                return false;

            _actions[actionId] = TransitionStateAndNotify(action, ActionCommandState.Interrupted);
            return true;
        }

        public bool CancelAction(string actionId)
        {
            if (!_actions.TryGetValue(actionId, out IActionCommand action))
                return false;

            if (action.State is ActionCommandState.Completed or ActionCommandState.Cancelled)
                return false;

            _actions[actionId] = TransitionState(action, ActionCommandState.Cancelled);
            _actions.Remove(actionId);
            return true;
        }

        private IActionCommand ResolvePostResolveState(IActionCommand action)
        {
            if (CurrentTick >= action.RecoveryEndTick)
                return TransitionStateAndNotify(action, ActionCommandState.Completed);

            if (CurrentTick > action.ResolveTick)
                return TransitionStateAndNotify(action, ActionCommandState.Recovery);

            return action;
        }

        private void PruneTerminalActions()
        {
            _removalBuffer.Clear();

            foreach ((string actionId, IActionCommand action) in _actions)
            {
                if (action.State is ActionCommandState.Completed or ActionCommandState.Cancelled or ActionCommandState.Interrupted)
                    _removalBuffer.Add(actionId);
            }

            foreach (string actionId in _removalBuffer)
                _actions.Remove(actionId);
        }

        private void ValidateTimeline(IActionCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.ActionId))
                throw new ArgumentException("ActionId must not be null or whitespace.", nameof(command));

            if (command.StartTick < CurrentTick)
                throw new ArgumentException(
                    $"StartTick ({command.StartTick}) must not be less than scheduler tick ({CurrentTick}).",
                    nameof(command));

            if (command.ResolveTick < command.StartTick)
                throw new ArgumentException(
                    $"ResolveTick ({command.ResolveTick}) must be greater than or equal to StartTick ({command.StartTick}).",
                    nameof(command));

            if (command.RecoveryEndTick < command.ResolveTick)
                throw new ArgumentException(
                    $"RecoveryEndTick ({command.RecoveryEndTick}) must be greater than or equal to ResolveTick ({command.ResolveTick}).",
                    nameof(command));

        }

        private IActionCommand NormalizeAndQueue(IActionCommand command)
        {
            return command switch
            {
                ActionCommandBase baseAction => baseAction with
                {
                    QueuedTick = CurrentTick,
                    State = ActionCommandState.Queued
                },
                _ => throw new InvalidOperationException(
                    $"Action type '{command.GetType().Name}' is not supported by ActionScheduler.")
            };
        }

        private static IActionCommand TransitionState(IActionCommand action, ActionCommandState newState)
        {
            return action switch
            {
                ActionCommandBase baseAction => baseAction with { State = newState },
                _ => throw new InvalidOperationException(
                    $"Action type '{action.GetType().Name}' is not supported by ActionScheduler state transitions.")
            };
        }

        private IActionCommand TransitionStateAndNotify(IActionCommand action, ActionCommandState newState)
        {
            IActionCommand updated = TransitionState(action, newState);
            PublishStateChange(updated, action.State);
            return updated;
        }

        private void PublishStateChange(IActionCommand action, ActionCommandState previousState)
        {
            if (_eventBus == null)
                return;

            ActionPhasePayload payload = new(
                action.ActionId,
                action.ActorId,
                action.Targets,
                previousState,
                action.State,
                CurrentTick,
                action.QueuedTick,
                action.StartTick,
                action.ResolveTick,
                action.RecoveryEndTick);

            string target = action.Targets.Count > 0 ? string.Join(",", action.Targets) : string.Empty;

            switch (action.State)
            {
                case ActionCommandState.Queued:
                    _eventBus.Publish(new ActionQueuedEvent(payload, action.ActorId, target));
                    break;
                case ActionCommandState.Executing:
                    _eventBus.Publish(new ActionStartedEvent(payload, action.ActorId, target));
                    break;
                case ActionCommandState.Resolving:
                    _eventBus.Publish(new ActionResolvedEvent(payload, action.ActorId, target));
                    break;
                case ActionCommandState.Interrupted:
                    _eventBus.Publish(new ActionInterruptedEvent(payload, action.ActorId, target));
                    break;
                case ActionCommandState.Completed:
                    _eventBus.Publish(new ActionCompletedEvent(payload, action.ActorId, target));
                    break;
                case ActionCommandState.Recovery:
                case ActionCommandState.Cancelled:
                default:
                    break;
            }
        }
    }
}
