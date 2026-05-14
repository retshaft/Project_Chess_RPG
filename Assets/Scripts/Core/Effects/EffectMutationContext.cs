using System;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Effects
{
    public readonly record struct EffectMutationUnitContext(
        Guid UnitId,
        int CurrentHp,
        int MaxHp,
        Vector2Int Position,
        UnitStatusFlags StatusFlags,
        bool Exists)
    {
        public static EffectMutationUnitContext Empty(Guid unitId = default) =>
            new(unitId, 0, 0, default, UnitStatusFlags.None, false);

        public bool IsDead => !Exists || CurrentHp <= 0 || (StatusFlags & UnitStatusFlags.Dead) != 0;
    }

    public readonly record struct EffectMutationContext(
        IReadOnlyEffectRuntimeState SourceEffect,
        EffectMutationUnitContext SourceUnit,
        EffectMutationUnitContext TargetUnit,
        int Tick,
        string Reason)
    {
        public static EffectMutationContext Empty =>
            new(null, EffectMutationUnitContext.Empty(), EffectMutationUnitContext.Empty(), 0, string.Empty);
    }
}
