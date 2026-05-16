using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Abilities;
using CheckmateRPG.Core.Determinism;
using UnityEngine;

namespace CheckmateRPG.Core.Actions.Resolvers
{
    public static class TargetingShapeResolver
    {
        private readonly struct SortEntry
        {
            public SortEntry(Vector2Int cell)
            {
                Cell = cell;
                StableId = BuildStableId(cell);
            }

            public Vector2Int Cell { get; }
            public Guid StableId { get; }
        }

        public static IReadOnlyList<Vector2Int> Resolve(
            TargetingShape shape,
            Vector2Int sourcePos,
            Vector2Int targetPos,
            int range,
            int boardSize = 8)
        {
            int normalizedBoardSize = Mathf.Max(1, boardSize);
            int normalizedRange = Mathf.Max(0, range);

            if (shape != TargetingShape.Self &&
                GetPlanarDistance(sourcePos, targetPos) > normalizedRange)
            {
                return Array.Empty<Vector2Int>();
            }

            var rawCells = new List<Vector2Int>();
            switch (shape)
            {
                case TargetingShape.Self:
                    TryAdd(rawCells, sourcePos, normalizedBoardSize);
                    break;

                case TargetingShape.Single:
                    TryAdd(rawCells, targetPos, normalizedBoardSize);
                    break;

                case TargetingShape.Cross:
                    TryAdd(rawCells, targetPos, normalizedBoardSize);
                    TryAdd(rawCells, targetPos + Vector2Int.up, normalizedBoardSize);
                    TryAdd(rawCells, targetPos + Vector2Int.down, normalizedBoardSize);
                    TryAdd(rawCells, targetPos + Vector2Int.left, normalizedBoardSize);
                    TryAdd(rawCells, targetPos + Vector2Int.right, normalizedBoardSize);
                    break;

                case TargetingShape.Grid3x3:
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                            TryAdd(rawCells, new Vector2Int(targetPos.x + x, targetPos.y + y), normalizedBoardSize);
                    }
                    break;
            }

            return StableSort(rawCells);
        }

        private static IReadOnlyList<Vector2Int> StableSort(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
                return Array.Empty<Vector2Int>();

            var entries = new List<SortEntry>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
                entries.Add(new SortEntry(cells[i]));

            var sortPolicy = new StableSortPolicy<SortEntry>(
                getTick: entry => entry.Cell.x,
                getSpeedTier: _ => ActionSpeedTier.Normal,
                getResolveOrder: entry => entry.Cell.y,
                getStableId: entry => entry.StableId);
            entries.Sort(sortPolicy);

            var sorted = new List<Vector2Int>(entries.Count);
            var seen = new HashSet<Vector2Int>();
            for (int i = 0; i < entries.Count; i++)
            {
                Vector2Int cell = entries[i].Cell;
                if (seen.Add(cell))
                    sorted.Add(cell);
            }

            return sorted;
        }

        private static int GetPlanarDistance(Vector2Int from, Vector2Int to)
        {
            return Mathf.Max(Mathf.Abs(from.x - to.x), Mathf.Abs(from.y - to.y));
        }

        private static void TryAdd(ICollection<Vector2Int> cells, Vector2Int cell, int boardSize)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= boardSize || cell.y >= boardSize)
                return;
            cells.Add(cell);
        }

        private static Guid BuildStableId(Vector2Int cell)
        {
            byte[] bytes = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(cell.x), 0, bytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(cell.y), 0, bytes, 4, 4);
            int mixA = unchecked((cell.x * 73856093) ^ (cell.y * 19349663));
            int mixB = unchecked((cell.x * 83492791) ^ (cell.y * (int)2971215073u));
            Buffer.BlockCopy(BitConverter.GetBytes(mixA), 0, bytes, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(mixB), 0, bytes, 12, 4);
            return new Guid(bytes);
        }
    }
}
