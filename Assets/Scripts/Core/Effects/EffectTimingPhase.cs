namespace CheckmateRPG.Core.Effects
{
    /// <summary>
    /// Defines the deterministic execution phase of an <see cref="EffectRuntimeState"/>.
    /// Every effect must declare exactly one timing phase; it will only be evaluated
    /// by the <see cref="EffectTimingPipeline"/> during that phase.
    /// </summary>
    public enum EffectTimingPhase
    {
        /// <summary>
        /// Evaluated when a cast starts.
        /// Typical uses: cast triggers, cast modifiers.
        /// </summary>
        OnCastStart = 0,

        /// <summary>
        /// Evaluated before action resolution begins.
        /// Typical uses: shields, guards, intercepts.
        /// </summary>
        OnPreResolve = 1,

        /// <summary>
        /// Evaluated during action resolution.
        /// Typical uses: direct damage, heals, movement effects.
        /// </summary>
        OnResolve = 2,

        /// <summary>
        /// Evaluated after action resolution completes.
        /// Typical uses: reactions, counters, reflects, chain effects.
        /// <para>
        /// Reaction effects (<see cref="EffectRuntimeState.IsReaction"/> == <c>true</c>)
        /// are only permitted in this phase and will be rejected in any other.
        /// </para>
        /// </summary>
        OnPostResolve = 3,

        /// <summary>
        /// Evaluated at the end of each tick.
        /// Typical uses: DOT, HOT, duration-based effects.
        /// <para>Duration countdown is exclusively performed during this phase.</para>
        /// </summary>
        OnTickEnd = 4,

        /// <summary>
        /// Evaluated during the recovery window of an action.
        /// Typical uses: recovery triggers, after-cast effects.
        /// </summary>
        OnRecovery = 5,

        /// <summary>
        /// Evaluated when a unit reaches HP 0.
        /// Typical uses: death triggers, death explosions.
        /// <para>
        /// Death finalization must never occur before the
        /// <see cref="CheckmateRPG.Core.Actions.Resolution.ResolutionPhase.Finalize"/> phase.
        /// </para>
        /// </summary>
        OnDeath = 6
    }
}
