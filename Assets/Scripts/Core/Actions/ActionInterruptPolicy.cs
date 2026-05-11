namespace CheckmateRPG.Core.Actions
{
    public readonly record struct ActionInterruptPolicy(
        InterruptPriority Priority,
        InterruptWindow Window)
    {
        public static ActionInterruptPolicy Default =>
            new(InterruptPriority.Normal, InterruptWindow.CastingInterruptible);

        public static ActionInterruptPolicy Uninterruptible =>
            new(InterruptPriority.Normal, InterruptWindow.Uninterruptible);
    }
}
