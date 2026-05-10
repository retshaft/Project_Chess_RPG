using System;
using System.Collections.Generic;
using System.Linq;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public sealed class MutationOrderingService
    {
        private readonly MutationOrderingPolicy _policy;

        public MutationOrderingService(MutationOrderingPolicy policy = null)
        {
            _policy = policy ?? new MutationOrderingPolicy();
        }

        public IReadOnlyList<IRuntimeMutation> SortDeterministic(IReadOnlyList<IRuntimeMutation> mutations)
        {
            if (mutations == null || mutations.Count == 0)
                return Array.Empty<IRuntimeMutation>();

            return mutations
                .Select((mutation, index) => new OrderedMutation(mutation, index))
                .OrderBy(entry => (int)_policy.ResolveStage(entry.Mutation))
                .ThenBy(entry => GetTargetKey(entry.Mutation))
                .ThenBy(entry => GetSourceKey(entry.Mutation))
                .ThenBy(entry => GetStringKey(entry.Mutation), StringComparer.Ordinal)
                .ThenBy(entry => GetPrimaryNumericSortKey(entry.Mutation))
                .ThenBy(entry => GetSecondaryNumericSortKey(entry.Mutation))
                .ThenBy(entry => GetTertiaryNumericSortKey(entry.Mutation))
                .ThenBy(entry => GetQuaternaryNumericSortKey(entry.Mutation))
                .ThenBy(entry => GetQuinaryNumericSortKey(entry.Mutation))
                .ThenBy(entry => entry.OriginalIndex)
                .Select(entry => entry.Mutation)
                .ToArray();
        }

        public void SplitByDeathBoundary(
            IReadOnlyList<IRuntimeMutation> orderedMutations,
            out IReadOnlyList<IRuntimeMutation> preDeathMutations,
            out IReadOnlyList<IRuntimeMutation> cleanupMutations)
        {
            if (orderedMutations == null || orderedMutations.Count == 0)
            {
                preDeathMutations = Array.Empty<IRuntimeMutation>();
                cleanupMutations = Array.Empty<IRuntimeMutation>();
                return;
            }

            var preDeath = new List<IRuntimeMutation>();
            var cleanup = new List<IRuntimeMutation>();

            for (int i = 0; i < orderedMutations.Count; i++)
            {
                IRuntimeMutation mutation = orderedMutations[i];
                if (mutation == null)
                    continue;

                MutationOrderingStage stage = _policy.ResolveStage(mutation);
                if (_policy.IsPreDeathStage(stage))
                    preDeath.Add(mutation);
                else
                    cleanup.Add(mutation);
            }

            preDeathMutations = preDeath;
            cleanupMutations = cleanup;
        }

        private static Guid GetTargetKey(IRuntimeMutation mutation)
        {
            return mutation?.TargetId ?? Guid.Empty;
        }

        private static Guid GetSourceKey(IRuntimeMutation mutation)
        {
            return mutation switch
            {
                DamageMutation damage => damage.SourceId,
                ApplyEffectMutation effect => effect.SourceId,
                _ => Guid.Empty
            };
        }

        private static string GetStringKey(IRuntimeMutation mutation)
        {
            return mutation switch
            {
                ApplyEffectMutation effect => effect.EffectId ?? string.Empty,
                _ => string.Empty
            };
        }

        private static int GetPrimaryNumericSortKey(IRuntimeMutation mutation)
        {
            return mutation switch
            {
                MovementMutation movement => movement.From.x,
                DamageMutation damage => damage.Amount,
                ApplyEffectMutation effect => effect.DurationTicks,
                _ => 0
            };
        }

        private static int GetSecondaryNumericSortKey(IRuntimeMutation mutation)
        {
            return mutation switch
            {
                MovementMutation movement => movement.From.y,
                DamageMutation damage => damage.IsCritical ? 1 : 0,
                ApplyEffectMutation effect => effect.TickInterval,
                _ => 0
            };
        }

        private static int GetTertiaryNumericSortKey(IRuntimeMutation mutation)
        {
            return mutation switch
            {
                MovementMutation movement => movement.To.x,
                ApplyEffectMutation effect => effect.InitialTickIn,
                _ => 0
            };
        }

        private static int GetQuaternaryNumericSortKey(IRuntimeMutation mutation)
        {
            return mutation switch
            {
                MovementMutation movement => movement.To.y,
                ApplyEffectMutation effect => effect.StackCount,
                _ => 0
            };
        }

        private static int GetQuinaryNumericSortKey(IRuntimeMutation mutation)
        {
            return mutation switch
            {
                ApplyEffectMutation effect => BitConverter.SingleToInt32Bits(effect.Magnitude),
                _ => 0
            };
        }

        private readonly record struct OrderedMutation(IRuntimeMutation Mutation, int OriginalIndex);
    }
}
