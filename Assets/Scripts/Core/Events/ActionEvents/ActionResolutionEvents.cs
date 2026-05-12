using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Simulation.Spatial;
using UnityEngine;

namespace CheckmateRPG.Core.Events.ActionEvents
{
    public readonly record struct MoveActionResolvedPayload(
        Guid ActionId,
        Guid ActorId,
        Vector2Int From,
        Vector2Int To);

    public readonly record struct AttackActionResolvedPayload(
        Guid ActionId,
        Guid ActorId,
        Guid TargetId,
        int Damage,
        bool IsCritical);

    public readonly record struct DamageAppliedPayload(
        Guid ActionId,
        Guid SourceId,
        Guid TargetId,
        int Damage,
        int RemainingHp,
        bool IsCritical);

    public readonly record struct MutationAppliedPayload(
        Guid MutationId,
        Guid TargetId,
        Guid SourceId,
        string MutationType,
        int Tick);

    public readonly record struct AbilityActionResolvedPayload(
        Guid ActionId,
        Guid ActorId,
        string AbilityId,
        int PrimaryTargetCount,
        int EffectCount,
        bool Succeeded);

    public readonly record struct MoveCompletedPayload(
        Guid UnitId,
        Vector2Int From,
        Vector2Int To);

    public readonly record struct SpatialConflictResolvedPayload(
        SpatialConflictType ConflictType,
        Guid WinningAction,
        IReadOnlyList<Guid> LosingActions,
        int Tick);

    public sealed record MoveActionResolvedEvent(MoveActionResolvedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<MoveActionResolvedPayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record AttackActionResolvedEvent(AttackActionResolvedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<AttackActionResolvedPayload>(Payload, EventCategory.Simulation, Source, Target);

    public sealed record DamageAppliedEvent(DamageAppliedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<DamageAppliedPayload>(Payload, EventCategory.Simulation, Source, Target);

    public sealed record MutationAppliedEvent(MutationAppliedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<MutationAppliedPayload>(Payload, EventCategory.Simulation, Source, Target);

    public sealed record AbilityActionResolvedEvent(AbilityActionResolvedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<AbilityActionResolvedPayload>(Payload, EventCategory.Simulation, Source, Target);

    public sealed record MoveCompletedEvent(MoveCompletedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<MoveCompletedPayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record SpatialConflictResolvedEvent(
        SpatialConflictResolvedPayload Payload,
        string Source = "",
        string Target = "")
        : BaseGameEvent<SpatialConflictResolvedPayload>(Payload, EventCategory.Simulation, Source, Target);
}
