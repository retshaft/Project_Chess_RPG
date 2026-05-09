namespace CheckmateRPG.Core.Effects
{
    public interface IEffectProcessor
    {
        bool CanProcess(EffectRuntimeState effect);
        void OnApplied(EffectSystemContext context, EffectRuntimeState effect);
        int OnTick(EffectSystemContext context, EffectRuntimeState effect);
        void OnExpired(EffectSystemContext context, EffectRuntimeState effect);
    }
}
