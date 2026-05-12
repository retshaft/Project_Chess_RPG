using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core.Simulation.Spatial
{
    public readonly record struct SpatialConflictContext(
        int CurrentTick,
        Guid SourceUnit,
        Vector2Int TargetTile,
        IReadOnlyList<ReservedPosition> CompetingActions,
        IReadOnlyDictionary<Vector2Int, IReadOnlyList<ReservedPosition>> ReservationData);
}
