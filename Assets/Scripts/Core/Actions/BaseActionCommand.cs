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
            bool isRecoveryInterruptible = false,
            InterruptPriority interruptPriority = InterruptPriority.Normal,
            InterruptWindow? interruptWindow = null,
            bool canInterruptOthers = true,
            InterruptPriority interruptProtection = InterruptPriority.None,
            ActionDefinition? definition = null,
            ActionLockType intentLockType = ActionLockType.CastLock,
            ActionConcurrencyPolicy concurrencyPolicy = ActionConcurrencyPolicy.Reject)
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
            InterruptWindow resolvedWindow = interruptWindow ??
                (isRecoveryInterruptible
                    ? InterruptWindow.RecoveryInterruptible
                    : isInterruptible
                        ? InterruptWindow.CastingInterruptible
                        : InterruptWindow.Uninterruptible);
            Definition = definition ??
                         new ActionDefinition(
                             interruptPriority,
                             resolvedWindow,
                             isInterruptible,
                             canInterruptOthers);
            IsInterruptible = Definition.CanBeInterrupted;
            IsRecoveryInterruptible = Definition.InterruptWindow == InterruptWindow.RecoveryInterruptible;
            CanBeInterrupted = Definition.CanBeInterrupted;
            CanInterruptOthers = Definition.CanInterruptOthers;
            InterruptPriority = Definition.InterruptPriority;
            InterruptWindow = Definition.InterruptWindow;
            InterruptState = new ActionInterruptState(
                Definition.InterruptPriority,
                Definition.InterruptWindow,
                interruptProtection,
                Guid.Empty);
            IntentLockType = intentLockType;
            ConcurrencyPolicy = concurrencyPolicy;
        }

        public Guid ActionId { get; }
        public Guid ActorId { get; }
        public ActionState State { get; private set; }
        public int QueuedTick { get; internal set; }
        public int StartTick { get; private set; }
        public int ResolveTick { get; private set; }
        public int RecoveryEndTick { get; private set; }
        public ActionSpeedTier SpeedTier { get; }
        public bool IsInterruptible { get; }
        public bool IsRecoveryInterruptible { get; }
        public bool CanBeInterrupted { get; }
        public bool CanInterruptOthers { get; }
        public InterruptPriority InterruptPriority { get; }
        public InterruptWindow InterruptWindow { get; }
        public ActionDefinition Definition { get; }
        public ActionInterruptState InterruptState { get; private set; }
        public ActionLockType IntentLockType { get; }
        public ActionConcurrencyPolicy ConcurrencyPolicy { get; }
        public ActionInterruptPolicy InterruptPolicy => new(Definition);
        public bool IsCompleted => ActionStateMachine.IsTerminal(State);

        internal void MarkQueued(int queuedTick)
        {
            QueuedTick = queuedTick;
            State = ActionState.Queued;
        }

        internal void RebaseTimeline(int startTick)
        {
            int boundedStartTick = Math.Max(0, startTick);
            int castDuration = Math.Max(0, ResolveTick - StartTick);
            int recoveryDuration = Math.Max(0, RecoveryEndTick - ResolveTick);
            StartTick = boundedStartTick;
            ResolveTick = boundedStartTick + castDuration;
            RecoveryEndTick = ResolveTick + recoveryDuration;
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

        internal void RegisterInterruptSource(Guid sourceActionId)
        {
            InterruptState = InterruptState with
            {
                InterruptSource = sourceActionId
            };
        }
    }
}
