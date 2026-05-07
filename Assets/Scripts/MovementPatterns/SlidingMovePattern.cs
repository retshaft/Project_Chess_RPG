using UnityEngine;
using CheckmateRPG.Grid;

namespace CheckmateRPG.MovementPatterns
{
    public sealed class SlidingMovePattern : IMovePattern
    {
        private readonly bool _allowOrthogonal;
        private readonly bool _allowDiagonal;
        private readonly int _maxSteps;

        public SlidingMovePattern(bool allowOrthogonal, bool allowDiagonal, int maxSteps = 7)
        {
            _allowOrthogonal = allowOrthogonal;
            _allowDiagonal = allowDiagonal;
            _maxSteps = Mathf.Max(1, maxSteps);
        }

        public bool CanMove(Vector2Int currentCell, Vector2Int targetCell, GameObject unit)
        {
            if (GridSystem.Instance == null)
                return false;

            Vector2Int delta = targetCell - currentCell;
            if (delta == Vector2Int.zero)
                return false;

            int absX = Mathf.Abs(delta.x);
            int absY = Mathf.Abs(delta.y);

            bool isOrthogonalMove = (absX == 0 && absY > 0) || (absY == 0 && absX > 0);
            bool isDiagonalMove = absX == absY && absX > 0;

            if ((!_allowOrthogonal || !isOrthogonalMove) && (!_allowDiagonal || !isDiagonalMove))
                return false;

            int steps = Mathf.Max(absX, absY);
            if (steps > _maxSteps)
                return false;

            Vector2Int step = new Vector2Int(delta.x == 0 ? 0 : delta.x / absX, delta.y == 0 ? 0 : delta.y / absY);
            for (int i = 1; i < steps; i++)
            {
                Vector2Int traversed = currentCell + step * i;
                if (!GridSystem.Instance.IsValidCell(traversed) || !GridSystem.Instance.IsCellFree(traversed))
                    return false;
            }

            return true;
        }
    }
}
