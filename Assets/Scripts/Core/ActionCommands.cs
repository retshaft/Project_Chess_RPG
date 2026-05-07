using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    public enum ActionCommandState
    {
        Queued,
        Executing,
        Resolving,
        Recovery,
        Completed,
        Cancelled,
        Interrupted
    }

    public interface IActionCommand
    {
        string ActionId { get; }
        string ActorId { get; }
        IReadOnlyList<string> Targets { get; }
        int QueuedTick { get; }
        int StartTick { get; }
        int ResolveTick { get; }
        int RecoveryEndTick { get; }
        ActionCommandState State { get; }
    }

    public abstract record ActionCommandBase(
        string ActionId,
        string ActorId,
        IReadOnlyList<string> Targets,
        int QueuedTick,
        int StartTick,
        int ResolveTick,
        int RecoveryEndTick,
        ActionCommandState State) : IActionCommand;

    public sealed record MoveAction(
        string ActionId,
        string ActorId,
        IReadOnlyList<string> Targets,
        int QueuedTick,
        int StartTick,
        int ResolveTick,
        int RecoveryEndTick,
        ActionCommandState State)
        : ActionCommandBase(ActionId, ActorId, Targets, QueuedTick, StartTick, ResolveTick, RecoveryEndTick, State);

    public sealed record BasicAttackAction(
        string ActionId,
        string ActorId,
        IReadOnlyList<string> Targets,
        int QueuedTick,
        int StartTick,
        int ResolveTick,
        int RecoveryEndTick,
        ActionCommandState State)
        : ActionCommandBase(ActionId, ActorId, Targets, QueuedTick, StartTick, ResolveTick, RecoveryEndTick, State);

    public sealed record AbilityAction(
        string ActionId,
        string ActorId,
        IReadOnlyList<string> Targets,
        int QueuedTick,
        int StartTick,
        int ResolveTick,
        int RecoveryEndTick,
        ActionCommandState State)
        : ActionCommandBase(ActionId, ActorId, Targets, QueuedTick, StartTick, ResolveTick, RecoveryEndTick, State);
}
