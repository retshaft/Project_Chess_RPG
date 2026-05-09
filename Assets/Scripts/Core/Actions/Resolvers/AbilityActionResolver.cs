using System;
using System.Collections.Generic;
using CheckmateRPG.Units;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    public sealed class AbilityActionResolver : IActionResolver<AbilityActionCommand>
    {
        private readonly IAbilityExecutor _executor;
        private readonly Func<string, AbilityDefinition> _abilityLookup;
        private readonly Func<Guid, string, AbilityRuntimeState> _runtimeStateLookup;
        private readonly Func<Guid, UnitBrain> _actorLookup;
        private readonly Func<int> _currentTick;
        private readonly Func<IReadOnlyDictionary<Guid, UnitBrain>> _unitsById;

        public AbilityActionResolver(
            IAbilityExecutor executor,
            Func<string, AbilityDefinition> abilityLookup,
            Func<Guid, string, AbilityRuntimeState> runtimeStateLookup,
            Func<Guid, UnitBrain> actorLookup,
            Func<int> currentTick,
            Func<IReadOnlyDictionary<Guid, UnitBrain>> unitsById)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _abilityLookup = abilityLookup ?? throw new ArgumentNullException(nameof(abilityLookup));
            _runtimeStateLookup = runtimeStateLookup ?? throw new ArgumentNullException(nameof(runtimeStateLookup));
            _actorLookup = actorLookup ?? throw new ArgumentNullException(nameof(actorLookup));
            _currentTick = currentTick ?? throw new ArgumentNullException(nameof(currentTick));
            _unitsById = unitsById ?? throw new ArgumentNullException(nameof(unitsById));
        }

        public ActionResolutionResult Resolve(
            AbilityActionCommand action,
            IBattleContext battleContext)
        {
            _ = battleContext;
            if (action == null)
                return ActionResolutionResult.Failed();
            AbilityDefinition definition = _abilityLookup(action.AbilityId);
            if (definition == null)
                return ActionResolutionResult.Failed();

            UnitBrain actor = _actorLookup(action.ActorId);
            if (actor == null)
                return ActionResolutionResult.Failed();

            AbilityRuntimeState runtimeState = _runtimeStateLookup(action.ActorId, action.AbilityId);
            AbilityResolveRequest request = new(
                action,
                actor,
                definition,
                runtimeState,
                _unitsById(),
                _currentTick());

            return _executor.ResolveAbility(request);
        }
    }
}
