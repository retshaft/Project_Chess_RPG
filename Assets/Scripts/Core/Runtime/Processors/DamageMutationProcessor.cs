using System;
using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class DamageMutationProcessor
    {
        private readonly Func<Guid, UnitBrain> _unitLookup;
        private readonly SimulationRuntime _simulationRuntime;

        public DamageMutationProcessor(Func<Guid, UnitBrain> unitLookup, SimulationRuntime simulationRuntime)
        {
            _unitLookup = unitLookup ?? throw new ArgumentNullException(nameof(unitLookup));
            _simulationRuntime = simulationRuntime ?? throw new ArgumentNullException(nameof(simulationRuntime));
        }

        public IReadOnlyList<IGameEvent> Apply(DamageMutation mutation)
        {
            UnitBrain target = _unitLookup(mutation.TargetId);
            if (target == null || target.Health == null)
                return Array.Empty<IGameEvent>();

            int amount = Mathf.Max(0, mutation.Amount);
            float defPenetrationRatio = Mathf.Clamp01(mutation.DefPenetrationRatio);
            if (mutation.IsTrueDamage)
            {
                target.Health.ApplyTrueDamage(amount);
            }
            else
            {
                switch (mutation.DamageType)
                {
                    case DamageType.Physical:
                        target.Health.TakeDamage(amount, defPenetrationRatio);
                        break;
                    case DamageType.Magical:
                        target.Health.ApplyMagicDamage(amount);
                        break;
                    default:
                        target.Health.ApplyTrueDamage(amount);
                        break;
                }
            }

            int actualRemainingHp = Mathf.RoundToInt(target.Health.CurrentHealth);
            bool isDead = target.Health.IsDead;

            _simulationRuntime.SetUnitHP(mutation.TargetId, actualRemainingHp, OwnershipOwners.DamageMutationProcessor);
            if (isDead)
                _simulationRuntime.AddUnitStatusFlag(mutation.TargetId, UnitStatusFlags.Dead);

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

        public IReadOnlyList<IGameEvent> Apply(HealMutation mutation)
        {
            UnitBrain target = _unitLookup(mutation.TargetId);
            if (target == null || target.Health == null)
                return Array.Empty<IGameEvent>();

            int amount = Mathf.Max(0, mutation.Amount);
            target.Health.Heal(amount);
            int actualRemainingHp = Mathf.RoundToInt(target.Health.CurrentHealth);
            _simulationRuntime.SetUnitHP(mutation.TargetId, actualRemainingHp, OwnershipOwners.DamageMutationProcessor);
            return Array.Empty<IGameEvent>();
        }

        public IReadOnlyList<IGameEvent> Apply(DeathMutation mutation)
        {
            UnitBrain target = _unitLookup(mutation.TargetId);
            if (target == null)
                return Array.Empty<IGameEvent>();

            int currentTick = mutation.Tick;
            _simulationRuntime.ApplyDeadUnitLifecycle(mutation.TargetId, currentTick, OwnershipOwners.ActionScheduler);
            UnitKilledEvent unitKilledEvent = new(
                new CheckmateRPG.Core.Events.ActionEvents.UnitKilledPayload(mutation.TargetId, mutation.SourceId, currentTick),
                mutation.SourceId.ToString("N"),
                mutation.TargetId.ToString("N"));

            return new IGameEvent[] { unitKilledEvent };
        }
    }
}
