using System;
using System.Collections.Generic;
using System.Reflection;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Events.EffectEvents;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core
{
    public sealed class SimulationTimelineRecorder
    {
        private readonly ReplayRecorder _replayRecorder;
        private readonly Func<int> _tickProvider;
        private readonly List<SimulationTimelineEntry> _entries = new();
        private IEventBus _eventBus;
        private bool _attached;
        private long _nextSequence;

        public SimulationTimelineRecorder(ReplayRecorder replayRecorder, Func<int> tickProvider)
        {
            _replayRecorder = replayRecorder;
            _tickProvider = tickProvider ?? throw new ArgumentNullException(nameof(tickProvider));
        }

        public void Attach(IEventBus eventBus)
        {
            if (_attached || eventBus == null)
                return;

            _eventBus = eventBus;
            _eventBus.SubscribeAll(HandleEvent);
            _attached = true;
        }

        public void Detach()
        {
            if (!_attached || _eventBus == null)
                return;

            _eventBus.UnsubscribeAll(HandleEvent);
            _eventBus = null;
            _attached = false;
        }

        public void RecordTick(int tick)
        {
            AddEntry(tick, SimulationTimelineEntryType.Tick, "TickAdvance", $"Tick={tick}");
        }

        public void RecordAction(string actionTrace)
        {
            if (string.IsNullOrWhiteSpace(actionTrace))
                return;

            AddEntry(_tickProvider(), SimulationTimelineEntryType.Action, "QueuedAction", actionTrace);
        }

        public void RecordMutations(IReadOnlyList<IRuntimeMutation> mutations, string stage)
        {
            if (mutations == null || mutations.Count == 0)
                return;

            string stageName = string.IsNullOrWhiteSpace(stage) ? "MutationStage" : stage;
            int tick = _tickProvider();
            for (int i = 0; i < mutations.Count; i++)
            {
                IRuntimeMutation mutation = mutations[i];
                if (mutation == null)
                    continue;

                string label = $"{stageName}:{mutation.GetType().Name}";
                AddEntry(tick, SimulationTimelineEntryType.Mutation, label, BuildMutationTrace(mutation));
            }
        }

        public IReadOnlyList<SimulationTimelineEntry> GetEntries()
        {
            return new List<SimulationTimelineEntry>(_entries);
        }

        public void Clear()
        {
            _entries.Clear();
            _nextSequence = 0;
        }

        private void HandleEvent(IGameEvent gameEvent)
        {
            if (gameEvent == null)
                return;

            SimulationTimelineEntryType type = ResolveEntryType(gameEvent);
            string detail = BuildEventTrace(gameEvent);
            AddEntry(_tickProvider(), type, gameEvent.GetType().Name, detail, gameEvent.Source, gameEvent.Target);
        }

        private void AddEntry(
            int tick,
            SimulationTimelineEntryType type,
            string label,
            string detail,
            string source = "",
            string target = "")
        {
            int safeTick = Math.Max(0, tick);
            var entry = SimulationTimelineEntry.Create(
                safeTick,
                ++_nextSequence,
                type,
                label,
                detail,
                source,
                target);

            _entries.Add(entry);
            _replayRecorder?.RecordTimelineEntry(safeTick, entry);
        }

        private static SimulationTimelineEntryType ResolveEntryType(IGameEvent gameEvent)
        {
            if (gameEvent is ActionInterruptedEvent)
                return SimulationTimelineEntryType.Interrupt;
            if (gameEvent is EffectAppliedEvent or EffectTickEvent or EffectExpiredEvent)
                return SimulationTimelineEntryType.Effect;
            return SimulationTimelineEntryType.Event;
        }

        private static string BuildEventTrace(IGameEvent gameEvent)
        {
            if (gameEvent is IResolvableGameEvent resolvable)
            {
                return
                    $"Phase={resolvable.Phase}|Category={resolvable.Category}|Depth={resolvable.EventDepth}|Order={resolvable.QueueOrder}" +
                    $"|Payload={TryGetPayloadString(gameEvent)}";
            }

            return $"Payload={TryGetPayloadString(gameEvent)}";
        }

        private static string BuildMutationTrace(IRuntimeMutation mutation)
        {
            return mutation switch
            {
                DamageMutation damage =>
                    $"Id={damage.MutationId:N}|Target={damage.TargetId:N}|Source={damage.SourceId:N}|Amount={damage.Amount}|Critical={damage.IsCritical}",
                MovementMutation move =>
                    $"Id={move.MutationId:N}|Target={move.TargetId:N}|From=({move.From.x},{move.From.y})|To=({move.To.x},{move.To.y})",
                ApplyEffectMutation effect =>
                    $"Id={effect.MutationId:N}|Effect={effect.EffectId}|Source={effect.SourceId:N}|Target={effect.TargetId:N}|Duration={effect.DurationTicks}|Interval={effect.TickInterval}|Initial={effect.InitialTickIn}|Stacks={effect.StackCount}|Magnitude={effect.Magnitude}",
                _ =>
                    $"Id={mutation.MutationId:N}|Target={mutation.TargetId:N}|Data={mutation}"
            };
        }

        private static string TryGetPayloadString(IGameEvent gameEvent)
        {
            PropertyInfo payloadProperty = gameEvent.GetType().GetProperty("Payload", BindingFlags.Instance | BindingFlags.Public);
            if (payloadProperty == null)
                return string.Empty;

            object payload = payloadProperty.GetValue(gameEvent);
            return payload?.ToString() ?? string.Empty;
        }
    }
}
