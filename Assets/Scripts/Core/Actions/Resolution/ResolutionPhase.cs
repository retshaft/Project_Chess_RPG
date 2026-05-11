namespace CheckmateRPG.Core.Actions.Resolution
{
    /// <summary>
    /// Sequential phases of the action resolution pipeline executed once per tick.
    /// </summary>
    public enum ResolutionPhase
    {
        /// <summary>Interrupt checks, counter reservation, and pre-validation.</summary>
        PreResolve = 0,

        /// <summary>
        /// Damage computation, movement, and mutation generation.
        /// Runtime state must NOT be mutated directly in this phase.
        /// </summary>
        Resolve = 1,

        /// <summary>
        /// Reaction triggers, reflect effects, and chain-reaction scheduling.
        /// Reactions are ordered by ActionSpeedTier.
        /// </summary>
        PostResolve = 2,

        /// <summary>Death confirmation, state cleanup, and event flush.</summary>
        Finalize = 3
    }
}
