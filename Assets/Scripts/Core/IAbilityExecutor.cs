using CheckmateRPG.Core.Actions.Resolvers;

namespace CheckmateRPG.Core
{
    public interface IAbilityExecutor
    {
        bool TryQueueAbility(AbilityQueueRequest request, out AbilityQueueResult result);
        ActionResolutionResult ResolveAbility(AbilityResolveRequest request);
    }
}
