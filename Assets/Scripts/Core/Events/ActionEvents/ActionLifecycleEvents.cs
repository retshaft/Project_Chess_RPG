using System;
using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Core.Events.ActionEvents
{
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

    public sealed record ActionQueuedEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionStartedEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionResolvedEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionInterruptedEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);

    public sealed record ActionCompletedEvent(ActionLifecyclePayload Payload, string Source = "", string Target = "")
        : BaseGameEvent<ActionLifecyclePayload>(Payload, EventCategory.Domain, Source, Target);
}
