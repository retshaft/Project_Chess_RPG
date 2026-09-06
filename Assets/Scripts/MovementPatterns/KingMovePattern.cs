using UnityEngine;
using CheckmateRPG.Grid;

namespace CheckmateRPG.MovementPatterns
{
    public sealed class KingMovePattern : IMovePattern
    {
        public bool CanMove(Vector2Int currentCell, Vector2Int targetCell, GameObject unit)
        {
            if (!IsInBounds(targetCell))
                return false;

            Vector2Int delta = targetCell - currentCell;
            if (delta == Vector2Int.zero)
                return false;

            return Mathf.Abs(delta.x) <= 1 && Mathf.Abs(delta.y) <= 1;
        }

        private static bool IsInBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < GridSystem.GridWidth &&
                   cell.y >= 0 && cell.y < GridSystem.GridHeight;
        }
    }
}
