using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    public sealed class ActionResolverRegistry
    {
        private readonly Dictionary<Type, Func<IActionCommand, IBattleContext, ActionResolutionResult>> _resolvers = new();

        public ActionResolverRegistry()
        {
            Register(new MoveActionResolver());
            Register(new AttackActionResolver());
        }

        public void Register<TAction>(IActionResolver<TAction> resolver)
            where TAction : IActionCommand
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            _resolvers[typeof(TAction)] = (action, context) => resolver.Resolve((TAction)action, context);
        }

        public bool TryResolve(
            IActionCommand action,
            IBattleContext battleContext,
            out ActionResolutionResult result)
        {
            if (action == null || battleContext == null)
            {
                result = ActionResolutionResult.Failed();
                return false;
            }

            if (!_resolvers.TryGetValue(action.GetType(), out Func<IActionCommand, IBattleContext, ActionResolutionResult> resolver))
            {
                result = ActionResolutionResult.Failed();
                return false;
            }

            result = resolver(action, battleContext);
            return true;
        }
    }
}
