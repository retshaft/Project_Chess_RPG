namespace CheckmateRPG.Core.Simulation.Spatial
{
    public enum SpatialResolutionPolicy
    {
        Reject,
        PriorityWin,
        MutualCancel,
        SwapAllowed,
        ForceOverride,
        HigherSpeedWins = PriorityWin
    }
}
