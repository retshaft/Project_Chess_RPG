namespace CheckmateRPG.Core.Effects
{
    /// <summary>
    /// Represents the full observable runtime surface of an active effect, combining
    /// the read-only state snapshot from <see cref="IReadOnlyEffectRuntimeState"/> with
    /// the current <see cref="EffectLifecycle"/> stage.
    /// <para>
    /// Consumers outside the simulation layer (e.g. AI, UI) should depend on this
    /// interface rather than the mutable <see cref="EffectRuntimeState"/> implementation.
    /// </para>
    /// </summary>
    public interface IEffectRuntime : IReadOnlyEffectRuntimeState
    {
    }
}
