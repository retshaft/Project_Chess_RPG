// IAttackable.cs
// Interface for any entity that can perform an attack action.
// Designed to be extended with AP, range, or area-of-effect checks later.

using UnityEngine;

namespace CheckmateRPG.Core
{
    public interface IAttackable
    {
        /// <summary>
        /// Whether the entity is currently able to attack (cooldown expired, alive, etc.).
        /// </summary>
        bool CanAttack { get; }

        /// <summary>
        /// Attempt to attack a target at the given world position.
        /// The implementation decides whether the target is in range.
        /// </summary>
        void Attack(GameObject target);
    }
}
