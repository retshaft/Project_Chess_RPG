using System;

namespace CheckmateRPG.Core.Actions
{
    public interface IReadOnlyActionState
    {
        Guid ActionId { get; }
        Guid ActorId { get; }
        ActionState State { get; }
        int QueuedTick { get; }
        int StartTick { get; }
        int ResolveTick { get; }
        int RecoveryEndTick { get; }
        bool IsInterruptible { get; }
        bool IsCompleted { get; }
    }
}
