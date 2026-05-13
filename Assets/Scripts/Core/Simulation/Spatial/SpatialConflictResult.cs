using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Simulation.Spatial
{
    public readonly record struct SpatialConflictResult(
        Guid WinningAction,
        IReadOnlyList<Guid> RejectedActions,
        SpatialConflictType ConflictType,
        SpatialResolutionPolicy ResolutionPolicy);
}
