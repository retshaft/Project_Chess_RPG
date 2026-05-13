using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;
using UnityEngine;

namespace CheckmateRPG.Core.Prediction
{
    /// <summary>
    /// Applies <see cref="IRuntimeMutation"/> instances to the isolated
    /// <see cref="SimulationRuntime"/> clone inside a
    /// <see cref="PredictionSimulationContext"/> and records the
    /// corresponding prediction outcomes.
    /// <para>
    /// Unlike the production <c>RuntimeMutationProcessor</c>, this class never
    /// touches <c>UnitBrain</c>, never broadcasts events, and never persists
    /// any state.
    /// </para>
    /// </summary>
    internal static class PredictionMutationApplier
    {
        /// <summary>
        /// Applies an ordered list of mutations to the cloned runtime in
        /// <paramref name="context"/> and records each outcome.
        /// </summary>
        public static void Apply(
            IReadOnlyList<IRuntimeMutation> orderedMutations,
            PredictionSimulationContext context)
        {
            if (orderedMutations == null || orderedMutations.Count == 0 || context == null)
                return;

            for (int i = 0; i < orderedMutations.Count; i++)
            {
                switch (orderedMutations[i])
                {
                    case DamageMutation damage:
                        ApplyDamage(damage, context);
                        break;
                    case HealMutation heal:
                        ApplyHeal(heal, context);
                        break;
                    case MovementMutation movement:
                        ApplyMovement(movement, context);
                        break;
                    case MoveMutation movement:
                        ApplyMovement(new MovementMutation(
                            movement.MutationId,
                            movement.TargetId,
                            movement.From,
                            movement.To,
                            movement.Context), context);
                        break;
                    case DeathMutation death:
                        ApplyDeath(death, context);
                        break;
                    // ApplyEffectMutation is registered in the clone for completeness
                    // but does not produce any prediction outcome record on its own.
                    case ApplyEffectMutation effect:
                        ApplyEffect(effect, context);
                        break;
                }
            }
        }

        // ── Damage ────────────────────────────────────────────────────────────────

        private static void ApplyDamage(DamageMutation mutation, PredictionSimulationContext context)
        {
            SimulationRuntime runtime = context.PredictedRuntime;
            if (!runtime.TryGetUnit(mutation.TargetId, out IReadOnlyUnitRuntimeState state))
                return;

            int amount = Mathf.Max(0, mutation.Amount);
            int resultingHp = Mathf.Max(0, state.HP - amount);

            runtime.SetUnitHP(mutation.TargetId, resultingHp, OwnershipOwners.DamageMutationProcessor);

            context.RecordDamage(new PredictedDamage(
                mutation.MutationId,
                mutation.TargetId,
                mutation.SourceId,
                amount,
                mutation.IsCritical,
                resultingHp));

            if (resultingHp <= 0 && (state.StatusFlags & UnitStatusFlags.Dead) == 0)
            {
                // Determine which action caused the killing blow via source lookup.
                Guid killerActionId = ResolveKillerActionId(mutation.SourceId, runtime);
                context.RecordDeath(new PredictedDeath(mutation.TargetId, killerActionId));
                runtime.AddUnitStatusFlag(mutation.TargetId, UnitStatusFlags.Dead);
            }
        }

        private static void ApplyHeal(HealMutation mutation, PredictionSimulationContext context)
        {
            SimulationRuntime runtime = context.PredictedRuntime;
            if (!runtime.TryGetUnit(mutation.TargetId, out IReadOnlyUnitRuntimeState state))
                return;

            int amount = Mathf.Max(0, mutation.Amount);
            int resultingHp = Mathf.Max(0, state.HP + amount);
            runtime.SetUnitHP(mutation.TargetId, resultingHp, OwnershipOwners.DamageMutationProcessor);
        }

        // ── Movement ──────────────────────────────────────────────────────────────

        private static void ApplyMovement(MovementMutation mutation, PredictionSimulationContext context)
        {
            SimulationRuntime runtime = context.PredictedRuntime;
            if (!runtime.TryGetUnit(mutation.TargetId, out IReadOnlyUnitRuntimeState state))
                return;

            // Skip if unit is already dead.
            if ((state.StatusFlags & UnitStatusFlags.Dead) != 0)
                return;

            runtime.SetUnitPosition(mutation.TargetId, mutation.To, OwnershipOwners.MovementMutationProcessor);

            context.RecordPosition(new PredictedPosition(
                mutation.TargetId,
                mutation.From,
                mutation.To));
        }

        // ── Effect ────────────────────────────────────────────────────────────────

        private static void ApplyEffect(ApplyEffectMutation mutation, PredictionSimulationContext context)
        {
            SimulationRuntime runtime = context.PredictedRuntime;

            var effectState = new Effects.EffectRuntimeState(
                mutation.EffectId,
                mutation.SourceId,
                mutation.TargetId,
                mutation.DurationTicks,
                mutation.StackCount,
                mutation.TickInterval,
                mutation.InitialTickIn,
                mutation.Magnitude,
                timingPhase: mutation.TimingPhase,
                actionSpeedLevel: mutation.ActionSpeedLevel,
                isReaction: mutation.IsReaction,
                stackPolicy: mutation.StackPolicy,
                maxStackCap: mutation.MaxStackCap);

            runtime.RegisterEffect(effectState);
        }

        private static void ApplyDeath(DeathMutation mutation, PredictionSimulationContext context)
        {
            SimulationRuntime runtime = context.PredictedRuntime;
            if (!runtime.TryGetUnit(mutation.TargetId, out IReadOnlyUnitRuntimeState state))
                return;

            if ((state.StatusFlags & UnitStatusFlags.Dead) != 0)
                return;

            Guid killerActionId = ResolveKillerActionId(mutation.SourceId, runtime);
            context.RecordDeath(new PredictedDeath(mutation.TargetId, killerActionId));
            runtime.ApplyDeadUnitLifecycle(mutation.TargetId, mutation.Tick, OwnershipOwners.ActionScheduler);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Guid ResolveKillerActionId(Guid sourceUnitId, SimulationRuntime runtime)
        {
            if (sourceUnitId == Guid.Empty)
                return Guid.Empty;

            foreach (System.Collections.Generic.KeyValuePair<Guid, IReadOnlyActionState> pair in runtime.ActiveActions)
            {
                if (pair.Value != null && pair.Value.ActorId == sourceUnitId)
                    return pair.Key;
            }

            return Guid.Empty;
        }
    }
}
