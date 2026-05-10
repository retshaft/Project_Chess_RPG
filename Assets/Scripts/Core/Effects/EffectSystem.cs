using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Events.EffectEvents;
using CheckmateRPG.Core.Runtime.Ownership;
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
        private readonly List<EffectRuntimeState> _activeEffects = new();
        private readonly List<IEffectProcessor> _processors = new();

        public EffectSystem(IEventBus eventBus, Func<Guid, UnitBrain> unitResolver)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _context = new EffectSystemContext(unitResolver);
        }

        public void RegisterProcessor(IEffectProcessor processor)
        {
            if (processor == null || _processors.Contains(processor))
                return;

            _processors.Add(processor);
        }

        public bool ApplyOrRefreshEffect(EffectRuntimeState state)
        {
            if (state == null ||
                string.IsNullOrWhiteSpace(state.EffectId) ||
                state.TargetId == Guid.Empty ||
                state.RemainingTick <= 0)
            {
                return false;
            }

            EffectRuntimeState runtimeState = FindOrCreate(state);
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
            _ = schedulerTick;
            if (_activeEffects.Count == 0)
                return;

            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                EffectRuntimeState effect = _activeEffects[i];
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
                _activeEffects.RemoveAt(i);
            }
        }

        public IReadOnlyDictionary<string, EffectRuntimeState> CreateRuntimeSnapshot()
        {
            var snapshot = new SortedDictionary<string, EffectRuntimeState>(StringComparer.Ordinal);
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                EffectRuntimeState effect = _activeEffects[i];
                if (effect == null)
                    continue;

                snapshot[BuildSnapshotKey(effect)] = new EffectRuntimeState(effect);
            }

            return snapshot;
        }

        private EffectRuntimeState FindOrCreate(EffectRuntimeState requested)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                EffectRuntimeState active = _activeEffects[i];
                if (active.EffectId == requested.EffectId && active.TargetId == requested.TargetId)
                    return active;
            }

            var created = new EffectRuntimeState(
                requested.EffectId,
                requested.SourceId,
                requested.TargetId,
                requested.RemainingTick,
                requested.StackCount,
                requested.TickInterval,
                requested.NextTickIn,
                requested.Magnitude);

            _activeEffects.Add(created);
            return created;
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
            return $"{effect.TargetId:N}:{effect.EffectId}";
        }
    }
}
