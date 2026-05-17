using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Effects;
using UnityEngine;

namespace CheckmateRPG.Core.Actions
{
    public sealed class AbilityActionCommand : BaseActionCommand
    {
        public AbilityActionCommand(
            Guid actorId,
            string abilityId,
            IReadOnlyList<Guid> targetIds,
            int startTick,
            ActionSpeedTier speedTier,
            int recoveryDurationTicks = 1,
            bool isInterruptible = true,
            ActionDefinition? definition = null,
            ActionLockType intentLockType = ActionLockType.CastLock,
            ActionConcurrencyPolicy concurrencyPolicy = ActionConcurrencyPolicy.Reject,
            IReadOnlyList<Vector2Int> targetCells = null,
            IReadOnlyList<EffectRuntimeState> runtimeEffects = null,
            int apCost = 0,
            int spCost = 0,
            int cooldownTicks = 0)
            : base(
                actorId,
                startTick,
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier),
                startTick + ActionTimelineFormula.ToActionDurationTicks(speedTier) + Math.Max(0, recoveryDurationTicks),
                speedTier,
                isInterruptible,
                definition: definition,
                intentLockType: intentLockType,
                concurrencyPolicy: concurrencyPolicy)
        {
            AbilityId = abilityId ?? string.Empty;
            TargetIds = targetIds ?? Array.Empty<Guid>();
            TargetCells = targetCells ?? Array.Empty<Vector2Int>();
            RuntimeEffects = CloneRuntimeEffects(runtimeEffects);
            ApCost = Math.Max(0, apCost);
            SPCost = Math.Max(0, spCost);
            CooldownTicks = Math.Max(0, cooldownTicks);
        }

        public string AbilityId { get; }
        public IReadOnlyList<Guid> TargetIds { get; }
        public IReadOnlyList<Vector2Int> TargetCells { get; }
        public IReadOnlyList<EffectRuntimeState> RuntimeEffects { get; }
        public int ApCost { get; }
        public int SPCost { get; }
        public int CooldownTicks { get; }

        private static IReadOnlyList<EffectRuntimeState> CloneRuntimeEffects(IReadOnlyList<EffectRuntimeState> runtimeEffects)
        {
            if (runtimeEffects == null || runtimeEffects.Count == 0)
                return Array.Empty<EffectRuntimeState>();

            var clone = new List<EffectRuntimeState>(runtimeEffects.Count);
            for (int i = 0; i < runtimeEffects.Count; i++)
            {
                EffectRuntimeState effect = runtimeEffects[i];
                if (effect == null)
                    continue;
                clone.Add(new EffectRuntimeState(effect));
            }

            return clone;
        }
    }
}
