using System;
using System.Collections.Generic;
using System.Linq;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public readonly record struct QueuedMutation(
        IRuntimeMutation Mutation,
        ActionSpeedTier ActionSpeedLevel,
        int ResolveOrder,
        int Sequence);

    public sealed class MutationQueue
    {
        private const int DefaultResolveOrder = int.MaxValue;
        private readonly List<QueuedMutation> _queued = new();
        private int _nextSequence;

        public int Count => _queued.Count;

        public void Enqueue(IMutation mutation)
        {
            if (mutation is not IRuntimeMutation runtimeMutation)
                return;

            Enqueue(runtimeMutation, ActionSpeedTier.Normal, DefaultResolveOrder);
        }

        public void Enqueue(IRuntimeMutation mutation, ActionSpeedTier actionSpeedLevel, int resolveOrder)
        {
            if (mutation == null)
                return;

            _queued.Add(new QueuedMutation(
                mutation,
                actionSpeedLevel,
                resolveOrder,
                _nextSequence++));
        }

        public void EnqueueRange(IReadOnlyList<QueuedMutation> mutations)
        {
            if (mutations == null || mutations.Count == 0)
                return;

            for (int i = 0; i < mutations.Count; i++)
            {
                QueuedMutation queuedMutation = mutations[i];
                if (queuedMutation.Mutation == null)
                    continue;

                _queued.Add(new QueuedMutation(
                    queuedMutation.Mutation,
                    queuedMutation.ActionSpeedLevel,
                    queuedMutation.ResolveOrder,
                    _nextSequence++));
            }
        }

        public IRuntimeMutation Peek()
        {
            return _queued.Count == 0 ? null : _queued[0].Mutation;
        }

        public IRuntimeMutation Dequeue()
        {
            if (_queued.Count == 0)
                return null;

            IRuntimeMutation mutation = _queued[0].Mutation;
            _queued.RemoveAt(0);
            return mutation;
        }

        public void Clear()
        {
            _queued.Clear();
            _nextSequence = 0;
        }

        public IReadOnlyList<IRuntimeMutation> CreateOrderedSnapshot(MutationOrderingService orderingService)
        {
            if (orderingService == null)
                throw new ArgumentNullException(nameof(orderingService));
            if (_queued.Count == 0)
                return Array.Empty<IRuntimeMutation>();

            IOrderedEnumerable<QueuedMutation> actionOrdered = _queued
                .OrderBy(entry => (int)entry.ActionSpeedLevel)
                .ThenBy(entry => entry.ResolveOrder)
                .ThenBy(entry => entry.Sequence);

            var grouped = actionOrdered
                .GroupBy(entry => new ActionMutationOrderingKey(entry.ActionSpeedLevel, entry.ResolveOrder))
                .ToArray();

            var merged = new List<IRuntimeMutation>(_queued.Count);
            for (int i = 0; i < grouped.Length; i++)
            {
                var perActionMutations = new List<IRuntimeMutation>();
                foreach (QueuedMutation entry in grouped[i])
                {
                    if (entry.Mutation != null)
                        perActionMutations.Add(entry.Mutation);
                }

                IReadOnlyList<IRuntimeMutation> deterministic = orderingService.SortDeterministic(perActionMutations);
                for (int m = 0; m < deterministic.Count; m++)
                    merged.Add(deterministic[m]);
            }

            return merged;
        }

        private readonly record struct ActionMutationOrderingKey(ActionSpeedTier Speed, int ResolveOrder);
    }
}
