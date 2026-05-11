using System;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation.Spatial
{
    public readonly record struct ReservedPosition(
        Vector2Int Position,
        Guid UnitId,
        Guid ActionId,
        ActionSpeedTier ActionSpeedLevel,
        int ReservationTick);
}
