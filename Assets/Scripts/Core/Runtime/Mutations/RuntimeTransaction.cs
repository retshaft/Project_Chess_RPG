using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Runtime.Mutations
{
    public sealed class RuntimeTransaction
    {
        private readonly List<IRuntimeMutation> _buffer = new();

        public bool IsCommitted { get; private set; }

        public int Count => _buffer.Count;

        public void Buffer(IRuntimeMutation mutation)
        {
            EnsureNotCommitted();
            if (mutation == null)
                return;

            _buffer.Add(mutation);
        }

        public void BufferRange(IReadOnlyList<IRuntimeMutation> mutations)
        {
            EnsureNotCommitted();
            if (mutations == null || mutations.Count == 0)
                return;

            for (int i = 0; i < mutations.Count; i++)
                Buffer(mutations[i]);
        }

        public IReadOnlyList<IRuntimeMutation> CreateOrderedSnapshot(MutationOrderingService orderingService)
        {
            if (orderingService == null)
                throw new ArgumentNullException(nameof(orderingService));

            return orderingService.SortDeterministic(_buffer);
        }

        public void Commit()
        {
            if (IsCommitted)
                return;

            IsCommitted = true;
            _buffer.Clear();
        }

        public static RuntimeTransaction From(IReadOnlyList<IRuntimeMutation> mutations)
        {
            var transaction = new RuntimeTransaction();
            transaction.BufferRange(mutations);
            return transaction;
        }

        private void EnsureNotCommitted()
        {
            if (IsCommitted)
                throw new InvalidOperationException("RuntimeTransaction is already committed.");
        }
    }
}
