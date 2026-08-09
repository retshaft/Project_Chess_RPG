using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public enum EventPhase
    {
        PreResolve,
        Resolve,
        PostResolve,
        Cleanup
    }

    public enum EventCategory
    {
        Domain,
        Simulation,
        Presentation,
        Debug
    }

    public abstract record BaseGameEvent<TPayload> : IGameEvent<TPayload>
    {
        protected BaseGameEvent(TPayload payload, EventCategory category, string source = "", string target = "")
        {
            Payload = payload;
            Category = category;
            Source = source ?? string.Empty;
            Target = target ?? string.Empty;
            Timestamp = DateTime.UtcNow;
        }

        public DateTime Timestamp { get; init; }
        public TPayload Payload { get; init; }
        public EventPhase Phase { get; init; } = EventPhase.Resolve;
        public EventCategory Category { get; init; }
        public string Source { get; init; }
        public string Target { get; init; }
        public int EventDepth { get; init; }
        public long QueueOrder { get; init; }
        public Guid ReactionChainId { get; init; }

        public IResolvableGameEvent WithPhase(EventPhase phase) => this with { Phase = phase };
        public IResolvableGameEvent WithQueueMetadata(int eventDepth, long queueOrder, Guid reactionChainId) =>
            this with
            {
                EventDepth = eventDepth,
                QueueOrder = queueOrder,
                ReactionChainId = reactionChainId
            };
    }

    public readonly record struct MoveStartedPayload(Guid UnitId, int FromX, int FromY, int ToX, int ToY);
    public readonly record struct KingDiedPayload(bool IsPlayerKing);

    public sealed record MoveStartedEvent(MoveStartedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<MoveStartedPayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record KingDiedEvent(KingDiedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<KingDiedPayload>(Payload, EventCategory.Domain, Source, Target);
}
