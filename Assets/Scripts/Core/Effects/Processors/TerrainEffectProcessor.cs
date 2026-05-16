using CheckmateRPG.Components;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core.Effects.Processors
{
    public sealed class TerrainEffectProcessor : IEffectProcessor
    {
        public bool CanProcess(IReadOnlyEffectRuntimeState effect)
        {
            if (effect == null)
                return false;

            return effect.EffectId == TerrainEffectIds.SpikesTrueDot ||
                   effect.EffectId == TerrainEffectIds.SanctuaryHot ||
                   effect.EffectId == TerrainEffectIds.SanctuaryDefense;
        }

        public void OnApplied(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            if (effect == null || effect.EffectId != TerrainEffectIds.SanctuaryDefense)
                return;

            if (context.TryGetUnit(effect.TargetId, out UnitBrain unit))
                UpdateSanctuaryDefenseBonus(unit, apply: IsSanctuaryAlly(unit) && IsOnTile(unit, TileType.Sanctuary));
        }

        public EffectProcessorResult OnTick(
            EffectSystemContext context,
            EffectMutationContext mutationContext,
            EffectMutationFactory mutationFactory,
            IReadOnlyEffectRuntimeState effect)
        {
            if (effect == null || mutationFactory == null || !context.TryGetUnit(effect.TargetId, out UnitBrain unit))
                return EffectProcessorResult.Empty;

            if (effect.EffectId == TerrainEffectIds.SanctuaryDefense)
            {
                UpdateSanctuaryDefenseBonus(unit, apply: IsSanctuaryAlly(unit) && IsOnTile(mutationContext.TargetUnit.Position, TileType.Sanctuary));
                return EffectProcessorResult.Empty;
            }

            if (!mutationContext.TargetUnit.Exists || mutationContext.TargetUnit.IsDead || mutationContext.TargetUnit.MaxHp <= 0)
                return EffectProcessorResult.Empty;

            int stacks = Mathf.Max(1, effect.StackCount);
            float magnitude = Mathf.Max(0f, effect.Magnitude);

            if (effect.EffectId == TerrainEffectIds.SpikesTrueDot)
            {
                if (!IsOnTile(mutationContext.TargetUnit.Position, TileType.Spikes))
                    return EffectProcessorResult.Empty;

                float damage = mutationContext.TargetUnit.MaxHp * GridSystem.SpikeDamagePercentPerSecond * stacks * magnitude;
                int amount = Mathf.RoundToInt(damage);
                IRuntimeMutation mutation = mutationFactory.CreateDamage(
                    mutationContext,
                    amount,
                    DamageType.Physical,
                    isCritical: false,
                    isTrueDamage: true);
                return mutation != null
                    ? new EffectProcessorResult(new[] { mutation }, -amount)
                    : EffectProcessorResult.Empty;
            }

            if (effect.EffectId == TerrainEffectIds.SanctuaryHot)
            {
                if (!IsSanctuaryAlly(unit) || !IsOnTile(mutationContext.TargetUnit.Position, TileType.Sanctuary))
                    return EffectProcessorResult.Empty;

                float heal = mutationContext.TargetUnit.MaxHp * GridSystem.SanctuaryHealPercentPerSecond * stacks * magnitude;
                int amount = Mathf.RoundToInt(heal);
                IRuntimeMutation mutation = mutationFactory.CreateHeal(mutationContext, amount);
                return mutation != null
                    ? new EffectProcessorResult(new[] { mutation }, amount)
                    : EffectProcessorResult.Empty;
            }

            return EffectProcessorResult.Empty;
        }

        public void OnExpired(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            if (effect == null || effect.EffectId != TerrainEffectIds.SanctuaryDefense)
                return;

            if (context.TryGetUnit(effect.TargetId, out UnitBrain unit))
                UpdateSanctuaryDefenseBonus(unit, apply: false);
        }

        private static bool IsOnTile(UnitBrain unit, TileType tileType)
        {
            if (unit == null)
                return false;

            GridSystem grid = GridSystem.Instance;
            if (grid == null)
                return false;

            Vector2Int position = unit.RuntimeState != null
                ? unit.RuntimeState.Position
                : (unit.Movement != null ? unit.Movement.GridPosition : default);
            if (!grid.IsValidCell(position))
                return false;

            return grid.GetTileType(position) == tileType;
        }

        private static bool IsOnTile(Vector2Int position, TileType tileType)
        {
            GridSystem grid = GridSystem.Instance;
            if (grid == null || !grid.IsValidCell(position))
                return false;

            return grid.GetTileType(position) == tileType;
        }

        private static bool IsSanctuaryAlly(UnitBrain unit)
        {
            if (unit == null)
                return false;

            TeamComponent team = unit.GetComponent<TeamComponent>();
            return team != null && team.IsPlayer;
        }

        private static void UpdateSanctuaryDefenseBonus(UnitBrain unit, bool apply)
        {
            if (unit?.Health == null)
                return;

            unit.Health.SetDefenseBonus(apply ? GridSystem.SanctuaryDefenseBonus : 0f);
        }
    }
}
