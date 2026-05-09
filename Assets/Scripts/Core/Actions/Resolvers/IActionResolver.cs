using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    /// <summary>
    /// Resolves a scheduled action into a deterministic resolution result using battle context data.
    /// </summary>
    /// <typeparam name="TAction">Concrete action command type handled by this resolver.</typeparam>
    public interface IActionResolver<TAction>
        where TAction : IActionCommand
    {
        /// <summary>
        /// Computes the action result without mutating runtime state.
        /// </summary>
        ActionResolutionResult Resolve(
            TAction action,
            IBattleContext battleContext);
    }
}
