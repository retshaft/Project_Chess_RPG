using System;
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

    public sealed record MoveActionResolvedEvent(MoveActionResolvedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<MoveActionResolvedPayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record AttackActionResolvedEvent(AttackActionResolvedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<AttackActionResolvedPayload>(Payload, EventCategory.Combat, Source, Target);

    public sealed record DamageAppliedEvent(DamageAppliedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<DamageAppliedPayload>(Payload, EventCategory.Combat, Source, Target);
}
