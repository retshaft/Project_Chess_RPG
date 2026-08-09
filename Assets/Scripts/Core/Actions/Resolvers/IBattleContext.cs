using System;
using UnityEngine;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    public readonly record struct BattleUnitSnapshot(
        Guid UnitId,
        Vector2Int Position,
        int HP,
        UnitStatusFlags StatusFlags);

    public interface IBattleContext
    {
        bool TryGetUnit(Guid unitId, out BattleUnitSnapshot unit);
        bool IsCellValid(Vector2Int cell);
        bool IsCellOccupied(Vector2Int cell, Guid ignoredUnitId = default);
        bool IsTargetInAttackRange(Guid attackerId, Guid targetId);
        int CriticalDamageMultiplier { get; }
        int GetAttackCount(Guid unitId);
        float GetAttackDamageRatio(Guid unitId);
        float GetDefPenetrationRatio(Guid attackerId, Guid targetId);
        float GetAttackDamageMultiplier(Guid unitId);
        void NotifyAttackPerformed(Guid unitId);
        void NotifyAttackStart(Guid attackerId, Guid targetId);
        int GetOnHitEffectCount(Guid unitId, CheckmateRPG.Core.StatusEffectType type);
    }
}
