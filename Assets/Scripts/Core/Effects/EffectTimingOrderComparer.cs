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
    ///   <item>Earlier <see cref="IReadOnlyEffectRuntimeState.AppliedTick"/> first.</item>
    ///   <item>Higher effective speed first (VeryFast → VerySlow).</item>
    ///   <item><see cref="IReadOnlyEffectRuntimeState.EffectId"/> ordinal ordering.</item>
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

            // 1. Earlier applied tick first.
            int appliedCompare = ex.AppliedTick.CompareTo(ey.AppliedTick);
            if (appliedCompare != 0) return appliedCompare;

            // 2. Higher effective speed first (VeryFast has highest priority).
            int speedCompare = GetSpeedPriority(ey.ActionSpeedLevel).CompareTo(GetSpeedPriority(ex.ActionSpeedLevel));
            if (speedCompare != 0) return speedCompare;

            // 3. Deterministic effect-id ordering.
            int effectIdCompare = string.Compare(ex.EffectId, ey.EffectId, StringComparison.Ordinal);
            if (effectIdCompare != 0) return effectIdCompare;

            // 4. Deterministic runtime key fallback.
            return string.Compare(x.Key, y.Key, StringComparison.Ordinal);
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
