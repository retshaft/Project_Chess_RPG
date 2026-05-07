using UnityEngine;

namespace CheckmateRPG.MovementPatterns
{
    public sealed class KnightMovePattern : IMovePattern
    {
        public bool CanMove(Vector2Int currentCell, Vector2Int targetCell, GameObject unit)
        {
            Vector2Int delta = targetCell - currentCell;
            int absX = Mathf.Abs(delta.x);
            int absY = Mathf.Abs(delta.y);
            return (absX == 2 && absY == 1) || (absX == 1 && absY == 2);
        }
    }
}
