using System.Collections.Generic;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation.Spatial
{
    public readonly record struct SpatialConflictContext(
        int Tick,
        IReadOnlyList<ReservedPosition> CompetingActions,
        Vector2Int TargetTile,
        IReadOnlyDictionary<Vector2Int, IReadOnlyList<IReadOnlyUnitRuntimeState>> OccupancyData,
        IReadOnlyDictionary<Vector2Int, IReadOnlyList<ReservedPosition>> ReservationData);
}
