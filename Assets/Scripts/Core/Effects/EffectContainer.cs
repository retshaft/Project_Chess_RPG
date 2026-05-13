using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Effects
{
    /// <summary>
    /// Stores and exposes the active effects for a single unit as independent
    /// <see cref="IEffectRuntime"/> objects.
    /// <para>
    /// Each unit that participates in the simulation owns one <see cref="EffectContainer"/>.
    /// The container is the authoritative per-unit source of truth for effect state and
    /// lifecycle, replacing any need for scattered bool flags or ad-hoc per-unit queries.
    /// </para>
    /// <para>
    /// Mutation methods are <c>internal</c>; external consumers receive a read-only
    /// <see cref="IEffectRuntime"/> projection.
    /// </para>
    /// </summary>
    public sealed class EffectContainer
    {
        private readonly Dictionary<string, EffectRuntimeState> _effects =
            new(StringComparer.Ordinal);

        public EffectContainer(Guid unitId)
        {
            if (unitId == Guid.Empty)
                throw new ArgumentException("UnitId must not be empty.", nameof(unitId));

            UnitId = unitId;
        }

        /// <summary>The unit that owns this container.</summary>
        public Guid UnitId { get; }

        /// <summary>Number of active effects currently held in the container.</summary>
        public int Count => _effects.Count;

        // ── Public read-only access ───────────────────────────────────────────────

        /// <summary>Returns <c>true</c> when the container holds an effect for <paramref name="effectKey"/>.</summary>
        public bool HasEffect(string effectKey) =>
            !string.IsNullOrEmpty(effectKey) && _effects.ContainsKey(effectKey);

        /// <summary>
        /// Attempts to retrieve the runtime view for <paramref name="effectKey"/>.
        /// </summary>
        /// <returns><c>true</c> if found; <c>false</c> otherwise.</returns>
        public bool TryGet(string effectKey, out IEffectRuntime effect)
        {
            if (!string.IsNullOrEmpty(effectKey) &&
                _effects.TryGetValue(effectKey, out EffectRuntimeState state) &&
                state != null)
            {
                effect = state;
                return true;
            }

            effect = null;
            return false;
        }

        /// <summary>
        /// Returns a snapshot list of all currently active effects in this container.
        /// The list reflects the state at the time of the call.
        /// </summary>
        public IReadOnlyList<IEffectRuntime> GetAll()
        {
            var list = new List<IEffectRuntime>(_effects.Count);
            foreach (EffectRuntimeState state in _effects.Values)
            {
                if (state != null)
                    list.Add(state);
            }

            return list;
        }

        // ── Internal mutable access ───────────────────────────────────────────────

        internal bool TryGetMutable(string effectKey, out EffectRuntimeState effect)
        {
            if (string.IsNullOrEmpty(effectKey))
            {
                effect = null;
                return false;
            }

            return _effects.TryGetValue(effectKey, out effect) && effect != null;
        }

        internal void Add(string effectKey, EffectRuntimeState effect)
        {
            if (string.IsNullOrWhiteSpace(effectKey) || effect == null)
                return;

            _effects[effectKey] = effect;
        }

        internal bool Remove(string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey))
                return false;

            return _effects.Remove(effectKey);
        }

        internal IEnumerable<string> Keys => _effects.Keys;
    }
}
