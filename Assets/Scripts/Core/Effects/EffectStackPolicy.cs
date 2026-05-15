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
        public const int DefaultMaxApplicationsPerTick = 5;

        public static int ResolveMaxApplicationsPerTick(int maxApplicationsPerTick)
        {
            if (maxApplicationsPerTick <= 0)
                return DefaultMaxApplicationsPerTick;

            return maxApplicationsPerTick;
        }
    }
}
