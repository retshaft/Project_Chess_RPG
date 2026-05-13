namespace CheckmateRPG.Core.Runtime.Mutations
{
    public enum MutationCommitPhase
    {
        QueueMutation = 0,
        CommitPhase = 1,
        RuntimeApply = 2,
        EventPublish = 3
    }
}
