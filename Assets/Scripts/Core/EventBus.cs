using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    public sealed class EventBus : IEventBus
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
        {
            if (gameEvent == null)
                throw new ArgumentNullException(nameof(gameEvent));

            Delegate[] handlers = null;
            Type eventType = typeof(TEvent);

            lock (_lock)
            {
                if (_subscribers.TryGetValue(eventType, out List<Delegate> list) && list.Count > 0)
                    handlers = list.ToArray();
            }

            if (handlers == null)
                return;

            foreach (Delegate handler in handlers)
            {
                if (handler is Action<TEvent> typedHandler)
                    typedHandler.Invoke(gameEvent);
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Type eventType = typeof(TEvent);

            lock (_lock)
            {
                if (!_subscribers.TryGetValue(eventType, out List<Delegate> list))
                {
                    list = new List<Delegate>();
                    _subscribers[eventType] = list;
                }

                if (!list.Contains(handler))
                    list.Add(handler);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null)
                return;

            Type eventType = typeof(TEvent);

            lock (_lock)
            {
                if (!_subscribers.TryGetValue(eventType, out List<Delegate> list))
                    return;

                list.Remove(handler);
                if (list.Count == 0)
                    _subscribers.Remove(eventType);
            }
        }
    }
}
