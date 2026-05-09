using System;

namespace CheckmateRPG.Core.Actions
{
    public abstract class BaseActionCommand : IActionCommand
    {
        protected BaseActionCommand(
            Guid actorId,
            int startTick,
            int resolveTick,
            int recoveryEndTick,
            bool isInterruptible = true)
        {
            if (actorId == Guid.Empty)
                throw new ArgumentException("ActorId must not be empty.", nameof(actorId));
            if (startTick < 0)
                throw new ArgumentOutOfRangeException(nameof(startTick));
            if (resolveTick < startTick)
                throw new ArgumentOutOfRangeException(nameof(resolveTick));
            if (recoveryEndTick < resolveTick)
                throw new ArgumentOutOfRangeException(nameof(recoveryEndTick));

            ActionId = SeededRandomProvider.Shared.NextGuid();
            ActorId = actorId;
            State = ActionState.Queued;
            QueuedTick = -1;
            StartTick = startTick;
            ResolveTick = resolveTick;
            RecoveryEndTick = recoveryEndTick;
            IsInterruptible = isInterruptible;
        }

        public Guid ActionId { get; }
        public Guid ActorId { get; }
        public ActionState State { get; internal set; }
        public int QueuedTick { get; internal set; }
        public int StartTick { get; }
        public int ResolveTick { get; }
        public int RecoveryEndTick { get; }
        public bool IsInterruptible { get; }
        public bool IsCompleted => State is ActionState.Completed or ActionState.Cancelled;

        internal void MarkQueued(int queuedTick)
        {
            QueuedTick = queuedTick;
            State = ActionState.Queued;
        }

        internal void TransitionTo(ActionState state)
        {
            State = state;
        }
    }
}
