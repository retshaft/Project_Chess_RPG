using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Actions.Resolution
{
    /// <summary>
    /// Provides deterministic ordering for simultaneous action resolution within a single tick.
    /// <para>
    /// Priority (ascending = resolved first):
    /// <list type="number">
    ///   <item>ActionSpeedTier – VeryFast (0) before VerySlow (4).</item>
    ///   <item>ResolveTick – earlier scheduled tick first.</item>
    ///   <item>ActionId – GUID byte-order comparison for a stable tie-break.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class ResolutionOrderComparer : IComparer<IActionCommand>
    {
        /// <summary>Singleton instance. Thread-safe for read-only comparison operations.</summary>
        public static readonly ResolutionOrderComparer Default = new();

        private ResolutionOrderComparer() { }

        public int Compare(IActionCommand x, IActionCommand y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            // Lower enum value = higher priority (VeryFast = 0 resolves before VerySlow = 4).
            int speedCompare = ((int)x.SpeedTier).CompareTo((int)y.SpeedTier);
            if (speedCompare != 0) return speedCompare;

            // Earlier scheduled resolve tick wins.
            int tickCompare = x.ResolveTick.CompareTo(y.ResolveTick);
            if (tickCompare != 0) return tickCompare;

            // Deterministic GUID tie-break – never produces a non-deterministic result.
            return x.ActionId.CompareTo(y.ActionId);
        }
    }
}
