using UnityEngine;

namespace CheckmateRPG.MovementPatterns
{
    public sealed class KingMovePattern : IMovePattern
    {
        public bool CanMove(Vector2Int currentCell, Vector2Int targetCell, GameObject unit)
        {
            Vector2Int delta = targetCell - currentCell;
            if (delta == Vector2Int.zero)
                return false;

            return Mathf.Abs(delta.x) <= 1 && Mathf.Abs(delta.y) <= 1;
        }
    }
}
