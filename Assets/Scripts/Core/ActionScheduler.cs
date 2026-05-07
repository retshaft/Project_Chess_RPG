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

        public int CurrentTick { get; private set; }

        public IReadOnlyCollection<IActionCommand> Actions => _actions.Values;

        public IActionCommand ScheduleAction(IActionCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            IActionCommand queued = TransitionState(command, ActionCommandState.Queued);
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
                }
            }
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

            if (action.State is ActionCommandState.Completed or ActionCommandState.Cancelled)
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

        private static IActionCommand TransitionState(IActionCommand action, ActionCommandState newState)
        {
            return action switch
            {
                MoveAction move => move with { State = newState },
                BasicAttackAction attack => attack with { State = newState },
                AbilityAction ability => ability with { State = newState },
                ActionCommandBase baseAction => baseAction with { State = newState },
                _ => action
            };
        }
    }
}
