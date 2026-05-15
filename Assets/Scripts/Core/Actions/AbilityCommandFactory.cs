using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Abilities;
using CheckmateRPG.Core.Actions.Resolvers;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Runtime;
using UnityEngine;

namespace CheckmateRPG.Core.Actions
{
    public static class AbilityCommandFactory
    {
        private static readonly ActionDefinition DefaultDefinition =
            new(InterruptPriority.Normal, InterruptWindow.CastingInterruptible, true, true);

        public static AbilityActionCommand CreateCommand(
            AbilityDataSO data,
            UnitRuntimeState source,
            Vector2Int targetPos,
            int tick)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            IReadOnlyList<Vector2Int> targetCells = TargetingShapeResolver.Resolve(
                data.TargetShape,
                source.Position,
                targetPos,
                data.Range);
            IReadOnlyList<Guid> targetIds = ResolveTargetIds(data.TargetShape, source, targetCells);
            IReadOnlyList<EffectRuntimeState> runtimeEffects = CloneEffects(data, source, targetCells, tick);

            return new AbilityActionCommand(
                source.UnitId,
                data.AbilityId,
                targetIds,
                tick + 1,
                ActionSpeedTier.Normal,
                definition: DefaultDefinition,
                targetCells: targetCells,
                runtimeEffects: runtimeEffects,
                apCost: data.ApCost,
                cooldownTicks: data.CooldownTicks);
        }

        private static IReadOnlyList<Guid> ResolveTargetIds(
            TargetingShape shape,
            UnitRuntimeState source,
            IReadOnlyList<Vector2Int> targetCells)
        {
            if (shape != TargetingShape.Self || source == null)
                return Array.Empty<Guid>();

            if (targetCells == null || targetCells.Count == 0)
                return Array.Empty<Guid>();

            return new[] { source.UnitId };
        }

        private static IReadOnlyList<EffectRuntimeState> CloneEffects(
            AbilityDataSO data,
            UnitRuntimeState source,
            IReadOnlyList<Vector2Int> targetCells,
            int tick)
        {
            if (data == null || data.Effects == null || data.Effects.Count == 0 || source == null)
                return Array.Empty<EffectRuntimeState>();

            IReadOnlyList<Vector2Int> resolvedCells = targetCells ?? Array.Empty<Vector2Int>();
            if (resolvedCells.Count == 0 && data.TargetShape != TargetingShape.Self)
                return Array.Empty<EffectRuntimeState>();

            var clones = new List<EffectRuntimeState>(data.Effects.Count * Math.Max(1, resolvedCells.Count));
            for (int i = 0; i < data.Effects.Count; i++)
            {
                EffectDataSO effectData = data.Effects[i];
                if (effectData == null)
                    continue;

                if (data.TargetShape == TargetingShape.Self)
                {
                    clones.Add(BuildRuntimeEffect(data.AbilityId, effectData, source.UnitId, source.UnitId, tick, i));
                    continue;
                }

                for (int t = 0; t < resolvedCells.Count; t++)
                {
                    clones.Add(BuildRuntimeEffect(data.AbilityId, effectData, source.UnitId, Guid.Empty, tick, i));
                }
            }

            return clones;
        }

        private static EffectRuntimeState BuildRuntimeEffect(
            string abilityId,
            EffectDataSO data,
            Guid sourceId,
            Guid targetId,
            int tick,
            int effectIndex)
        {
            return new EffectRuntimeState(
                effectId: BuildEffectId(abilityId, data, effectIndex),
                sourceId: sourceId,
                targetId: targetId,
                remainingTick: Mathf.Max(1, data.DurationTicks),
                stackCount: Mathf.Max(1, data.MaxStacks),
                tickInterval: 1,
                nextTickIn: 1,
                magnitude: Mathf.Max(0f, data.BaseValue),
                timingPhase: EffectTimingPhase.OnTickEnd,
                actionSpeedLevel: ActionSpeedTier.Normal,
                isReaction: false,
                appliedTick: Mathf.Max(0, tick),
                stackPolicy: EffectStackPolicy.Refresh,
                maxStackCap: Mathf.Max(1, data.MaxStacks));
        }

        private static string BuildEffectId(string abilityId, EffectDataSO data, int index)
        {
            string ability = string.IsNullOrWhiteSpace(abilityId) ? "ability" : abilityId.Trim();
            string type = data != null ? data.Type.ToString() : "effect";
            return $"{ability}:{type}:{index}";
        }
    }
}
