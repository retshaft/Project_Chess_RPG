using UnityEngine;
using CheckmateRPG.Grid;

namespace CheckmateRPG.MovementPatterns
{
    public sealed class KnightMovePattern : IMovePattern
    {
        public bool CanMove(Vector2Int currentCell, Vector2Int targetCell, GameObject unit)
        {
            if (!IsInBounds(targetCell))
                return false;

            Vector2Int delta = targetCell - currentCell;
            int absX = Mathf.Abs(delta.x);
            int absY = Mathf.Abs(delta.y);
            return (absX == 2 && absY == 1) || (absX == 1 && absY == 2);
        }

        private static bool IsInBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < GridSystem.GridWidth &&
                   cell.y >= 0 && cell.y < GridSystem.GridHeight;
        }
    }
}
