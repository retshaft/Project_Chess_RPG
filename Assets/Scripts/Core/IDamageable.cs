// IDamageable.cs
// Interface for any entity that can receive damage.
// Kept minimal so both units and future destructible objects can implement it.

namespace CheckmateRPG.Core
{
    public interface IDamageable
    {
        /// <summary>
        /// Current hit points.
        /// </summary>
        float CurrentHealth { get; }

        /// <summary>
        /// Maximum hit points.
        /// </summary>
        float MaxHealth { get; }

        /// <summary>
        /// Returns true when the entity has no remaining health.
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// Apply incoming damage. Negative values are treated as 0.
        /// </summary>
        void TakeDamage(float amount);

        /// <summary>
        /// Restore hit points up to MaxHealth. Negative values are treated as 0.
        /// </summary>
        void Heal(float amount);
    }
}
