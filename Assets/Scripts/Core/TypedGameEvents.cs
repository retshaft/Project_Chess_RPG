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
        Combat,
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

        public IResolvableGameEvent WithPhase(EventPhase phase) => this with { Phase = phase };
        public IResolvableGameEvent WithQueueMetadata(int eventDepth, long queueOrder) =>
            this with { EventDepth = eventDepth, QueueOrder = queueOrder };
    }

    public readonly record struct MoveStartedPayload(int UnitId, int FromX, int FromY, int ToX, int ToY);
    public readonly record struct MoveCompletedPayload(int UnitId, int FromX, int FromY, int ToX, int ToY);
    public readonly record struct AttackResolvedPayload(int AttackerUnitId, int TargetUnitId, int DamageAmount, bool IsCritical);
    public readonly record struct UnitDamagedPayload(int UnitId, int Amount, int RemainingHealth, int SourceUnitId, bool IsCritical);
    public readonly record struct AbilityResolvedPayload(int CasterUnitId, string AbilityId, int PrimaryTargetUnitId, bool WasSuccessful);
    public readonly record struct EffectAppliedPayload(int UnitId, string EffectId, int DurationTicks, int SourceUnitId);
    public readonly record struct UnitKilledPayload(int UnitId, int KillerUnitId);
}
