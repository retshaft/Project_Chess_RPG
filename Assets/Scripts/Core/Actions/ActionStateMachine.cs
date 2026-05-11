namespace CheckmateRPG.Core.Actions
{
    /// <summary>
    /// Enforces the deterministic Action Lifecycle State Machine.
    /// All valid transitions are declared here; any attempt to skip, overwrite, or
    /// perform an invalid transition is explicitly rejected.
    /// </summary>
    /// <remarks>
    /// Basic flow:   Queued → Casting → Resolving → Recovery → Completed
    /// Interrupt:    Queued/Casting/Recovery → Interrupted (policy-gated)
    /// Cancel:       Queued  → Cancelled
    ///               Casting → Cancelled     (pre-resolve only)
    /// </remarks>
    public static class ActionStateMachine
    {
        /// <summary>
        /// Returns <c>true</c> when transitioning from <paramref name="from"/> to
        /// <paramref name="to"/> is a legal state-machine step.
        /// </summary>
        public static bool IsValidTransition(ActionState from, ActionState to)
        {
            return (from, to) switch
            {
                (ActionState.Queued,    ActionState.Casting)     => true,
                (ActionState.Queued,    ActionState.Cancelled)   => true,
                (ActionState.Casting,   ActionState.Resolving)   => true,
                (ActionState.Casting,   ActionState.Interrupted) => true,
                (ActionState.Casting,   ActionState.Cancelled)   => true,
                (ActionState.Resolving, ActionState.Recovery)    => true,
                (ActionState.Resolving, ActionState.Completed)   => true,
                (ActionState.Recovery,  ActionState.Completed)   => true,
                (ActionState.Recovery,  ActionState.Interrupted) => true,
                _ => false
            };
        }

        /// <summary>
        /// Returns <c>true</c> when an action in <paramref name="state"/> is eligible
        /// for cancellation. Cancellation is only permitted before the Resolving phase.
        /// </summary>
        public static bool CanCancel(ActionState state) =>
            state is ActionState.Queued or ActionState.Casting;

        /// <summary>
        /// Returns <c>true</c> when an action in <paramref name="state"/> is eligible
        /// for interruption based on the action's <paramref name="window"/> policy.
        /// </summary>
        public static bool CanInterrupt(ActionState state, InterruptWindow window)
        {
            if (window == InterruptWindow.Uninterruptible)
                return state == ActionState.Queued;

            return state switch
            {
                ActionState.Queued => true,
                ActionState.Casting => window == InterruptWindow.CastingInterruptible,
                ActionState.Resolving => false,
                ActionState.Recovery => window == InterruptWindow.RecoveryInterruptible,
                _ => false
            };
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="state"/> is a terminal lifecycle stage
        /// (no further transitions are possible).
        /// </summary>
        public static bool IsTerminal(ActionState state) =>
            state is ActionState.Completed or ActionState.Cancelled or ActionState.Interrupted;
    }
}
