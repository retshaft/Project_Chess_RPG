using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core.Replay
{
    public sealed class DeterministicVector2IntComparer : IComparer<Vector2Int>, IEqualityComparer<Vector2Int>
    {
        public static readonly DeterministicVector2IntComparer Instance = new();

        public int Compare(Vector2Int x, Vector2Int y)
        {
            int cx = x.x.CompareTo(y.x);
            return cx != 0 ? cx : x.y.CompareTo(y.y);
        }

        public bool Equals(Vector2Int x, Vector2Int y)
        {
            return x.x == y.x && x.y == y.y;
        }

        public int GetHashCode(Vector2Int obj)
        {
            return HashCode.Combine(obj.x, obj.y);
        }
    }
}
