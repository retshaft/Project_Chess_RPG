using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    public readonly record struct PendingReactionExecution(
        int RegistrationOrder,
        string ReactionId,
        ActionSpeedTier SourceActionSpeedLevel,
        ReactionExecutionPlan Plan);

    /// <summary>
    /// Deterministic reaction ordering:
    /// 1) Earlier registration first (lower registration index first, ascending)
    /// 2) Higher source action speed tier
    /// 3) ReactionId ordinal ordering
    /// </summary>
    public sealed class ReactionOrderingComparer : IComparer<PendingReactionExecution>
    {
        public static readonly ReactionOrderingComparer Default = new();

        private ReactionOrderingComparer() { }

        public int Compare(PendingReactionExecution x, PendingReactionExecution y)
        {
            int registrationOrderCompare = x.RegistrationOrder.CompareTo(y.RegistrationOrder);
            if (registrationOrderCompare != 0)
                return registrationOrderCompare;

            int speedCompare = GetSpeedPriority(y.SourceActionSpeedLevel).CompareTo(GetSpeedPriority(x.SourceActionSpeedLevel));
            if (speedCompare != 0)
                return speedCompare;

            return string.Compare(x.ReactionId, y.ReactionId, StringComparison.Ordinal);
        }

        private static int GetSpeedPriority(ActionSpeedTier tier)
        {
            return tier switch
            {
                ActionSpeedTier.VeryFast => 5,
                ActionSpeedTier.Fast => 4,
                ActionSpeedTier.Normal => 3,
                ActionSpeedTier.Slow => 2,
                ActionSpeedTier.VerySlow => 1,
                _ => 0
            };
        }
    }
}
