namespace CheckmateRPG.Core.Effects
{
    public interface IEffectProcessor
    {
        bool CanProcess(IReadOnlyEffectRuntimeState effect);
        void OnApplied(EffectSystemContext context, IReadOnlyEffectRuntimeState effect);
        int OnTick(EffectSystemContext context, IReadOnlyEffectRuntimeState effect);
        void OnExpired(EffectSystemContext context, IReadOnlyEffectRuntimeState effect);
    }
}
