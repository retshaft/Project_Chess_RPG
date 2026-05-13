using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Events.EffectEvents;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Effects
{
    public readonly struct EffectSystemContext
    {
        private readonly Func<Guid, UnitBrain> _unitResolver;

        public EffectSystemContext(Func<Guid, UnitBrain> unitResolver)
        {
            _unitResolver = unitResolver;
        }

        public bool TryGetUnit(Guid unitId, out UnitBrain unit)
        {
            unit = _unitResolver != null ? _unitResolver(unitId) : null;
            return unit != null;
        }
    }

    public sealed class EffectSystem
    {
        private readonly IEventBus _eventBus;
        private readonly EffectSystemContext _context;
        private readonly Func<SimulationRuntime> _runtimeProvider;
        private readonly List<IEffectProcessor> _processors = new();
        private readonly EffectExpirationQueue _expirationQueue = new();
        private readonly EffectTimingPipeline _timingPipeline;

        public EffectSystem(IEventBus eventBus, Func<Guid, UnitBrain> unitResolver, Func<SimulationRuntime> runtimeProvider)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _context = new EffectSystemContext(unitResolver);
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            _timingPipeline = new EffectTimingPipeline(_expirationQueue);
        }

        public void RegisterProcessor(IEffectProcessor processor)
        {
            if (processor == null || _processors.Contains(processor))
                return;

            _processors.Add(processor);
        }

        public bool ApplyOrRefreshEffect(EffectRuntimeState state)
        {
            SimulationRuntime runtime = GetRuntime();
            if (state == null ||
                string.IsNullOrWhiteSpace(state.EffectId) ||
                state.TargetId == Guid.Empty ||
                state.RemainingTick <= 0)
            {
                return false;
            }

            EffectRuntimeState runtimeState = FindOrCreate(runtime, state);
            runtimeState.RefreshFromApplication(
                state.SourceId,
                state.StackCount,
                state.RemainingTick,
                state.TickInterval,
                state.NextTickIn,
                state.Magnitude,
                OwnershipOwners.EffectSystem);

            IEffectProcessor processor = ResolveProcessor(runtimeState);
            if (processor == null)
            {
                Debug.LogWarning($"[EffectSystem] No processor registered for effect '{runtimeState.EffectId}'.");
            }
            processor?.OnApplied(_context, runtimeState);
            runtimeState.TransitionLifecycle(OwnershipOwners.EffectSystem, EffectLifecycle.Active);

            PublishApplied(runtimeState);
            return true;
        }

        /// <summary>
        /// Advances all active effects for one tick.
        /// Duration countdown and DOT/HOT ticks are routed exclusively through
        /// <see cref="EffectTimingPhase.OnTickEnd"/> via <see cref="EffectTimingPipeline"/>.
        /// Expired effects are collected in <see cref="EffectExpirationQueue"/> and removed
        /// after all tick processing is complete.
        /// </summary>
        public void AdvanceTick(int schedulerTick)
        {
            SimulationRuntime runtime = GetRuntime();
            runtime.SetCurrentTick(schedulerTick);
            if (runtime.ActiveEffects.Count == 0)
                return;

            // Build a snapshot of the mutable effect dictionary for pipeline consumption.
            var mutableSnapshot = BuildMutableEffectSnapshot(runtime);

            // Run only the OnTickEnd phase here; other phases are invoked by the
            // ResolutionPhasePipeline at the appropriate moment.
            IReadOnlyList<EffectTickResult> tickResults = _timingPipeline.RunPhase(
                EffectTimingPhase.OnTickEnd,
                schedulerTick,
                mutableSnapshot,
                ResolveProcessor,
                _context);

            // Publish tick events for each effect that fired this tick.
            for (int i = 0; i < tickResults.Count; i++)
            {
                EffectTickResult result = tickResults[i];
                PublishTick(result.Effect, result.DeltaHp);
            }

            // Flush expired effects – deferred removal guarantees that no effect is
            // removed inline while the pipeline is still iterating.
            _expirationQueue.Flush(effectKey =>
            {
                if (runtime.TryGetMutableEffect(effectKey, out EffectRuntimeState expired))
                {
                    expired.TransitionLifecycle(OwnershipOwners.EffectSystem, EffectLifecycle.Removed);
                    PublishExpired(expired);
                }

                runtime.UnregisterEffect(effectKey);
            });
        }

        /// <summary>
        /// Runs a single non-OnTickEnd timing phase for all matching active effects.
        /// Intended to be called by the <c>ResolutionPhasePipeline</c> at the correct moment.
        /// </summary>
        /// <param name="phase">The phase to run. Must not be <see cref="EffectTimingPhase.OnTickEnd"/>.</param>
        /// <param name="schedulerTick">Current simulation tick.</param>
        /// <param name="actionId">Action that triggered this phase (may be <see cref="Guid.Empty"/>).</param>
        public IReadOnlyList<EffectTickResult> RunPhase(
            EffectTimingPhase phase,
            int schedulerTick,
            Guid actionId = default)
        {
            if (phase == EffectTimingPhase.OnTickEnd)
                throw new ArgumentException(
                    "OnTickEnd is driven by AdvanceTick. Use AdvanceTick() instead.",
                    nameof(phase));

            SimulationRuntime runtime = GetRuntime();
            if (runtime.ActiveEffects.Count == 0)
                return Array.Empty<EffectTickResult>();

            var mutableSnapshot = BuildMutableEffectSnapshot(runtime);

            IReadOnlyList<EffectTickResult> results = _timingPipeline.RunPhase(
                phase,
                schedulerTick,
                mutableSnapshot,
                ResolveProcessor,
                _context,
                actionId);

            // Flush any expiration requests that may have arisen during non-tick phases.
            _expirationQueue.Flush(effectKey =>
            {
                if (runtime.TryGetMutableEffect(effectKey, out EffectRuntimeState expired))
                {
                    expired.TransitionLifecycle(OwnershipOwners.EffectSystem, EffectLifecycle.Removed);
                    PublishExpired(expired);
                }

                runtime.UnregisterEffect(effectKey);
            });

            return results;
        }

        public IReadOnlyDictionary<string, EffectRuntimeState> CreateRuntimeSnapshot()
        {
            SimulationRuntime runtime = GetRuntime();
            var snapshot = new SortedDictionary<string, EffectRuntimeState>(StringComparer.Ordinal);
            foreach (string effectKey in runtime.ActiveEffects.Keys)
            {
                if (!runtime.TryGetMutableEffect(effectKey, out EffectRuntimeState effect) || effect == null)
                    continue;

                snapshot[effectKey] = new EffectRuntimeState(effect);
            }

            return snapshot;
        }

        private EffectRuntimeState FindOrCreate(SimulationRuntime runtime, EffectRuntimeState requested)
        {
            string effectKey = BuildSnapshotKey(requested);
            if (runtime.TryGetMutableEffect(effectKey, out EffectRuntimeState active))
                return active;

            var created = new EffectRuntimeState(
                requested.EffectId,
                requested.SourceId,
                requested.TargetId,
                requested.RemainingTick,
                requested.StackCount,
                requested.TickInterval,
                requested.NextTickIn,
                requested.Magnitude,
                requested.TimingPhase,
                requested.ActionSpeedLevel,
                requested.IsReaction,
                runtime.CurrentTick);

            runtime.RegisterEffect(effectKey, created);
            if (!runtime.TryGetMutableEffect(effectKey, out EffectRuntimeState runtimeEffect))
                return created;

            return runtimeEffect;
        }

        private SimulationRuntime GetRuntime()
        {
            SimulationRuntime runtime = _runtimeProvider();
            if (runtime == null)
                throw new InvalidOperationException("SimulationRuntime provider returned null.");

            return runtime;
        }

        private IEffectProcessor ResolveProcessor(IReadOnlyEffectRuntimeState effect)
        {
            for (int i = 0; i < _processors.Count; i++)
            {
                IEffectProcessor processor = _processors[i];
                if (processor != null && processor.CanProcess(effect))
                    return processor;
            }

            return null;
        }

        private static Dictionary<string, EffectRuntimeState> BuildMutableEffectSnapshot(
            SimulationRuntime runtime)
        {
            var snapshot = new Dictionary<string, EffectRuntimeState>(
                runtime.ActiveEffects.Count,
                StringComparer.Ordinal);

            foreach (string key in runtime.ActiveEffects.Keys)
            {
                if (runtime.TryGetMutableEffect(key, out EffectRuntimeState effect) && effect != null)
                    snapshot[key] = effect;
            }

            return snapshot;
        }

        private void PublishApplied(IReadOnlyEffectRuntimeState state)
        {
            _eventBus.Publish(new EffectAppliedEvent(
                new CheckmateRPG.Core.Events.EffectEvents.EffectAppliedPayload(
                    state.EffectId,
                    state.SourceId,
                    state.TargetId,
                    state.RemainingTick,
                    state.StackCount),
                state.SourceId.ToString("N"),
                state.TargetId.ToString("N")));
        }

        private void PublishTick(IReadOnlyEffectRuntimeState state, int deltaHp)
        {
            _eventBus.Publish(new EffectTickEvent(
                new EffectTickPayload(
                    state.EffectId,
                    state.SourceId,
                    state.TargetId,
                    state.RemainingTick,
                    state.StackCount,
                    deltaHp),
                state.SourceId.ToString("N"),
                state.TargetId.ToString("N")));
        }

        private void PublishExpired(IReadOnlyEffectRuntimeState state)
        {
            _eventBus.Publish(new EffectExpiredEvent(
                new EffectExpiredPayload(
                    state.EffectId,
                    state.SourceId,
                    state.TargetId,
                    state.StackCount),
                state.SourceId.ToString("N"),
                state.TargetId.ToString("N")));
        }

        private static string BuildSnapshotKey(EffectRuntimeState effect)
        {
            return SimulationRuntime.BuildEffectKey(effect);
        }
    }
}
