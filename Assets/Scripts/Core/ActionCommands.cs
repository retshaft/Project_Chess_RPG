namespace CheckmateRPG.Core
{
    public enum ActionSpeedTier
    {
        VeryFast,
        Fast,
        Normal,
        Slow,
        VerySlow
    }

    public static class ActionTimelineFormula
    {
        public const int TickMilliseconds = 100;

        public static int ToActionDurationTicks(ActionSpeedTier speedTier)
        {
            return speedTier switch
            {
                ActionSpeedTier.VeryFast => 7,
                ActionSpeedTier.Fast => 10,
                ActionSpeedTier.Normal => 14,
                ActionSpeedTier.Slow => 20,
                ActionSpeedTier.VerySlow => 25,
                _ => 14
            };
        }
    }

    public readonly record struct ActionTimelineDefinition(
        int ActionDuration,
        int ResolveTiming,
        int RecoveryTiming,
        int InterruptWindow)
    {
        public int ResolveTickOffset => ResolveTiming;
        public int RecoveryEndTickOffset => ResolveTiming + RecoveryTiming;

        public void Validate()
        {
            if (ActionDuration <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(ActionDuration), "ActionDuration must be greater than 0.");
            if (ResolveTiming < 0)
                throw new System.ArgumentOutOfRangeException(nameof(ResolveTiming), "ResolveTiming must be 0 or greater.");
            if (RecoveryTiming < 0)
                throw new System.ArgumentOutOfRangeException(nameof(RecoveryTiming), "RecoveryTiming must be 0 or greater.");
            if (InterruptWindow < 0)
                throw new System.ArgumentOutOfRangeException(nameof(InterruptWindow), "InterruptWindow must be 0 or greater.");
            if (ResolveTiming > ActionDuration)
                throw new System.ArgumentOutOfRangeException(nameof(ResolveTiming), "ResolveTiming must not exceed ActionDuration.");
            if (RecoveryEndTickOffset < ResolveTiming)
                throw new System.ArgumentOutOfRangeException(nameof(RecoveryTiming), "Recovery end must not be earlier than ResolveTiming.");
            if (InterruptWindow > ActionDuration)
                throw new System.ArgumentOutOfRangeException(nameof(InterruptWindow), "InterruptWindow must not exceed ActionDuration.");
        }
    }
}
