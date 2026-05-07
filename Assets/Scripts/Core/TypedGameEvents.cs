using System;

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
        }

        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
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

    public sealed record MoveStartedEvent(MoveStartedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<MoveStartedPayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record MoveCompletedEvent(MoveCompletedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<MoveCompletedPayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record AttackResolvedEvent(AttackResolvedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<AttackResolvedPayload>(Payload, EventCategory.Combat, Source, Target);

    public sealed record UnitDamagedEvent(UnitDamagedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<UnitDamagedPayload>(Payload, EventCategory.Combat, Source, Target);

    public sealed record AbilityResolvedEvent(AbilityResolvedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<AbilityResolvedPayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record EffectAppliedEvent(EffectAppliedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<EffectAppliedPayload>(Payload, EventCategory.Combat, Source, Target);

    public sealed record UnitKilledEvent(UnitKilledPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<UnitKilledPayload>(Payload, EventCategory.Combat, Source, Target);
}
