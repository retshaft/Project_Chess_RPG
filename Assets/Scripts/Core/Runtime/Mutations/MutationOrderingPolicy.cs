namespace CheckmateRPG.Core.Runtime.Mutations
{
    public enum MutationOrderingStage
    {
        Movement = 1,
        Damage = 2,
        Death = 3,
        Cleanup = 4,
        EventDispatch = 5
    }

    public sealed class MutationOrderingPolicy
    {
        public MutationOrderingStage ResolveStage(IRuntimeMutation mutation)
        {
            return mutation switch
            {
                MovementMutation => MutationOrderingStage.Movement,
                MoveMutation => MutationOrderingStage.Movement,
                DamageMutation => MutationOrderingStage.Damage,
                HealMutation => MutationOrderingStage.Damage,
                SPMutation => MutationOrderingStage.Damage,
                DeathMutation => MutationOrderingStage.Death,
                CooldownMutation => MutationOrderingStage.Cleanup,
                AbilityActionCompleteMutation => MutationOrderingStage.Cleanup,
                _ => MutationOrderingStage.Cleanup
            };
        }

        public bool IsPreDeathStage(MutationOrderingStage stage)
        {
            return stage is MutationOrderingStage.Movement or MutationOrderingStage.Damage or MutationOrderingStage.Death;
        }
    }
}
