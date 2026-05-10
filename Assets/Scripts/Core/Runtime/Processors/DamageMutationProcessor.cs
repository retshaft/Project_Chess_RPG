using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class DamageMutationProcessor
    {
        private readonly Func<Guid, UnitBrain> _unitLookup;

        public DamageMutationProcessor(Func<Guid, UnitBrain> unitLookup)
        {
            _unitLookup = unitLookup ?? throw new ArgumentNullException(nameof(unitLookup));
        }

        public IReadOnlyList<IGameEvent> Apply(DamageMutation mutation)
        {
            UnitBrain target = _unitLookup(mutation.TargetId);
            if (target == null || target.Health == null)
                return Array.Empty<IGameEvent>();

            int amount = Mathf.Max(0, mutation.Amount);
            target.Health.ApplyTrueDamage(amount);

            int actualRemainingHp = Mathf.RoundToInt(target.Health.CurrentHealth);
            bool isDead = target.Health.IsDead;

            UnitRuntimeState state = target.RuntimeState;
            if (state != null)
            {
                state.SetHP(actualRemainingHp, OwnershipOwners.DamageMutationProcessor);
                if (isDead)
                    state.AddStatusFlag(UnitStatusFlags.Dead);
            }

            DamageAppliedEvent damageAppliedEvent = new(
                new DamageAppliedPayload(
                    mutation.MutationId,
                    mutation.SourceId,
                    mutation.TargetId,
                    amount,
                    actualRemainingHp,
                    mutation.IsCritical),
                mutation.MutationId.ToString("N"),
                mutation.TargetId.ToString("N"));

            return new IGameEvent[] { damageAppliedEvent };
        }
    }
}
