using System;

namespace CheckmateRPG.Core.Effects
{
    public interface IReadOnlyEffectRuntimeState
    {
        string EffectId { get; }
        Guid SourceId { get; }
        Guid TargetId { get; }
        int RemainingTick { get; }
        int StackCount { get; }
        int TickInterval { get; }
        int NextTickIn { get; }
        float Magnitude { get; }
        bool IsExpired { get; }
    }
}
