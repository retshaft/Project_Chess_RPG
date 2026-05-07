using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Grid;

namespace CheckmateRPG.MovementPatterns
{
    public sealed class PawnMovePattern : IMovePattern
    {
        public bool CanMove(Vector2Int currentCell, Vector2Int targetCell, GameObject unit)
        {
            if (unit == null || GridSystem.Instance == null)
                return false;

            Vector2Int delta = targetCell - currentCell;
            if (delta.x != 0)
                return false;

            bool isEnemy = unit.TryGetComponent(out TeamComponent team) && team.IsEnemy;
            int forward = isEnemy ? -1 : 1;

            if (delta.y == forward)
                return true;

            if (delta.y != forward * 2)
                return false;

            if (!IsInitialPawnRow(currentCell.y, isEnemy))
                return false;

            Vector2Int intermediate = currentCell + new Vector2Int(0, forward);
            return GridSystem.Instance.IsValidCell(intermediate) && GridSystem.Instance.IsCellFree(intermediate);
        }

        private static bool IsInitialPawnRow(int row, bool isEnemy)
        {
            return isEnemy ? row >= GridSystem.GridHeight - 2 : row <= 1;
        }
    }
}
