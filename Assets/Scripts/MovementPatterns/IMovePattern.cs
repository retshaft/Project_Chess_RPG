using UnityEngine;

namespace CheckmateRPG.MovementPatterns
{
    public interface IMovePattern
    {
        bool CanMove(Vector2Int currentCell, Vector2Int targetCell, GameObject unit);
    }
}
