using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using UnityEngine;

namespace CheckmateRPG.Core.Effects
{
    /// <summary>
    /// Executes registered effects in strict <see cref="EffectTimingPhase"/> order,
    /// providing the deterministic, replay-consistent effect scheduling required by
    /// the simulation.
    /// <para>
    /// Invariants enforced by this pipeline:
    /// <list type="bullet">
    ///   <item>Effects are only executed during their declared <see cref="EffectTimingPhase"/>.</item>
    ///   <item>Reaction effects (<see cref="IReadOnlyEffectRuntimeState.IsReaction"/> == <c>true</c>)
    ///         are rejected unless the current phase is <see cref="EffectTimingPhase.OnPostResolve"/>.</item>
    ///   <item>Duration countdown (<see cref="EffectRuntimeState.AdvanceTick"/>) is performed
    ///         exclusively during <see cref="EffectTimingPhase.OnTickEnd"/>.</item>
    ///   <item>Effects whose duration reaches 0 are enqueued in the
    ///         <see cref="EffectExpirationQueue"/> and never removed inline.</item>
    ///   <item>Within a phase, effects are ordered by
    ///         <see cref="EffectTimingOrderComparer"/>: AppliedTick → SpeedPriority → EffectId.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class EffectTimingPipeline
    {
        private readonly EffectExpirationQueue _expirationQueue;
        private readonly EffectMutationFactory _mutationFactory;

        /// <param name="expirationQueue">
        /// Queue that collects effects reaching duration 0. Must not be null.
        /// The caller is responsible for flushing it after all phases of a tick complete.
        /// </param>
        public EffectTimingPipeline(
            EffectExpirationQueue expirationQueue,
            EffectMutationFactory mutationFactory)
        {
            _expirationQueue = expirationQueue ?? throw new ArgumentNullException(nameof(expirationQueue));
            _mutationFactory = mutationFactory ?? throw new ArgumentNullException(nameof(mutationFactory));
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Executes all effects in <paramref name="activeEffects"/> that belong to
        /// <paramref name="phase"/> in deterministic order, then ticks down duration for
        /// <see cref="EffectTimingPhase.OnTickEnd"/> effects and enqueues any that expire.
        /// </summary>
        /// <param name="phase">The timing phase to evaluate.</param>
        /// <param name="currentTick">Current simulation tick.</param>
        /// <param name="activeEffects">
        /// Mutable dictionary of all currently active effects, keyed by their runtime key.
        /// Effects are not removed from the dictionary here; removal is deferred to
        /// <see cref="EffectExpirationQueue.Flush"/>.
        /// </param>
        /// <param name="processorResolver">
        /// Delegate that returns the <see cref="IEffectProcessor"/> for a given effect,
        /// or <c>null</c> if none is registered.
        /// </param>
        /// <param name="effectContext">
        /// The <see cref="EffectSystemContext"/> forwarded to processor callbacks.
        /// </param>
        /// <param name="actionId">
        /// Optional action ID to embed in the <see cref="EffectTimingContext"/>; use
        /// <see cref="Guid.Empty"/> for tick-driven phases.
        /// </param>
        /// <returns>
        /// A read-only list of (effectKey, deltaHp) pairs produced during
        /// <see cref="EffectTimingPhase.OnTickEnd"/> ticks, in execution order.
        /// For all other phases the list is empty.
        /// </returns>
        public EffectPhaseResult RunPhase(
            EffectTimingPhase phase,
            int currentTick,
            IReadOnlyDictionary<string, EffectRuntimeState> activeEffects,
            Func<IReadOnlyEffectRuntimeState, IEffectProcessor> processorResolver,
            EffectSystemContext effectContext,
            Guid actionId = default)
        {
            if (activeEffects == null) throw new ArgumentNullException(nameof(activeEffects));
            if (processorResolver == null) throw new ArgumentNullException(nameof(processorResolver));

            var sorted = BuildSortedPhaseList(phase, activeEffects);
            if (sorted.Count == 0)
                return EffectPhaseResult.Empty;

            var tickResults = new List<EffectTickResult>();
            var queuedMutations = new List<QueuedMutation>();

            for (int i = 0; i < sorted.Count; i++)
            {
                (string key, EffectRuntimeState effect) = sorted[i];

                // Reject reaction effects outside OnPostResolve.
                if (effect.IsReaction && phase != EffectTimingPhase.OnPostResolve)
                {
                    Debug.LogWarning(
                        $"[EffectTimingPipeline] Reaction effect '{effect.EffectId}' skipped: " +
                        $"reactions are only valid in OnPostResolve (current phase: {phase}).");
                    continue;
                }

                var timingContext = new EffectTimingContext(
                    currentTick,
                    phase,
                    effect.SourceId,
                    effect.TargetId,
                    actionId);

                IEffectProcessor processor = processorResolver(effect);

                if (phase == EffectTimingPhase.OnTickEnd)
                {
                    // Duration countdown is exclusively performed in OnTickEnd.
                    effect.AdvanceTick(OwnershipOwners.EffectSystem);

                    if (processor != null && effect.NextTickIn <= 0)
                    {
                        effect.TransitionLifecycle(OwnershipOwners.EffectSystem, EffectLifecycle.Ticking);
                        EffectMutationContext mutationContext = effectContext.CreateMutationContext(
                            effect,
                            currentTick,
                            $"{phase}:{effect.EffectId}");
                        EffectProcessorResult result = processor.OnTick(
                            effectContext,
                            mutationContext,
                            _mutationFactory,
                            effect);
                        EnqueueMutations(queuedMutations, effect, result.Mutations, i);
                        effect.ResetTickCountdown(OwnershipOwners.EffectSystem);
                        tickResults.Add(new EffectTickResult(key, effect, result.DeltaHp, timingContext));
                    }

                    if (effect.IsExpired)
                    {
                        effect.TransitionLifecycle(OwnershipOwners.EffectSystem, EffectLifecycle.Expired);
                        processor?.OnExpired(effectContext, effect);
                        _expirationQueue.Enqueue(key);
                    }
                    else if (effect.Lifecycle == EffectLifecycle.Ticking)
                    {
                        effect.TransitionLifecycle(OwnershipOwners.EffectSystem, EffectLifecycle.Active);
                    }
                }
                else
                {
                    // Non-tick phases trigger OnApplied for freshly applied effects or
                    // OnTick-equivalent logic supplied by the processor.
                    if (processor != null)
                    {
                        EffectMutationContext mutationContext = effectContext.CreateMutationContext(
                            effect,
                            currentTick,
                            $"{phase}:{effect.EffectId}");
                        EffectProcessorResult result = processor.OnTick(
                            effectContext,
                            mutationContext,
                            _mutationFactory,
                            effect);
                        EnqueueMutations(queuedMutations, effect, result.Mutations, i);
                        if (result.DeltaHp != 0)
                            tickResults.Add(new EffectTickResult(key, effect, result.DeltaHp, timingContext));
                    }
                }
            }

            return new EffectPhaseResult(queuedMutations, tickResults);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static List<(string Key, EffectRuntimeState Effect)> BuildSortedPhaseList(
            EffectTimingPhase phase,
            IReadOnlyDictionary<string, EffectRuntimeState> activeEffects)
        {
            var list = new List<(string Key, EffectRuntimeState Effect)>();

            foreach ((string key, EffectRuntimeState effect) in activeEffects)
            {
                if (effect == null || effect.TimingPhase != phase)
                    continue;

                list.Add((key, effect));
            }

            if (list.Count > 1)
            {
                list.Sort((a, b) =>
                    EffectTimingOrderComparer.Default.Compare(
                        (a.Key, a.Effect),
                        (b.Key, b.Effect)));
            }

            return list;
        }

        private static void EnqueueMutations(
            ICollection<QueuedMutation> queuedMutations,
            IReadOnlyEffectRuntimeState effect,
            IReadOnlyList<IRuntimeMutation> mutations,
            int resolveOrder)
        {
            if (queuedMutations == null || mutations == null || mutations.Count == 0)
                return;

            ActionSpeedTier speed = effect?.ActionSpeedLevel ?? ActionSpeedTier.Normal;
            for (int i = 0; i < mutations.Count; i++)
            {
                if (mutations[i] != null)
                    queuedMutations.Add(new QueuedMutation(mutations[i], speed, resolveOrder, i));
            }
        }
    }

    public readonly record struct EffectPhaseResult(
        IReadOnlyList<QueuedMutation> QueuedMutations,
        IReadOnlyList<EffectTickResult> TickResults)
    {
        public static EffectPhaseResult Empty =>
            new(Array.Empty<QueuedMutation>(), Array.Empty<EffectTickResult>());
    }

    /// <summary>
    /// Carries the result of a single effect evaluation during a pipeline phase.
    /// </summary>
    public readonly struct EffectTickResult
    {
        public EffectTickResult(
            string effectKey,
            IReadOnlyEffectRuntimeState effect,
            int deltaHp,
            EffectTimingContext context)
        {
            EffectKey = effectKey;
            Effect = effect;
            DeltaHp = deltaHp;
            Context = context;
        }

        /// <summary>Runtime dictionary key of the evaluated effect.</summary>
        public string EffectKey { get; }

        /// <summary>Snapshot of the effect state at the time of evaluation.</summary>
        public IReadOnlyEffectRuntimeState Effect { get; }

        /// <summary>HP delta produced by the effect this step (negative = damage, positive = heal).</summary>
        public int DeltaHp { get; }

        /// <summary>The timing context active when the effect was evaluated.</summary>
        public EffectTimingContext Context { get; }
    }
}
