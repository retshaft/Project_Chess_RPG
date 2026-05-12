namespace CheckmateRPG.Core.Actions
{
    public readonly record struct ActionDefinition(
        InterruptPriority InterruptPriority,
        InterruptWindow InterruptWindow,
        bool CanBeInterrupted,
        bool CanInterruptOthers)
    {
        public static ActionDefinition Default =>
            new(InterruptPriority.Normal, InterruptWindow.CastingInterruptible, true, true);
    }
}
