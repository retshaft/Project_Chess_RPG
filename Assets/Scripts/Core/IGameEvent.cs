using System;

namespace CheckmateRPG.Core
{
    public interface IGameEvent
    {
        DateTime Timestamp { get; }
    }

    public interface IResolvableGameEvent : IGameEvent
    {
        EventPhase Phase { get; }
        EventCategory Category { get; }
        string Source { get; }
        string Target { get; }
        int EventDepth { get; }
        long QueueOrder { get; }

        IResolvableGameEvent WithPhase(EventPhase phase);
        IResolvableGameEvent WithQueueMetadata(int eventDepth, long queueOrder);
    }

    public interface IGameEvent<out TPayload> : IResolvableGameEvent
    {
        TPayload Payload { get; }
    }
}
