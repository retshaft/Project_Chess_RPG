using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Core.Determinism
{
    /// <summary>
    /// Canonical stable sort policy for simulation entities that participate in tick resolution.
    /// <para>
    /// Priority order (ascending = earlier in sorted sequence):
    /// <list type="number">
    ///   <item><term>Tick</term><description>
    ///     Earlier scheduled tick wins. Entities scheduled for lower tick values are
    ///     processed first.
    ///   </description></item>
    ///   <item><term>ActionSpeedLevel</term><description>
    ///     Lower <see cref="ActionSpeedTier"/> enum value wins
    ///     (<c>VeryFast = 0</c> before <c>VerySlow = 4</c>).
    ///   </description></item>
    ///   <item><term>ResolveOrder</term><description>
    ///     Lower resolve-order index wins. This corresponds to the position of the action
    ///     in the tick's sorted resolution list.
    ///   </description></item>
    ///   <item><term>StableId ordering</term><description>
    ///     GUID byte-order comparison used as a final deterministic tie-break.
    ///   </description></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class StableSortPolicy<T> : IComparer<T>
    {
        private readonly Func<T, int> _getTick;
        private readonly Func<T, ActionSpeedTier> _getSpeedTier;
        private readonly Func<T, int> _getResolveOrder;
        private readonly Func<T, Guid> _getStableId;

        /// <summary>
        /// Constructs the comparer with explicit key selectors for each ordering dimension.
        /// </summary>
        /// <param name="getTick">Returns the tick value of an element.</param>
        /// <param name="getSpeedTier">Returns the <see cref="ActionSpeedTier"/> of an element.</param>
        /// <param name="getResolveOrder">
        ///   Returns the resolve-order index of an element within a tick
        ///   (lower index = resolved earlier). Pass <c>_ => 0</c> if not applicable.
        /// </param>
        /// <param name="getStableId">
        ///   Returns the stable <see cref="Guid"/> used as a final tie-break.
        /// </param>
        public StableSortPolicy(
            Func<T, int> getTick,
            Func<T, ActionSpeedTier> getSpeedTier,
            Func<T, int> getResolveOrder,
            Func<T, Guid> getStableId)
        {
            _getTick = getTick ?? throw new ArgumentNullException(nameof(getTick));
            _getSpeedTier = getSpeedTier ?? throw new ArgumentNullException(nameof(getSpeedTier));
            _getResolveOrder = getResolveOrder ?? throw new ArgumentNullException(nameof(getResolveOrder));
            _getStableId = getStableId ?? throw new ArgumentNullException(nameof(getStableId));
        }

        /// <inheritdoc/>
        public int Compare(T x, T y)
        {
            // 1. Tick — earlier tick is processed first.
            int tickCmp = _getTick(x).CompareTo(_getTick(y));
            if (tickCmp != 0) return tickCmp;

            // 2. ActionSpeedLevel — lower enum value (VeryFast=0) wins.
            int speedCmp = ((int)_getSpeedTier(x)).CompareTo((int)_getSpeedTier(y));
            if (speedCmp != 0) return speedCmp;

            // 3. ResolveOrder — earlier position in tick's sorted list wins.
            int orderCmp = _getResolveOrder(x).CompareTo(_getResolveOrder(y));
            if (orderCmp != 0) return orderCmp;

            // 4. StableId — Guid byte-order comparison, always produces a total order.
            return _getStableId(x).CompareTo(_getStableId(y));
        }
    }

    /// <summary>
    /// Pre-built <see cref="StableSortPolicy{T}"/> for <see cref="IActionCommand"/> instances.
    /// Uses <see cref="IReadOnlyActionState.ResolveTick"/> as the tick key,
    /// <see cref="IReadOnlyActionState.SpeedTier"/> as the speed key, a supplied resolve-order
    /// map, and <see cref="IReadOnlyActionState.ActionId"/> as the stable id.
    /// </summary>
    public static class ActionCommandStableSortPolicy
    {
        /// <summary>
        /// Creates a comparer for <see cref="IActionCommand"/> using the given
        /// <paramref name="resolveOrderMap"/> to look up each action's position in the
        /// tick's resolution sequence.
        /// </summary>
        /// <param name="resolveOrderMap">
        ///   Maps <see cref="IReadOnlyActionState.ActionId"/> to its resolve-order index for
        ///   the current tick. Pass an empty dictionary if no explicit ordering is available;
        ///   the fallback is <c>int.MaxValue</c>.
        /// </param>
        public static StableSortPolicy<IActionCommand> Create(
            IReadOnlyDictionary<Guid, int> resolveOrderMap = null)
        {
            return new StableSortPolicy<IActionCommand>(
                getTick: cmd => cmd.ResolveTick,
                getSpeedTier: cmd => cmd.SpeedTier,
                getResolveOrder: cmd =>
                {
                    if (resolveOrderMap != null &&
                        resolveOrderMap.TryGetValue(cmd.ActionId, out int order))
                        return order;
                    return int.MaxValue;
                },
                getStableId: cmd => cmd.ActionId);
        }

        /// <summary>
        /// Creates a comparer for <see cref="IActionCommand"/> with no explicit resolve-order
        /// map. Equivalent to calling <see cref="Create"/> with a null map.
        /// </summary>
        public static StableSortPolicy<IActionCommand> Default { get; } = Create();
    }
}
