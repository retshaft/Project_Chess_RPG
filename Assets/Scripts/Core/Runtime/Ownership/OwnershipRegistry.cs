using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Runtime.Ownership
{
    public sealed class OwnershipRegistry
    {
        private readonly Dictionary<string, OwnershipRule> _rules = new(StringComparer.Ordinal);

        public OwnershipRegistry(IEnumerable<OwnershipRule> rules)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            foreach (OwnershipRule rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.StateKey) || string.IsNullOrWhiteSpace(rule.OwnerName))
                    throw new ArgumentException("Ownership rules must define both a state key and an owner name.", nameof(rules));

                _rules[rule.StateKey] = rule;
            }
        }

        public static OwnershipRegistry CreateDefault()
        {
            return new OwnershipRegistry(new[]
            {
                new OwnershipRule(OwnershipStateKeys.HP, OwnershipOwners.DamageMutationProcessor),
                new OwnershipRule(OwnershipStateKeys.Position, OwnershipOwners.MovementMutationProcessor),
                new OwnershipRule(OwnershipStateKeys.Cooldown, OwnershipOwners.TickScheduler),
                new OwnershipRule(OwnershipStateKeys.EffectStack, OwnershipOwners.EffectSystem),
                new OwnershipRule(OwnershipStateKeys.ActionState, OwnershipOwners.ActionScheduler)
            });
        }

        public bool TryGetRule(string stateKey, out OwnershipRule rule)
        {
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                rule = default;
                return false;
            }

            return _rules.TryGetValue(stateKey, out rule);
        }
    }
}
