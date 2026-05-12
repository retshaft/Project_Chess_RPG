using System;
using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Core.Events.ActionEvents
{
    public readonly record struct ActionCancelledPayload(
        Guid ActionId,
        Guid ActorId,
        ActionCancellationReason Reason,
        int SchedulerTick);

    public readonly record struct ActionLifecyclePayload(
        Guid ActionId,
        Guid ActorId,
        ActionState PreviousState,
        ActionState CurrentState,
        int SchedulerTick,
        int QueuedTick,
        int StartTick,
        int ResolveTick,
        int RecoveryEndTick,
        bool IsInterruptible);

    public readonly record struct ActionInterruptedPayload(
        Guid SourceActionId,
        Guid TargetActionId,
        Guid ActorId,
        InterruptPriority InterruptPriority,
        int SchedulerTick);

    public readonly record struct ActionRejectedPayload(
        Guid RequestedActionId,
        Guid ActorId,
        ActionAdmissionRejectionReason Reason,
        ActionLockType CurrentLock,
        int SchedulerTick);

    /// <summary>
    /// Published on every action state transition. Provides full lifecycle context.
    /// </summary>
    public sealed record ActionStateChangedEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionQueuedEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    /// <summary>Published when an action enters the <see cref="ActionState.Casting"/> phase.</summary>
    public sealed record ActionCastingEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionResolvedEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionRecoveryEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionInterruptedEvent(ActionInterruptedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionInterruptedPayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionCompletedEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionCancelledEvent(ActionCancelledPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionCancelledPayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionRejectedEvent(ActionRejectedPayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionRejectedPayload>(Payload, EventCategory.Domain, Source, Target);
}
