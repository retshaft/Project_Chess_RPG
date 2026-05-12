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
        ActionSpeedTier SpeedTier { get; }
        bool IsInterruptible { get; }
        bool IsRecoveryInterruptible { get; }
        InterruptPriority InterruptPriority { get; }
        InterruptWindow InterruptWindow { get; }
        ActionLockType IntentLockType { get; }
        ActionConcurrencyPolicy ConcurrencyPolicy { get; }
        bool IsCompleted { get; }
    }
}
