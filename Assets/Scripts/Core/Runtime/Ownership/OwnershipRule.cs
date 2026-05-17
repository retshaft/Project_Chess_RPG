using System;

namespace CheckmateRPG.Core.Runtime.Ownership
{
    public readonly record struct OwnershipRule(string StateKey, string OwnerName);

    public static class OwnershipStateKeys
    {
        public const string HP = nameof(HP);
        public const string SP = nameof(SP);
        public const string Position = nameof(Position);
        public const string Cooldown = nameof(Cooldown);
        public const string EffectStack = nameof(EffectStack);
        public const string EffectLifecycle = nameof(EffectLifecycle);
        public const string ActionState = nameof(ActionState);
    }

    public static class OwnershipOwners
    {
        public const string DamageMutationProcessor = nameof(DamageMutationProcessor);
        public const string SPMutationProcessor = nameof(SPMutationProcessor);
        public const string MovementMutationProcessor = nameof(MovementMutationProcessor);
        public const string TickScheduler = nameof(TickScheduler);
        public const string EffectSystem = nameof(EffectSystem);
        public const string ActionScheduler = nameof(ActionScheduler);
    }
}
