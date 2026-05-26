using System;
using CheckmateRPG.Data;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public static class CombatPatternRules
    {
        public static bool IsAttackReachable(
            ChessPieceType pieceType,
            bool isEnemy,
            Vector2Int origin,
            Vector2Int target,
            int attackRange,
            Func<Vector2Int, bool> isBlockedCell = null)
        {
            int dx = target.x - origin.x;
            int dy = target.y - origin.y;
            int adx = Mathf.Abs(dx);
            int ady = Mathf.Abs(dy);

            switch (pieceType)
            {
                case ChessPieceType.Knight:
                    return (adx == 2 && ady == 1) || (adx == 1 && ady == 2);

                case ChessPieceType.Rook:
                    if (dx != 0 && dy != 0)
                        return false;
                    return IsClearPath(origin, target, isBlockedCell);

                case ChessPieceType.Bishop:
                    if (adx != ady)
                        return false;
                    return IsClearPath(origin, target, isBlockedCell);

                case ChessPieceType.Queen:
                    if (!(dx == 0 || dy == 0 || adx == ady))
                        return false;
                    return IsClearPath(origin, target, isBlockedCell);

                case ChessPieceType.King:
                    return Mathf.Max(adx, ady) <= 1;

                case ChessPieceType.Pawn:
                {
                    int forward = isEnemy ? -1 : 1;
                    return adx == 1 && dy == forward;
                }
            }

            return Mathf.Max(adx, ady) <= Mathf.Max(1, attackRange);
        }

        private static bool IsClearPath(Vector2Int origin, Vector2Int target, Func<Vector2Int, bool> isBlockedCell)
        {
            if (isBlockedCell == null)
                return true;

            int stepX = Math.Sign(target.x - origin.x);
            int stepY = Math.Sign(target.y - origin.y);
            Vector2Int cursor = origin + new Vector2Int(stepX, stepY);
            while (cursor != target)
            {
                if (isBlockedCell(cursor))
                    return false;
                cursor += new Vector2Int(stepX, stepY);
            }

            return true;
        }
    }
}
