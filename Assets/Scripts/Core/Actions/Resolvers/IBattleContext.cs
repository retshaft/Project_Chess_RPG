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
    }
}
