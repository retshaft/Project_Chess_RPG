namespace CheckmateRPG.Core.Actions
{
    public enum ActionState
    {
        Queued,
        Casting,
        Resolving,
        Recovery,
        Completed,
        Cancelled,
        Interrupted
    }
}
