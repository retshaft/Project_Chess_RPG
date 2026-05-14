using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public sealed class EventBus : IEventBus
    {
        public const int MaxEventDepth = 32;

        private readonly object _lock = new object();
        private readonly Dictionary<Type, List<Action<IGameEvent>>> _subscribers = new Dictionary<Type, List<Action<IGameEvent>>>();
        private readonly Dictionary<Delegate, Action<IGameEvent>> _handlerWrappers = new Dictionary<Delegate, Action<IGameEvent>>();
        private readonly Dictionary<Type, List<Action<IGameEvent>>> _baseTypeSubscribers = new Dictionary<Type, List<Action<IGameEvent>>>();
        private readonly List<Action<IGameEvent>> _globalSubscribers = new List<Action<IGameEvent>>();
        private readonly Queue<IResolvableGameEvent> _eventQueue = new Queue<IResolvableGameEvent>();
        private static readonly EventPhase[] ResolvePhases =
        {
            EventPhase.PreResolve,
            EventPhase.Resolve,
            EventPhase.PostResolve,
            EventPhase.Cleanup
        };

        private bool _isProcessing;
        private int _currentEventDepth;
        private Guid _currentReactionChainId;
        private long _nextQueueOrder;

        public bool EnableEventTrace { get; set; }

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : class, IGameEvent
        {
            if (gameEvent == null)
                throw new ArgumentNullException(nameof(gameEvent));

            if (!(gameEvent is IResolvableGameEvent resolvableEvent))
                throw new InvalidOperationException($"Event {typeof(TEvent).Name} must implement {nameof(IResolvableGameEvent)}.");

            int nextDepth = _currentEventDepth + 1;
            if (nextDepth > MaxEventDepth)
            {
                Debug.LogError($"[EventBus] MaxEventDepth({MaxEventDepth}) exceeded while publishing {typeof(TEvent).Name}. Event ignored.");
                return;
            }

            Guid nextReactionChainId =
                _currentReactionChainId != Guid.Empty
                    ? _currentReactionChainId
                    : Guid.NewGuid();

            IResolvableGameEvent queuedEvent;
            lock (_lock)
            {
                queuedEvent = resolvableEvent.WithQueueMetadata(nextDepth, ++_nextQueueOrder, nextReactionChainId);
                _eventQueue.Enqueue(queuedEvent);
            }

            Trace(queuedEvent, "Enqueue");
        }

        public void ProcessQueue()
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
            try
            {
                while (true)
                {
                    IResolvableGameEvent queuedEvent;

                    lock (_lock)
                    {
                        if (_eventQueue.Count == 0)
                            break;

                        queuedEvent = _eventQueue.Dequeue();
                    }

                    int previousDepth = _currentEventDepth;
                    Guid previousReactionChainId = _currentReactionChainId;
                    _currentEventDepth = queuedEvent.EventDepth;
                    _currentReactionChainId = queuedEvent.ReactionChainId;
                    try
                    {
                        foreach (EventPhase phase in ResolvePhases)
                        {
                            Dispatch(queuedEvent.WithPhase(phase));
                        }
                    }
                    finally
                    {
                        _currentEventDepth = previousDepth;
                        _currentReactionChainId = previousReactionChainId;
                    }
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class, IGameEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Type eventType = typeof(TEvent);
            Action<IGameEvent> wrapper = gameEvent => handler((TEvent)gameEvent);

            lock (_lock)
            {
                if (!_subscribers.TryGetValue(eventType, out List<Action<IGameEvent>> list))
                {
                    list = new List<Action<IGameEvent>>();
                    _subscribers[eventType] = list;
                }

                if (_handlerWrappers.ContainsKey(handler))
                    return;

                _handlerWrappers[handler] = wrapper;
                list.Add(wrapper);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class, IGameEvent
        {
            if (handler == null)
                return;

            Type eventType = typeof(TEvent);

            lock (_lock)
            {
                if (!_subscribers.TryGetValue(eventType, out List<Action<IGameEvent>> list))
                    return;
                if (!_handlerWrappers.TryGetValue(handler, out Action<IGameEvent> wrapper))
                    return;

                list.Remove(wrapper);
                _handlerWrappers.Remove(handler);
                if (list.Count == 0)
                    _subscribers.Remove(eventType);
            }
        }

        public void Subscribe(Type eventBaseType, Action<IGameEvent> handler)
        {
            if (eventBaseType == null)
                throw new ArgumentNullException(nameof(eventBaseType));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            if (!typeof(IGameEvent).IsAssignableFrom(eventBaseType))
                throw new ArgumentException($"Type must implement {nameof(IGameEvent)}.", nameof(eventBaseType));

            lock (_lock)
            {
                if (!_baseTypeSubscribers.TryGetValue(eventBaseType, out List<Action<IGameEvent>> list))
                {
                    list = new List<Action<IGameEvent>>();
                    _baseTypeSubscribers[eventBaseType] = list;
                }

                if (!list.Contains(handler))
                    list.Add(handler);
            }
        }

        public void Unsubscribe(Type eventBaseType, Action<IGameEvent> handler)
        {
            if (eventBaseType == null || handler == null)
                return;

            lock (_lock)
            {
                if (!_baseTypeSubscribers.TryGetValue(eventBaseType, out List<Action<IGameEvent>> list))
                    return;

                list.Remove(handler);
                if (list.Count == 0)
                    _baseTypeSubscribers.Remove(eventBaseType);
            }
        }

        public void SubscribeAll(Action<IGameEvent> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (!_globalSubscribers.Contains(handler))
                    _globalSubscribers.Add(handler);
            }
        }

        public void UnsubscribeAll(Action<IGameEvent> handler)
        {
            if (handler == null)
                return;

            lock (_lock)
            {
                _globalSubscribers.Remove(handler);
            }
        }

        private void Dispatch(IResolvableGameEvent gameEvent)
        {
            List<Action<IGameEvent>> typedHandlers = null;
            List<Action<IGameEvent>> baseHandlers = null;
            List<Action<IGameEvent>> globalHandlers = null;
            Type eventType = gameEvent.GetType();

            lock (_lock)
            {
                if (_subscribers.TryGetValue(eventType, out List<Action<IGameEvent>> typedList) && typedList.Count > 0)
                    typedHandlers = new List<Action<IGameEvent>>(typedList);

                if (_baseTypeSubscribers.Count > 0)
                {
                    baseHandlers = new List<Action<IGameEvent>>();
                    foreach (KeyValuePair<Type, List<Action<IGameEvent>>> entry in _baseTypeSubscribers)
                    {
                        if (entry.Key.IsAssignableFrom(eventType) && entry.Value.Count > 0)
                            baseHandlers.AddRange(entry.Value);
                    }
                }

                if (_globalSubscribers.Count > 0)
                    globalHandlers = new List<Action<IGameEvent>>(_globalSubscribers);
            }

            Trace(gameEvent, "Dispatch");

            if (typedHandlers != null)
            {
                foreach (Action<IGameEvent> handler in typedHandlers)
                {
                    handler.Invoke(gameEvent);
                }
            }

            if (baseHandlers != null)
            {
                foreach (Action<IGameEvent> handler in baseHandlers)
                {
                    handler.Invoke(gameEvent);
                }
            }

            if (globalHandlers != null)
            {
                foreach (Action<IGameEvent> handler in globalHandlers)
                {
                    handler.Invoke(gameEvent);
                }
            }
        }

        private void Trace(IResolvableGameEvent gameEvent, string stage)
        {
            if (!EnableEventTrace)
                return;

            Debug.Log(
                $"[EventBus][{stage}] Type={gameEvent.GetType().Name} Phase={gameEvent.Phase} " +
                $"Category={gameEvent.Category} Timestamp={gameEvent.Timestamp:O} QueueOrder={gameEvent.QueueOrder} " +
                $"Source={gameEvent.Source} Target={gameEvent.Target} Chain={gameEvent.ReactionChainId:N}");
        }
    }
}
