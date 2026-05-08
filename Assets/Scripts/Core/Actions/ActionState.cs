namespace CheckmateRPG.Core.Actions
{
    public enum ActionState
    {
        Queued,
        Executing,
        Resolving,
        Recovery,
        Completed,
        Cancelled,
        Interrupted
    }
}
