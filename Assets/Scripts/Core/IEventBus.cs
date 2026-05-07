using System;

namespace CheckmateRPG.Core
{
    public interface IEventBus
    {
        void Publish<TEvent>(TEvent gameEvent) where TEvent : class, IGameEvent;
        void ProcessQueue();
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class, IGameEvent;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class, IGameEvent;
        void Subscribe(Type eventBaseType, Action<IGameEvent> handler);
        void Unsubscribe(Type eventBaseType, Action<IGameEvent> handler);
        void SubscribeAll(Action<IGameEvent> handler);
        void UnsubscribeAll(Action<IGameEvent> handler);
    }
}
