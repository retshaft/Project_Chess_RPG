using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Actions.Resolution;
using CheckmateRPG.Core.Actions.Resolvers;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Core.Simulation.Spatial;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// Deterministic prediction pipeline.
    /// <para>
    /// Executes the full action-resolution sequence on an isolated deep-clone of
    /// the live <see cref="SimulationRuntime"/>, collects the results, then discards
    /// the clone — leaving the real runtime completely untouched.
    /// </para>
    /// <para>
    /// Isolation guarantees (see § [4] and § [7] of the design spec):
    /// <list type="bullet">
    ///   <item>No mutation is ever applied to the source runtime.</item>
    ///   <item>No events are broadcast to any <see cref="IEventBus"/>.</item>
    ///   <item>No persistence or external side effects occur.</item>
    ///   <item>Resolve ordering is identical to the production pipeline.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class PredictionPipeline
    {
        private readonly Func<Vector2Int, bool> _isCellValid;
        private readonly Func<Guid, int> _attackRangeLookup;
        private readonly int _criticalDamageMultiplier;
        private readonly MutationOrderingService _mutationOrderingService;
        private readonly PositionReservationSystem _positionReservationSystem;

        /// <param name="isCellValid">
        /// Delegate that returns <c>true</c> when a board cell is within bounds.
        /// Use <c>_ => true</c> to disable board bounds checking during prediction.
        /// </param>
        /// <param name="attackRangeLookup">
        /// Delegate that returns the attack range (Chebyshev) for a given unit id.
        /// Use <c>_ => 1</c> for default melee range.
        /// </param>
        /// <param name="criticalDamageMultiplier">
        /// Multiplier applied to critical-hit damage.  Defaults to 2.
        /// </param>
        /// <param name="spatialPolicy">
        /// Policy used to resolve simultaneous move collisions.
        /// Defaults to <see cref="SpatialResolutionPolicy.HigherSpeedWins"/>,
        /// which matches the production <see cref="PositionReservationSystem"/>.
        /// </param>
        public PredictionPipeline(
            Func<Vector2Int, bool> isCellValid,
            Func<Guid, int> attackRangeLookup,
            int criticalDamageMultiplier = 2,
            SpatialResolutionPolicy spatialPolicy = SpatialResolutionPolicy.HigherSpeedWins)
        {
            _isCellValid = isCellValid ?? throw new ArgumentNullException(nameof(isCellValid));
            _attackRangeLookup = attackRangeLookup ?? throw new ArgumentNullException(nameof(attackRangeLookup));
            _criticalDamageMultiplier = Math.Max(1, criticalDamageMultiplier);
            _mutationOrderingService = new MutationOrderingService();
            _positionReservationSystem = new PositionReservationSystem(spatialPolicy);
        }

        /// <summary>
        /// Runs the prediction pipeline for the given actions against the provided
        /// source runtime and returns the immutable result.
        /// </summary>
        /// <param name="actions">
        /// The actions to simulate.  They are sorted into deterministic resolve order
        /// internally, matching the production pipeline exactly.
        /// </param>
        /// <param name="sourceRuntime">
        /// The live <see cref="SimulationRuntime"/> to clone.  This instance is
        /// <em>never</em> mutated.
        /// </param>
        /// <param name="tick">
        /// The simulation tick to associate with the prediction snapshot.
        /// </param>
        /// <returns>
        /// An immutable <see cref="PredictionResult"/> or
        /// <see cref="PredictionResult.Empty"/> when there are no actions.
        /// </returns>
        public PredictionResult Execute(
            IReadOnlyList<IActionCommand> actions,
            SimulationRuntime sourceRuntime,
            int tick)
        {
            if (sourceRuntime == null)
                throw new ArgumentNullException(nameof(sourceRuntime));

            if (actions == null || actions.Count == 0)
                return PredictionResult.Empty;

            // ── [2] Clone runtime ─────────────────────────────────────────────────
            var context = new PredictionSimulationContext(sourceRuntime, tick);

            // ── [6] Deterministic ordering (identical to production pipeline) ──────
            IReadOnlyList<IActionCommand> sorted = SortActions(actions);

            // Record the resolve order before anything is cancelled.
            RecordResolveOrder(sorted, context);

            // ── Spatial reservation snapshot (collision preview) ──────────────────
            PositionReservationSnapshot spatialSnapshot = _positionReservationSystem.Build(sorted);

            // ── [3] Simulate action resolution ────────────────────────────────────
            var battleContext = new PredictionBattleContext(
                context.PredictedRuntime,
                _isCellValid,
                _attackRangeLookup,
                _criticalDamageMultiplier);

            var resolutionContext = new ActionResolutionContext(tick, sorted);
            RunPreResolve(sorted, spatialSnapshot, battleContext, resolutionContext, context);
            RunResolve(sorted, resolutionContext, battleContext);

            // ── Apply mutations to the cloned runtime only ────────────────────────
            var mutationQueue = new MutationQueue();
            mutationQueue.EnqueueRange(resolutionContext.PendingMutationQueue);
            IReadOnlyList<IRuntimeMutation> orderedMutations =
                mutationQueue.CreateOrderedSnapshot(_mutationOrderingService);

            PredictionMutationApplier.Apply(orderedMutations, context);

            // ── [3] Collect result, [3] Discard clone ─────────────────────────────
            // The cloned runtime is referenced only by `context`; once we return
            // the result the GC will collect it.
            return context.ToResult();
        }

        // ── Phase runners ─────────────────────────────────────────────────────────

        /// <summary>
        /// Pre-resolve: marks already-cancelled/interrupted actions, validates spatial
        /// reservations for moves, and validates attack targets — all matching the
        /// same checks performed by the production
        /// <see cref="ResolutionPhasePipeline"/>.
        /// </summary>
        private static void RunPreResolve(
            IReadOnlyList<IActionCommand> sorted,
            PositionReservationSnapshot spatialSnapshot,
            PredictionBattleContext battleContext,
            ActionResolutionContext resolutionContext,
            PredictionSimulationContext context)
        {
            resolutionContext.CurrentPhase = ResolutionPhase.PreResolve;

            for (int i = 0; i < sorted.Count; i++)
            {
                IActionCommand action = sorted[i];
                if (action == null)
                    continue;

                // Already in a terminal or cancelled state.
                if (action.State == ActionState.Cancelled || action.State == ActionState.Interrupted)
                {
                    CancelAction(action, ActionCancellationReason.Interrupted, resolutionContext, context);
                    continue;
                }

                if (resolutionContext.IsCancelled(action.ActionId))
                    continue;

                // Actor must be alive in the cloned runtime.
                if (!battleContext.TryGetUnit(action.ActorId, out BattleUnitSnapshot actor) ||
                    (actor.StatusFlags & UnitStatusFlags.Dead) != 0)
                {
                    CancelAction(action, ActionCancellationReason.Interrupted, resolutionContext, context);
                    continue;
                }

                switch (action)
                {
                    case MoveActionCommand move:
                        ValidateMovePreResolve(move, spatialSnapshot, battleContext, resolutionContext, context);
                        break;
                    case AttackActionCommand attack:
                        ValidateAttackPreResolve(attack, battleContext, resolutionContext, context);
                        break;
                }
            }
        }

        private static void ValidateMovePreResolve(
            MoveActionCommand move,
            PositionReservationSnapshot spatialSnapshot,
            PredictionBattleContext battleContext,
            ActionResolutionContext resolutionContext,
            PredictionSimulationContext context)
        {
            if (resolutionContext.IsCancelled(move.ActionId))
                return;

            // Spatial collision: the move must hold the winning reservation for its target cell.
            if (!spatialSnapshot.HasWinningReservation(move.ActionId))
            {
                CancelAction(move, ActionCancellationReason.ReservationLost, resolutionContext, context);
                return;
            }

            // Target cell must be within board bounds.
            if (!battleContext.IsCellValid(move.To))
            {
                CancelAction(move, ActionCancellationReason.TargetInvalid, resolutionContext, context);
                return;
            }

            // Target cell must not be occupied by another unit.
            if (battleContext.IsCellOccupied(move.To, move.ActorId))
            {
                CancelAction(move, ActionCancellationReason.ReservationLost, resolutionContext, context);
            }
        }

        private static void ValidateAttackPreResolve(
            AttackActionCommand attack,
            PredictionBattleContext battleContext,
            ActionResolutionContext resolutionContext,
            PredictionSimulationContext context)
        {
            if (resolutionContext.IsCancelled(attack.ActionId))
                return;

            // Target must exist and be alive.
            if (!battleContext.TryGetUnit(attack.TargetId, out BattleUnitSnapshot target) ||
                (target.StatusFlags & UnitStatusFlags.Dead) != 0)
            {
                CancelAction(attack, ActionCancellationReason.TargetInvalid, resolutionContext, context);
                return;
            }

            // Target cell must be within board bounds.
            if (!battleContext.IsCellValid(target.Position))
            {
                CancelAction(attack, ActionCancellationReason.TargetInvalid, resolutionContext, context);
                return;
            }

            // Target must be within attack range.
            if (!battleContext.IsTargetInAttackRange(attack.ActorId, attack.TargetId))
            {
                CancelAction(attack, ActionCancellationReason.OutOfRange, resolutionContext, context);
            }
        }

        private static void CancelAction(
            IActionCommand action,
            ActionCancellationReason reason,
            ActionResolutionContext resolutionContext,
            PredictionSimulationContext context)
        {
            resolutionContext.MarkCancelled(action.ActionId, reason);
            context.RecordInterrupt(new PredictedInterrupt(action.ActionId, action.ActorId, reason));
        }

        /// <summary>
        /// Runs the resolve step using the production resolver registry and buffers
        /// mutations into the resolution context.
        /// </summary>
        private static void RunResolve(
            IReadOnlyList<IActionCommand> sorted,
            ActionResolutionContext resolutionContext,
            PredictionBattleContext battleContext)
        {
            resolutionContext.CurrentPhase = ResolutionPhase.Resolve;
            var resolverRegistry = new ActionResolverRegistry();

            for (int i = 0; i < sorted.Count; i++)
            {
                IActionCommand action = sorted[i];
                if (action == null)
                    continue;

                if (resolutionContext.IsCancelled(action.ActionId))
                    continue;

                if (action.State == ActionState.Cancelled || action.State == ActionState.Interrupted)
                    continue;

                if (!resolverRegistry.TryResolve(action, battleContext, out ActionResolutionResult result))
                    continue;

                if (!result.Success)
                    continue;

                resolutionContext.AddMutations(action, result.RuntimeMutations);
                // Events are intentionally not broadcast — prediction isolation rule § [4].
            }
        }

        // ── Ordering (mirrors ResolutionPhasePipeline.SortActions) ─────────────

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

        private static void RecordResolveOrder(
            IReadOnlyList<IActionCommand> sorted,
            PredictionSimulationContext context)
        {
            for (int i = 0; i < sorted.Count; i++)
            {
                IActionCommand action = sorted[i];
                if (action == null)
                    continue;

                context.RecordResolveOrder(new PredictedResolveOrder(
                    action.ActionId,
                    action.ActorId,
                    action.SpeedTier,
                    action.ResolveTick,
                    i));
            }
        }
    }
}
