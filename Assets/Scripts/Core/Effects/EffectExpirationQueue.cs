using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Effects
{
    /// <summary>
    /// Collects effects whose <see cref="IReadOnlyEffectRuntimeState.RemainingTick"/> has
    /// reached zero during a tick's <see cref="EffectTimingPhase.OnTickEnd"/> phase and
    /// defers their removal until <see cref="Flush"/> is called.
    /// <para>
    /// Rule: an effect whose duration reaches 0 must <em>never</em> be removed inline;
    /// it must be enqueued here and flushed after all phases of the current tick complete.
    /// This guarantees that death-trigger effects and chain effects in later phases still
    /// see the expiring effect in the active set during the same tick.
    /// </para>
    /// </summary>
    public sealed class EffectExpirationQueue
    {
        private readonly Queue<string> _pending = new();

        /// <summary>Number of effects currently waiting to be flushed.</summary>
        public int Count => _pending.Count;

        /// <summary>
        /// Enqueues <paramref name="effectKey"/> for deferred removal.
        /// Duplicate keys are silently ignored.
        /// </summary>
        /// <param name="effectKey">The runtime key identifying the expiring effect.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="effectKey"/> is null or empty.</exception>
        public void Enqueue(string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey))
                throw new ArgumentNullException(nameof(effectKey));

            _pending.Enqueue(effectKey);
        }

        /// <summary>
        /// Drains the queue and invokes <paramref name="removeCallback"/> for every
        /// enqueued effect key, then clears the queue.
        /// </summary>
        /// <param name="removeCallback">
        /// Callback that performs the actual runtime removal. Must not be null.
        /// </param>
        public void Flush(Action<string> removeCallback)
        {
            if (removeCallback == null)
                throw new ArgumentNullException(nameof(removeCallback));

            while (_pending.Count > 0)
            {
                string effectKey = _pending.Dequeue();
                removeCallback(effectKey);
            }
        }

        /// <summary>Clears all pending entries without invoking any callbacks.</summary>
        public void Clear()
        {
            _pending.Clear();
        }
    }
}
