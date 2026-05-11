using System;
using System.Collections.Generic;
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
        private readonly List<IRuntimeMutation> _pendingMutations = new();
        private readonly List<IGameEvent> _pendingEvents = new();

        public ActionResolutionContext(int currentTick, IReadOnlyList<IActionCommand> pendingActions)
        {
            CurrentTick = currentTick;
            PendingActions = pendingActions ?? Array.Empty<IActionCommand>();
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
        public IReadOnlyList<IRuntimeMutation> PendingMutations => _pendingMutations;

        /// <summary>Events accumulated across all phases. Flushed during Finalize.</summary>
        public IReadOnlyList<IGameEvent> PendingEvents => _pendingEvents;

        internal void AddMutation(IRuntimeMutation mutation)
        {
            if (mutation != null)
                _pendingMutations.Add(mutation);
        }

        internal void AddMutations(IReadOnlyList<IRuntimeMutation> mutations)
        {
            if (mutations == null)
                return;

            for (int i = 0; i < mutations.Count; i++)
            {
                if (mutations[i] != null)
                    _pendingMutations.Add(mutations[i]);
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
    }
}
