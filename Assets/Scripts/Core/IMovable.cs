// IMovable.cs
// Interface for any entity that can occupy and move between grid tiles.

using UnityEngine;

namespace CheckmateRPG.Core
{
    public interface IMovable
    {
        /// <summary>
        /// Current grid position (column, row).
        /// </summary>
        Vector2Int GridPosition { get; }

        /// <summary>
        /// Whether the entity is currently in the middle of a move.
        /// </summary>
        bool IsMoving { get; }

        /// <summary>
        /// Initiate movement to the target grid cell.
        /// The implementation is responsible for validating the cell and performing the lerp.
        /// </summary>
        void MoveTo(Vector2Int targetGridPosition);
    }
}
