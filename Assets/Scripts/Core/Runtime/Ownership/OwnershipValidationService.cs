using System;

namespace CheckmateRPG.Core.Runtime.Ownership
{
    public sealed class OwnershipValidationService
    {
        public static OwnershipValidationService Default { get; } =
            new(OwnershipRegistry.CreateDefault());

        private readonly OwnershipRegistry _registry;

        public OwnershipValidationService(OwnershipRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public void EnsureAuthorized(string ownerName, string stateKey)
        {
            if (!_registry.TryGetRule(stateKey, out OwnershipRule rule))
                throw new InvalidOperationException($"No ownership rule is registered for state '{stateKey ?? "null"}'.");

            if (string.Equals(rule.OwnerName, ownerName, StringComparison.Ordinal))
                return;

            throw new InvalidOperationException(
                $"Unauthorized runtime mutation detected for '{stateKey}'. " +
                $"Expected owner '{rule.OwnerName}', but received '{ownerName ?? "null"}'.");
        }
    }
}
