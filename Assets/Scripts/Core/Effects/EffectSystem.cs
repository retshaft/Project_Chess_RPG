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

        public EffectSystem(IEventBus eventBus, Func<Guid, UnitBrain> unitResolver, Func<SimulationRuntime> runtimeProvider)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _context = new EffectSystemContext(unitResolver);
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
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

            PublishApplied(runtimeState);
            return true;
        }

        public void AdvanceTick(int schedulerTick)
        {
            SimulationRuntime runtime = GetRuntime();
            runtime.SetCurrentTick(schedulerTick);
            if (runtime.ActiveEffects.Count == 0)
                return;

            var effectKeys = new List<string>(runtime.ActiveEffects.Keys);
            for (int i = effectKeys.Count - 1; i >= 0; i--)
            {
                string effectKey = effectKeys[i];
                if (!runtime.TryGetEffect(effectKey, out EffectRuntimeState effect))
                    continue;

                effect.AdvanceTick(OwnershipOwners.EffectSystem);

                IEffectProcessor processor = ResolveProcessor(effect);
                if (processor != null && effect.NextTickIn <= 0)
                {
                    int deltaHp = processor.OnTick(_context, effect);
                    effect.ResetTickCountdown(OwnershipOwners.EffectSystem);
                    PublishTick(effect, deltaHp);
                }

                if (!effect.IsExpired)
                    continue;

                processor?.OnExpired(_context, effect);
                PublishExpired(effect);
                runtime.UnregisterEffect(effectKey);
            }
        }

        public IReadOnlyDictionary<string, EffectRuntimeState> CreateRuntimeSnapshot()
        {
            SimulationRuntime runtime = GetRuntime();
            var snapshot = new SortedDictionary<string, EffectRuntimeState>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, EffectRuntimeState> pair in runtime.ActiveEffects)
            {
                if (pair.Value == null)
                    continue;

                snapshot[pair.Key] = new EffectRuntimeState(pair.Value);
            }

            return snapshot;
        }

        private EffectRuntimeState FindOrCreate(SimulationRuntime runtime, EffectRuntimeState requested)
        {
            string effectKey = BuildSnapshotKey(requested);
            if (runtime.TryGetEffect(effectKey, out EffectRuntimeState active))
                return active;

            var created = new EffectRuntimeState(
                requested.EffectId,
                requested.SourceId,
                requested.TargetId,
                requested.RemainingTick,
                requested.StackCount,
                requested.TickInterval,
                requested.NextTickIn,
                requested.Magnitude);

            runtime.RegisterEffect(effectKey, created);
            if (!runtime.TryGetEffect(effectKey, out EffectRuntimeState runtimeEffect))
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

        private IEffectProcessor ResolveProcessor(EffectRuntimeState effect)
        {
            for (int i = 0; i < _processors.Count; i++)
            {
                IEffectProcessor processor = _processors[i];
                if (processor != null && processor.CanProcess(effect))
                    return processor;
            }

            return null;
        }

        private void PublishApplied(EffectRuntimeState state)
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

        private void PublishTick(EffectRuntimeState state, int deltaHp)
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

        private void PublishExpired(EffectRuntimeState state)
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
