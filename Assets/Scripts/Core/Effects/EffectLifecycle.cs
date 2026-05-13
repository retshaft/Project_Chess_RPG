namespace CheckmateRPG.Core.Effects
{
    /// <summary>
    /// Describes the current lifecycle stage of an <see cref="EffectRuntimeState"/>.
    /// Each stage represents a distinct phase of an effect's lifetime, from first
    /// application through final removal. Transitions are managed exclusively by the
    /// <see cref="EffectSystem"/> and <see cref="EffectTimingPipeline"/>.
    /// </summary>
    public enum EffectLifecycle
    {
        /// <summary>
        /// The effect was just applied or refreshed on a target.
        /// Set immediately after <see cref="EffectRuntimeState.RefreshFromApplication"/> runs.
        /// </summary>
        Applied = 0,

        /// <summary>
        /// The effect is running and waiting for its next evaluation tick.
        /// Set after <see cref="IEffectProcessor.OnApplied"/> returns.
        /// </summary>
        Active = 1,

        /// <summary>
        /// The effect is actively being processed by the timing pipeline during the
        /// current tick (i.e. inside <see cref="IEffectProcessor.OnTick"/>).
        /// </summary>
        Ticking = 2,

        /// <summary>
        /// The effect's remaining duration has reached zero.
        /// It remains in the active set until the <see cref="EffectExpirationQueue"/> is flushed.
        /// </summary>
        Expired = 3,

        /// <summary>
        /// The effect has been fully removed from the active container.
        /// This is the terminal stage; no further transitions are permitted.
        /// </summary>
        Removed = 4
    }
}
