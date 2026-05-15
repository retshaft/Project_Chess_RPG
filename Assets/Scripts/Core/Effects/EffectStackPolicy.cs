namespace CheckmateRPG.Core.Effects
{
    public enum EffectStackPolicy
    {
        Refresh = 0,
        Replace = 1,
        Independent = 2,
        MaxStackCap = 3
    }

    public static class EffectStackPolicyRules
    {
        // Safety default to prevent unbounded re-application of the same effect in a single tick.
        // This value is scoped to effect application control and is independent from reaction depth limits.
        public const int DefaultMaxApplicationsPerTick = 5;

        public static int ResolveMaxApplicationsPerTick(int maxApplicationsPerTick)
        {
            if (maxApplicationsPerTick <= 0)
                return DefaultMaxApplicationsPerTick;

            return maxApplicationsPerTick;
        }
    }
}
