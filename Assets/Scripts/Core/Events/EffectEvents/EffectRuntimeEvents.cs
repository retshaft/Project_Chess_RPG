using System;

namespace CheckmateRPG.Core.Events.EffectEvents
{
    public readonly record struct EffectAppliedPayload(
        string EffectId,
        Guid SourceId,
        Guid TargetId,
        int RemainingTick,
        int StackCount);

    public readonly record struct EffectTickPayload(
        string EffectId,
        Guid SourceId,
        Guid TargetId,
        int Tick,
        int TickIndex,
        int RemainingTick,
        int StackCount,
        int DeltaHp);

    public readonly record struct EffectExpiredPayload(
        string EffectId,
        Guid SourceId,
        Guid TargetId,
        int StackCount);

    public sealed record EffectAppliedEvent(EffectAppliedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<EffectAppliedPayload>(Payload, EventCategory.Simulation, Source, Target);

    public sealed record EffectTickEvent(EffectTickPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<EffectTickPayload>(Payload, EventCategory.Simulation, Source, Target);

    public sealed record EffectExpiredEvent(EffectExpiredPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<EffectExpiredPayload>(Payload, EventCategory.Simulation, Source, Target);
}
