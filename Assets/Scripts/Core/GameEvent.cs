using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    public sealed class GameEvent : IGameEvent
    {
        private static readonly IReadOnlyCollection<string> EmptyTags = Array.Empty<string>();

        public string EventId { get; }
        public DateTime Timestamp { get; }
        public object Source { get; }
        public object Target { get; }
        public IReadOnlyCollection<string> Tags { get; }
        public object Payload { get; }

        public GameEvent(
            string eventId,
            object source = null,
            object target = null,
            IEnumerable<string> tags = null,
            object payload = null,
            DateTime? timestamp = null)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                throw new ArgumentException("EventId must be a non-empty string.", nameof(eventId));

            EventId = eventId;
            Timestamp = timestamp ?? DateTime.UtcNow;
            Source = source;
            Target = target;
            Tags = tags != null ? new List<string>(tags) : EmptyTags;
            Payload = payload;
        }
    }
}
