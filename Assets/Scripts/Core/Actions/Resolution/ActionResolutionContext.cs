using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Actions.Resolution
{
    /// <summary>
    /// Carries the full mutable state of one tick's action resolution across all pipeline phases.
    /// Mutations must only be added during the <see cref="ResolutionPhase.Resolve"/> phase.
    /// Events may be added during any phase.
    /// </summary>
    public sealed class ActionResolutionContext
    {
        private readonly List<QueuedMutation> _pendingMutations = new();
        private readonly List<IGameEvent> _pendingEvents = new();
        private readonly Dictionary<Guid, ActionCancellationReason> _cancelledActions = new();
        private readonly Dictionary<Guid, int> _resolveOrderByActionId = new();
        private int _mutationSequence;

        public ActionResolutionContext(int currentTick, IReadOnlyList<IActionCommand> pendingActions)
        {
            CurrentTick = currentTick;
            PendingActions = pendingActions ?? Array.Empty<IActionCommand>();
            for (int i = 0; i < PendingActions.Count; i++)
            {
                IActionCommand action = PendingActions[i];
                if (action == null || action.ActionId == Guid.Empty)
                    continue;
                _resolveOrderByActionId[action.ActionId] = i;
            }
        }

        /// <summary>Tick being resolved.</summary>
        public int CurrentTick { get; }

        /// <summary>Phase currently executing in the pipeline.</summary>
        public ResolutionPhase CurrentPhase { get; internal set; }

        /// <summary>
        /// Actions scheduled to resolve this tick, sorted by deterministic speed order:
        /// ActionSpeedTier → ResolveTick → ActionId.
        /// </summary>
        public IReadOnlyList<IActionCommand> PendingActions { get; }

        /// <summary>
        /// Mutations accumulated during <see cref="ResolutionPhase.Resolve"/>.
        /// Applied in order after the Resolve phase completes.
        /// </summary>
        public IReadOnlyList<IRuntimeMutation> PendingMutations
        {
            get
            {
                if (_pendingMutations.Count == 0)
                    return Array.Empty<IRuntimeMutation>();

                var projected = new IRuntimeMutation[_pendingMutations.Count];
                for (int i = 0; i < _pendingMutations.Count; i++)
                    projected[i] = _pendingMutations[i].Mutation;
                return projected;
            }
        }

        internal IReadOnlyList<QueuedMutation> PendingMutationQueue => _pendingMutations;

        /// <summary>Events accumulated across all phases. Flushed during Finalize.</summary>
        public IReadOnlyList<IGameEvent> PendingEvents => _pendingEvents;
        public IReadOnlyDictionary<Guid, ActionCancellationReason> CancelledActions => _cancelledActions;

        internal void AddMutation(IRuntimeMutation mutation)
        {
            EnsureMutationPhase();
            if (mutation == null)
                return;

            _pendingMutations.Add(new QueuedMutation(
                mutation,
                ActionSpeedTier.Normal,
                int.MaxValue,
                _mutationSequence++));
        }

        internal void AddMutations(IActionCommand action, IReadOnlyList<IRuntimeMutation> mutations)
        {
            EnsureMutationPhase();
            if (mutations == null)
                return;

            ActionSpeedTier speed = action != null ? action.SpeedTier : ActionSpeedTier.Normal;
            int resolveOrder = ResolveOrderOf(action);

            for (int i = 0; i < mutations.Count; i++)
            {
                if (mutations[i] != null)
                {
                    _pendingMutations.Add(new QueuedMutation(
                        mutations[i],
                        speed,
                        resolveOrder,
                        _mutationSequence++));
                }
            }
        }

        internal void AddEvent(IGameEvent gameEvent)
        {
            if (gameEvent != null)
                _pendingEvents.Add(gameEvent);
        }

        internal void AddEvents(IReadOnlyList<IGameEvent> events)
        {
            if (events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null)
                    _pendingEvents.Add(events[i]);
            }
        }

        internal void MarkCancelled(Guid actionId, ActionCancellationReason reason)
        {
            if (actionId == Guid.Empty)
                return;

            _cancelledActions[actionId] = reason;
        }

        public bool IsCancelled(Guid actionId)
        {
            return actionId != Guid.Empty && _cancelledActions.ContainsKey(actionId);
        }

        public bool TryGetCancellationReason(Guid actionId, out ActionCancellationReason reason)
        {
            return _cancelledActions.TryGetValue(actionId, out reason);
        }

        private int ResolveOrderOf(IActionCommand action)
        {
            if (action == null || action.ActionId == Guid.Empty)
                return int.MaxValue;

            return _resolveOrderByActionId.TryGetValue(action.ActionId, out int order)
                ? order
                : int.MaxValue;
        }

        private void EnsureMutationPhase()
        {
            if (CurrentPhase != ResolutionPhase.Resolve)
                throw new InvalidOperationException("Runtime mutations can only be buffered during Resolve phase.");
        }
    }
}
