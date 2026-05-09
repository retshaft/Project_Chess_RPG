namespace CheckmateRPG.Core.Simulation
{
    public sealed class SimulationRuntime
    {
        public SimulationRuntime(int currentTick)
        {
            CurrentTick = currentTick;
        }

        public int CurrentTick { get; }
    }
}
