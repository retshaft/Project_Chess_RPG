namespace CheckmateRPG.Core.Actions
{
    public readonly record struct ActionInterruptPolicy(ActionDefinition Definition) : IActionInterruptPolicy
    {
        public static ActionInterruptPolicy Default =>
            new(ActionDefinition.Default);

        public static ActionInterruptPolicy Uninterruptible =>
            new(new ActionDefinition(
                InterruptPriority.Normal,
                InterruptWindow.Uninterruptible,
                false,
                true));

        public bool CanBeInterrupted(IReadOnlyActionState targetAction)
        {
            if (targetAction == null)
                return false;

            return Definition.CanBeInterrupted &&
                   ActionStateMachine.CanInterrupt(targetAction.State, Definition.InterruptWindow);
        }

        public bool CanInterruptOthers(IReadOnlyActionState sourceAction)
        {
            if (sourceAction == null)
                return false;

            return Definition.CanInterruptOthers;
        }
    }
}
