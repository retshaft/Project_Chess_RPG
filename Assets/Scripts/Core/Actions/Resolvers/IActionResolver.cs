using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    public interface IActionResolver<TAction>
        where TAction : IActionCommand
    {
        ActionResolutionResult Resolve(
            TAction action,
            IBattleContext battleContext);
    }
}
