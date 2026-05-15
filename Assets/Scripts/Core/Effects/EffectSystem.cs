using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Events.EffectEvents;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Effects
{
    public readonly struct EffectSystemContext
    {
        private readonly Func<Guid, UnitBrain> _unitResolver;
        private readonly Func<SimulationRuntime> _runtimeProvider;

        public EffectSystemContext(
            Func<Guid, UnitBrain> unitResolver,
            Func<SimulationRuntime> runtimeProvider)
        {
            _unitResolver = unitResolver;
            _runtimeProvider = runtimeProvider;
        }

        public bool TryGetUnit(Guid unitId, out UnitBrain unit)
        {
            unit = _unitResolver != null ? _unitResolver(unitId) : null;
            return unit != null;
        }

        public EffectMutationContext CreateMutationContext(
            IReadOnlyEffectRuntimeState sourceEffect,
            int tick,
            string reason)
        {
            EffectMutationUnitContext sourceUnit = BuildUnitContext(sourceEffect?.SourceId ?? Guid.Empty);
            EffectMutationUnitContext targetUnit = BuildUnitContext(sourceEffect?.TargetId ?? Guid.Empty);
            return new EffectMutationContext(
                sourceEffect,
                sourceUnit,
                targetUnit,
                tick,
                reason ?? string.Empty);
        }

        private EffectMutationUnitContext BuildUnitContext(Guid unitId)
        {
            if (unitId == Guid.Empty)
                return EffectMutationUnitContext.Empty();

            int currentHp = 0;
            Vector2Int position = default;
            UnitStatusFlags statusFlags = UnitStatusFlags.None;
            bool exists = false;

            SimulationRuntime runtime = _runtimeProvider != null ? _runtimeProvider() : null;
            if (runtime != null && runtime.TryGetUnit(unitId, out IReadOnlyUnitRuntimeState runtimeState) && runtimeState != null)
            {
                currentHp = runtimeState.HP;
                position = runtimeState.Position;
                statusFlags = runtimeState.StatusFlags;
                exists = true;
            }

            UnitBrain unit = _unitResolver != null ? _unitResolver(unitId) : null;
            int maxHp = ResolveMaxHp(unit, currentHp);
            if (!exists && unit != null)
            {
                currentHp = unit.Health != null ? Mathf.RoundToInt(unit.Health.CurrentHealth) : currentHp;
                position = unit.Movement != null ? unit.Movement.GridPosition : position;
                statusFlags = unit.IsDead ? UnitStatusFlags.Dead : statusFlags;
                exists = true;
            }

            return exists
                ? new EffectMutationUnitContext(unitId, currentHp, maxHp, position, statusFlags, true)
                : EffectMutationUnitContext.Empty(unitId);
        }

        private static int ResolveMaxHp(UnitBrain unit, int fallbackHp)
        {
            if (unit?.UnitData != null)
                return Mathf.Max(0, Mathf.RoundToInt(unit.UnitData.MaxHealth));
            if (unit?.Health != null)
                return Mathf.Max(0, Mathf.RoundToInt(unit.Health.MaxHealth));
            return Mathf.Max(0, fallbackHp);
        }
    }

    public sealed class EffectSystem
    {
        private const int TickPaddingWidth = 8;
        private const int CollisionIndexPaddingWidth = 4;

        private readonly IEventBus _eventBus;
        private readonly EffectSystemContext _context;
        private readonly Func<SimulationRuntime> _runtimeProvider;
        private readonly List<IEffectProcessor> _processors = new();
        private readonly EffectExpirationQueue _expirationQueue = new();
        private readonly EffectTimingPipeline _timingPipeline;
        private readonly EffectTickScheduler _tickScheduler;
        private readonly Dictionary<string, int> _applicationCountThisTick = new(StringComparer.Ordinal);
        private int _applicationCountTick = int.MinValue;

        public EffectSystem(IEventBus eventBus, Func<Guid, UnitBrain> unitResolver, Func<SimulationRuntime> runtimeProvider)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _context = new EffectSystemContext(unitResolver, runtimeProvider);
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            var mutationFactory = new EffectMutationFactory();
            _timingPipeline = new EffectTimingPipeline(_expirationQueue, mutationFactory);
            _tickScheduler = new EffectTickScheduler(_timingPipeline);
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

            if (!TryConsumePerTickApplicationQuota(state, runtime.CurrentTick))
                return false;

            EffectRuntimeState runtimeState = ApplyByStackPolicy(runtime, state);

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
        public IReadOnlyList<QueuedMutation> AdvanceTick(int schedulerTick)
        {
            SimulationRuntime runtime = GetRuntime();
            runtime.SetCurrentTick(schedulerTick);
            if (runtime.ActiveEffects.Count == 0)
                return Array.Empty<QueuedMutation>();

            // Build a snapshot of the mutable effect dictionary for pipeline consumption.
            var mutableSnapshot = BuildMutableEffectSnapshot(runtime);

            // Run only the OnTickEnd phase here; other phases are invoked by the
            // ResolutionPhasePipeline at the appropriate moment.
            EffectPhaseResult tickResult = _tickScheduler.ExecuteTick(
                schedulerTick,
                mutableSnapshot,
                ResolveProcessor,
                _context);

            // Publish tick events for each effect that fired this tick.
            for (int i = 0; i < tickResult.TickResults.Count; i++)
            {
                EffectTickResult result = tickResult.TickResults[i];
                PublishTick(result.Effect, result.DeltaHp, schedulerTick, i);
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

            return tickResult.QueuedMutations;
        }

        /// <summary>
        /// Runs a single non-OnTickEnd timing phase for all matching active effects.
        /// Intended to be called by the <c>ResolutionPhasePipeline</c> at the correct moment.
        /// </summary>
        /// <param name="phase">The phase to run. Must not be <see cref="EffectTimingPhase.OnTickEnd"/>.</param>
        /// <param name="schedulerTick">Current simulation tick.</param>
        /// <param name="actionId">Action that triggered this phase (may be <see cref="Guid.Empty"/>).</param>
        public EffectPhaseResult RunPhase(
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
                return EffectPhaseResult.Empty;

            var mutableSnapshot = BuildMutableEffectSnapshot(runtime);

            EffectPhaseResult results = _timingPipeline.RunPhase(
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

        private EffectRuntimeState ApplyByStackPolicy(SimulationRuntime runtime, EffectRuntimeState requested)
        {
            int appliedTick = runtime.CurrentTick;
            string baseKey = BuildBaseEffectKey(requested);

            if (requested.StackPolicy == EffectStackPolicy.Independent)
            {
                string independentKey = BuildIndependentEffectKey(runtime, requested, appliedTick);
                return CreateEffect(runtime, independentKey, requested, appliedTick);
            }

            if (!runtime.TryGetMutableEffect(baseKey, out EffectRuntimeState active))
                return CreateEffect(runtime, baseKey, requested, appliedTick);

            switch (requested.StackPolicy)
            {
                case EffectStackPolicy.Replace:
                    active.ReplaceFromApplication(
                        requested.SourceId,
                        requested.StackCount,
                        requested.RemainingTick,
                        requested.TickInterval,
                        requested.NextTickIn,
                        requested.Magnitude,
                        appliedTick,
                        requested.StackPolicy,
                        requested.MaxStackCap,
                        requested.MaxApplicationsPerTick,
                        OwnershipOwners.EffectSystem);
                    break;
                case EffectStackPolicy.MaxStackCap:
                    active.RefreshFromApplication(
                        requested.SourceId,
                        requested.StackCount,
                        requested.RemainingTick,
                        requested.TickInterval,
                        requested.NextTickIn,
                        requested.Magnitude,
                        requested.MaxStackCap,
                        requested.MaxApplicationsPerTick,
                        OwnershipOwners.EffectSystem);
                    break;
                case EffectStackPolicy.Refresh:
                default:
                    active.RefreshFromApplication(
                        requested.SourceId,
                        0,
                        requested.RemainingTick,
                        requested.TickInterval,
                        requested.NextTickIn,
                        requested.Magnitude,
                        requested.MaxStackCap,
                        requested.MaxApplicationsPerTick,
                        OwnershipOwners.EffectSystem);
                    break;
            }

            return active;
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

        private bool TryConsumePerTickApplicationQuota(EffectRuntimeState state, int currentTick)
        {
            if (_applicationCountTick != currentTick)
            {
                _applicationCountTick = currentTick;
                _applicationCountThisTick.Clear();
            }

            // Quota key is TargetId + EffectId (SimulationRuntime.BuildEffectKey),
            // so the cap is enforced per target/per effect type per tick.
            string effectKey = BuildBaseEffectKey(state);
            int maxApplicationsPerTick = EffectStackPolicyRules.ResolveMaxApplicationsPerTick(state.MaxApplicationsPerTick);
            int currentApplications = _applicationCountThisTick.TryGetValue(effectKey, out int count)
                ? count
                : 0;

            if (currentApplications >= maxApplicationsPerTick)
            {
                Debug.LogWarning(
                    $"[EffectSystem] Effect application skipped: MaxApplicationsPerTick({maxApplicationsPerTick}) reached for '{state.EffectId}' on '{state.TargetId:N}' at tick {currentTick}.");
                return false;
            }

            _applicationCountThisTick[effectKey] = currentApplications + 1;
            return true;
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

        private void PublishTick(IReadOnlyEffectRuntimeState state, int deltaHp, int schedulerTick, int tickIndex)
        {
            _eventBus.Publish(new EffectTickEvent(
                new EffectTickPayload(
                    state.EffectId,
                    state.SourceId,
                    state.TargetId,
                    schedulerTick,
                    tickIndex,
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

        private static EffectRuntimeState CreateEffect(
            SimulationRuntime runtime,
            string effectKey,
            EffectRuntimeState requested,
            int appliedTick)
        {
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
                appliedTick,
                requested.StackPolicy,
                requested.MaxStackCap,
                requested.MaxApplicationsPerTick);

            runtime.RegisterEffect(effectKey, created);
            if (!runtime.TryGetMutableEffect(effectKey, out EffectRuntimeState runtimeEffect))
                return created;

            return runtimeEffect;
        }

        private static string BuildBaseEffectKey(EffectRuntimeState effect)
        {
            return SimulationRuntime.BuildEffectKey(effect);
        }

        private static string BuildIndependentEffectKey(
            SimulationRuntime runtime,
            EffectRuntimeState requested,
            int appliedTick)
        {
            string baseKey = BuildBaseEffectKey(requested);
            string seed = $"{baseKey}:{requested.SourceId:N}:{appliedTick.ToString($"D{TickPaddingWidth}")}";
            string key = seed;
            int collisionIndex = 0;
            while (runtime.ActiveEffects.ContainsKey(key))
            {
                collisionIndex++;
                key = $"{seed}:{collisionIndex.ToString($"D{CollisionIndexPaddingWidth}")}";
            }

            return key;
        }
    }
}
