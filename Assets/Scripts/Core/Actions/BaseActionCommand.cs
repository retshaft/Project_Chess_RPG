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
            ActionSpeedTier speedTier,
            bool isInterruptible = true,
            bool isRecoveryInterruptible = false)
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
            SpeedTier = speedTier;
            IsInterruptible = isInterruptible;
            IsRecoveryInterruptible = isRecoveryInterruptible;
        }

        public Guid ActionId { get; }
        public Guid ActorId { get; }
        public ActionState State { get; private set; }
        public int QueuedTick { get; internal set; }
        public int StartTick { get; }
        public int ResolveTick { get; }
        public int RecoveryEndTick { get; }
        public ActionSpeedTier SpeedTier { get; }
        public bool IsInterruptible { get; }
        public bool IsRecoveryInterruptible { get; }
        public bool IsCompleted => ActionStateMachine.IsTerminal(State);

        internal void MarkQueued(int queuedTick)
        {
            QueuedTick = queuedTick;
            State = ActionState.Queued;
        }

        /// <summary>
        /// Performs a validated state transition through the <see cref="ActionStateMachine"/>.
        /// Throws <see cref="InvalidOperationException"/> on any illegal transition attempt.
        /// </summary>
        internal void TransitionTo(ActionState newState)
        {
            if (State == newState)
                return;

            if (!ActionStateMachine.IsValidTransition(State, newState))
                throw new InvalidOperationException(
                    $"Invalid action state transition: {State} → {newState} (ActionId={ActionId:N}).");

            State = newState;
        }
    }
}
