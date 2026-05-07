using System;

namespace CheckmateRPG.Core
{
    public interface IEventBus
    {
        void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent;
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;
    }
}
