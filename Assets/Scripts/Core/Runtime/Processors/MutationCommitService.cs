using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public readonly record struct MutationCommitResult(
        IReadOnlyList<IRuntimeMutation> AppliedMutations,
        IReadOnlyList<IGameEvent> StagedEvents)
    {
        public static MutationCommitResult Empty =>
            new(Array.Empty<IRuntimeMutation>(), Array.Empty<IGameEvent>());
    }

    /// <summary>
    /// Authoritative commit point that applies queued runtime mutations in one batch.
    /// </summary>
    public sealed class MutationCommitService
    {
        private readonly RuntimeMutationProcessor _mutationProcessor;
        private bool _isCommitting;

        public MutationCommitService(RuntimeMutationProcessor mutationProcessor)
        {
            _mutationProcessor = mutationProcessor ?? throw new ArgumentNullException(nameof(mutationProcessor));
        }

        public MutationCommitResult Commit(MutationQueue queue, MutationOrderingService orderingService)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));
            if (orderingService == null)
                throw new ArgumentNullException(nameof(orderingService));
            if (queue.Count == 0)
                return MutationCommitResult.Empty;
            if (_isCommitting)
                throw new InvalidOperationException("Mutation commit re-entry is not allowed.");

            _isCommitting = true;
            try
            {
                IReadOnlyList<IRuntimeMutation> ordered = queue.CreateOrderedSnapshot(orderingService);
                if (ordered.Count == 0)
                    return MutationCommitResult.Empty;

                IReadOnlyList<IGameEvent> stagedEvents = _mutationProcessor.Apply(ordered);
                return new MutationCommitResult(ordered, stagedEvents);
            }
            finally
            {
                _isCommitting = false;
            }
        }
    }
}
