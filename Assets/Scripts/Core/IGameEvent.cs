using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    public interface IGameEvent
    {
        string EventId { get; }
        DateTime Timestamp { get; }
        object Source { get; }
        object Target { get; }
        IReadOnlyCollection<string> Tags { get; }
        object Payload { get; }
    }
}
