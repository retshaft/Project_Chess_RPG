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
                        _actions[actionId] = TransitionState(action, ActionCommandState.Executing);
                        break;
                    case ActionCommandState.Resolving:
                        _actions[actionId] = ResolvePostResolveState(action);
                        break;
                    case ActionCommandState.Recovery when CurrentTick >= action.RecoveryEndTick:
                        _actions[actionId] = TransitionState(action, ActionCommandState.Completed);
                        break;
                    case ActionCommandState.Interrupted:
                        _actions[actionId] = TransitionState(action, ActionCommandState.Completed);
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

                IActionCommand resolving = TransitionState(action, ActionCommandState.Resolving);
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

            _actions[actionId] = TransitionState(action, ActionCommandState.Interrupted);
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
                return TransitionState(action, ActionCommandState.Completed);

            if (CurrentTick > action.ResolveTick)
                return TransitionState(action, ActionCommandState.Recovery);

            return action;
        }

        private void PruneTerminalActions()
        {
            _removalBuffer.Clear();

            foreach ((string actionId, IActionCommand action) in _actions)
            {
                if (action.State is ActionCommandState.Completed or ActionCommandState.Cancelled)
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
                    $"StartTick ({command.StartTick}) must be greater than or equal to scheduler tick ({CurrentTick}).",
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
    }
}
