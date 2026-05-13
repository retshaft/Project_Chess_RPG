using System;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public enum ResourceMutationType
    {
        ActionPoint = 0,
        SkillPoint = 1,
        Cooldown = 2,
        Generic = 3
    }

    public readonly record struct ResourceMutation(
        Guid MutationId,
        Guid TargetId,
        ResourceMutationType ResourceType,
        int Delta,
        string Reason = "",
        MutationContext Context = default) : IRuntimeMutation;
}
