using System;
using System.Collections.Generic;
using CheckmateRPG.Core;

namespace CheckmateRPG.Core.Effects
{
    /// <summary>
    /// Provides deterministic ordering for effects that share the same
    /// <see cref="EffectTimingPhase"/> within a single pipeline pass.
    /// <para>
    /// Priority (ascending = executed first):
    /// <list type="number">
    ///   <item><see cref="IReadOnlyEffectRuntimeState.ActionSpeedLevel"/> –
    ///         lower enum value (VeryFast = 0) executes before higher values.</item>
    ///   <item>Remaining tick – fewer ticks remaining is processed first.</item>
    ///   <item>Effect key string – lexicographic comparison for a stable tie-break.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class EffectTimingOrderComparer : IComparer<(string Key, IReadOnlyEffectRuntimeState Effect)>
    {
        /// <summary>Singleton instance. Thread-safe for read-only comparison operations.</summary>
        public static readonly EffectTimingOrderComparer Default = new();

        private EffectTimingOrderComparer() { }

        public int Compare(
            (string Key, IReadOnlyEffectRuntimeState Effect) x,
            (string Key, IReadOnlyEffectRuntimeState Effect) y)
        {
            IReadOnlyEffectRuntimeState ex = x.Effect;
            IReadOnlyEffectRuntimeState ey = y.Effect;

            if (ex == null && ey == null) return 0;
            if (ex == null) return 1;
            if (ey == null) return -1;

            // 1. ActionSpeedLevel – lower enum value resolves first.
            int speedCompare = ((int)ex.ActionSpeedLevel).CompareTo((int)ey.ActionSpeedLevel);
            if (speedCompare != 0) return speedCompare;

            // 2. Remaining tick – fewer ticks remaining (closer to expiry) processed first.
            int tickCompare = ex.RemainingTick.CompareTo(ey.RemainingTick);
            if (tickCompare != 0) return tickCompare;

            // 3. Deterministic runtime key tie-break (format: "{targetId}:{effectId}").
            return string.Compare(x.Key, y.Key, StringComparison.Ordinal);
        }
    }
}
