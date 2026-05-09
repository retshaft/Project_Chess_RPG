using System;
using System.Reflection;

namespace CheckmateRPG.Core
{
    public sealed class EventTraceRecorder
    {
        private readonly ReplayRecorder _replayRecorder;
        private readonly Func<int> _tickProvider;
        private IEventBus _eventBus;
        private bool _attached;

        public EventTraceRecorder(ReplayRecorder replayRecorder, Func<int> tickProvider)
        {
            _replayRecorder = replayRecorder ?? throw new ArgumentNullException(nameof(replayRecorder));
            _tickProvider = tickProvider ?? throw new ArgumentNullException(nameof(tickProvider));
        }

        public void Attach(IEventBus eventBus)
        {
            if (_attached)
                return;
            if (eventBus == null)
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

        private void HandleEvent(IGameEvent gameEvent)
        {
            if (gameEvent == null)
                return;

            int tick = Math.Max(0, _tickProvider());
            _replayRecorder.RecordEvent(tick, BuildTrace(gameEvent));
        }

        private static string BuildTrace(IGameEvent gameEvent)
        {
            if (gameEvent is IResolvableGameEvent resolvable)
            {
                return
                    $"{gameEvent.GetType().Name}|Phase={resolvable.Phase}|Category={resolvable.Category}" +
                    $"|Source={resolvable.Source}|Target={resolvable.Target}|Depth={resolvable.EventDepth}|Order={resolvable.QueueOrder}" +
                    $"|Payload={TryGetPayloadString(gameEvent)}";
            }

            return $"{gameEvent.GetType().Name}|Payload={TryGetPayloadString(gameEvent)}";
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
