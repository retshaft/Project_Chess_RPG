using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class CooldownMutationProcessor
    {
        private readonly Func<Guid, string, AbilityRuntimeState> _abilityStateLookup;
        private readonly Func<int> _currentTickProvider;

        public CooldownMutationProcessor(
            Func<Guid, string, AbilityRuntimeState> abilityStateLookup,
            Func<int> currentTickProvider)
        {
            _abilityStateLookup = abilityStateLookup ?? throw new ArgumentNullException(nameof(abilityStateLookup));
            _currentTickProvider = currentTickProvider ?? throw new ArgumentNullException(nameof(currentTickProvider));
        }

        public IReadOnlyList<IGameEvent> Apply(CooldownMutation mutation)
        {
            if (mutation.TargetUnitId == Guid.Empty || string.IsNullOrWhiteSpace(mutation.TargetAbilityId))
                return Array.Empty<IGameEvent>();

            AbilityRuntimeState state = _abilityStateLookup(mutation.TargetUnitId, mutation.TargetAbilityId);
            if (state == null)
                return Array.Empty<IGameEvent>();

            int currentTick = Mathf.Max(0, _currentTickProvider());
            state.UpdateCooldown(currentTick, OwnershipOwners.TickScheduler);
            state.MutateCooldownByDelta(mutation.DurationChange, currentTick, OwnershipOwners.TickScheduler);
            return Array.Empty<IGameEvent>();
        }
    }
}
