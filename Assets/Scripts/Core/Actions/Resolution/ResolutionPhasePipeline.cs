using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions.Resolvers;

namespace CheckmateRPG.Core.Actions.Resolution
{
    /// <summary>
    /// Executes the four-phase action resolution pipeline for a single tick.
    /// <para>
    /// Phase order: <see cref="ResolutionPhase.PreResolve"/> →
    ///              <see cref="ResolutionPhase.Resolve"/> →
    ///              <see cref="ResolutionPhase.PostResolve"/> →
    ///              <see cref="ResolutionPhase.Finalize"/>.
    /// </para>
    /// <para>
    /// Rules enforced by this pipeline:
    /// <list type="bullet">
    ///   <item>Actions are resolved in deterministic ActionSpeedTier order (fastest first).</item>
    ///   <item>Mutations are only collected during Resolve; they must not be applied to runtime during that phase.</item>
    ///   <item>Reactions are only triggered in PostResolve and follow the same speed ordering.</item>
    ///   <item>Death finalization only occurs in Finalize.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class ResolutionPhasePipeline
    {
        private readonly Func<IActionCommand, IBattleContext, ActionResolutionResult> _resolveAction;

        /// <summary>
        /// Optional per-action callback executed in <see cref="ResolutionPhase.PreResolve"/>.
        /// Use for interrupt checks, counter reservations, and pre-validation.
        /// </summary>
        private readonly Action<IActionCommand, ActionResolutionContext> _onPreResolveAction;

        /// <summary>
        /// Optional per-action callback executed in <see cref="ResolutionPhase.PostResolve"/>.
        /// Use for reactions, reflect effects, and chain-reaction scheduling.
        /// Callbacks are invoked in the same deterministic speed order as Resolve.
        /// </summary>
        private readonly Action<IActionCommand, ActionResolutionContext> _onPostResolveAction;

        /// <summary>
        /// Optional callback executed once at the end of <see cref="ResolutionPhase.Finalize"/>.
        /// Use for death confirmation, cleanup tasks, and event flush preparation.
        /// </summary>
        private readonly Action<ActionResolutionContext> _onFinalize;

        /// <param name="resolveAction">
        /// Delegate that computes the resolution result for a single action without mutating runtime state.
        /// </param>
        /// <param name="onPreResolveAction">Optional per-action PreResolve handler.</param>
        /// <param name="onPostResolveAction">Optional per-action PostResolve handler (reactions).</param>
        /// <param name="onFinalize">Optional Finalize handler (death / cleanup).</param>
        public ResolutionPhasePipeline(
            Func<IActionCommand, IBattleContext, ActionResolutionResult> resolveAction,
            Action<IActionCommand, ActionResolutionContext> onPreResolveAction = null,
            Action<IActionCommand, ActionResolutionContext> onPostResolveAction = null,
            Action<ActionResolutionContext> onFinalize = null)
        {
            _resolveAction = resolveAction ?? throw new ArgumentNullException(nameof(resolveAction));
            _onPreResolveAction = onPreResolveAction;
            _onPostResolveAction = onPostResolveAction;
            _onFinalize = onFinalize;
        }

        /// <summary>
        /// Runs all four resolution phases for the given tick and returns the populated context
        /// containing the ordered pending mutations and events.
        /// </summary>
        /// <param name="tick">Current simulation tick.</param>
        /// <param name="readyActions">Actions that completed execution this tick (unsorted).</param>
        /// <param name="battleContext">Read-only battle state snapshot used by resolvers.</param>
        public ActionResolutionContext Execute(
            int tick,
            IReadOnlyList<IActionCommand> readyActions,
            IBattleContext battleContext)
        {
            IReadOnlyList<IActionCommand> sorted = SortActions(readyActions);
            var context = new ActionResolutionContext(tick, sorted);

            RunPreResolve(context);
            RunResolve(context, battleContext);
            RunPostResolve(context);
            RunFinalize(context);

            return context;
        }

        // ── Phase runners ─────────────────────────────────────────────────────────

        /// <summary>
        /// PreResolve: interrupt checks, counter reservation, and validation.
        /// </summary>
        private void RunPreResolve(ActionResolutionContext context)
        {
            context.CurrentPhase = ResolutionPhase.PreResolve;

            if (_onPreResolveAction == null)
                return;

            IReadOnlyList<IActionCommand> actions = context.PendingActions;
            for (int i = 0; i < actions.Count; i++)
                _onPreResolveAction(actions[i], context);
        }

        /// <summary>
        /// Resolve: damage computation, movement, and mutation generation.
        /// Runtime state must NOT be mutated here; mutations are buffered in the context.
        /// </summary>
        private void RunResolve(ActionResolutionContext context, IBattleContext battleContext)
        {
            context.CurrentPhase = ResolutionPhase.Resolve;

            IReadOnlyList<IActionCommand> actions = context.PendingActions;
            for (int i = 0; i < actions.Count; i++)
            {
                IActionCommand action = actions[i];
                if (action == null)
                    continue;

                ActionResolutionResult result = _resolveAction(action, battleContext);
                if (!result.Success)
                    continue;

                context.AddMutations(result.RuntimeMutations);
                context.AddEvents(result.Events);
            }
        }

        /// <summary>
        /// PostResolve: reaction triggers, reflect, and chain-reaction scheduling.
        /// Reactions follow the same deterministic speed ordering as Resolve.
        /// Recursive immediate resolution of reactions is forbidden here.
        /// </summary>
        private void RunPostResolve(ActionResolutionContext context)
        {
            context.CurrentPhase = ResolutionPhase.PostResolve;

            if (_onPostResolveAction == null)
                return;

            IReadOnlyList<IActionCommand> actions = context.PendingActions;
            for (int i = 0; i < actions.Count; i++)
                _onPostResolveAction(actions[i], context);
        }

        /// <summary>
        /// Finalize: death confirmation, cleanup, and event flush.
        /// Units with HP &lt;= 0 are only confirmed dead in this phase.
        /// </summary>
        private void RunFinalize(ActionResolutionContext context)
        {
            context.CurrentPhase = ResolutionPhase.Finalize;
            _onFinalize?.Invoke(context);
        }

        // ── Ordering ──────────────────────────────────────────────────────────────

        private static IReadOnlyList<IActionCommand> SortActions(IReadOnlyList<IActionCommand> actions)
        {
            if (actions == null || actions.Count == 0)
                return Array.Empty<IActionCommand>();

            if (actions.Count == 1)
                return actions;

            var sorted = new IActionCommand[actions.Count];
            for (int i = 0; i < actions.Count; i++)
                sorted[i] = actions[i];

            Array.Sort(sorted, ResolutionOrderComparer.Default);
            return sorted;
        }
    }
}
