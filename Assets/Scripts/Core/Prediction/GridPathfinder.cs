using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;

namespace CheckmateRPG.Core.Prediction
{
    public static class GridPathfinder
    {
        public static List<Vector2Int> FindPath(UnitBrain unit, Vector2Int startCell, Vector2Int targetCell)
        {
            if (unit == null || unit.Movement == null)
                return null;

            if (startCell == targetCell)
                return new List<Vector2Int> { startCell };

            int width = GridSystem.GridWidth;
            int height = GridSystem.GridHeight;
            
            bool[,] visited = new bool[width, height];
            Vector2Int[,] cameFrom = new Vector2Int[width, height];
            
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            
            queue.Enqueue(startCell);
            visited[startCell.x, startCell.y] = true;
            
            bool found = false;

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                
                if (current == targetCell)
                {
                    found = true;
                    break;
                }

                // Optimization: Instead of evaluating all cells, just evaluate reachable cells from 'current'.
                // Since CanMove might depend on the unit's transform (which we can't safely mutate here without breaking state),
                // we'll rely on the pure MovePattern.
                // Wait! We can use MovementComponent's internally cached pattern or just evaluate all cells.
                
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        Vector2Int neighbor = new Vector2Int(x, y);
                        
                        if (visited[x, y])
                            continue;

                        // Check if the move is legal from 'current' to 'neighbor'
                        if (unit.Movement.TestCanMove(current, neighbor))
                        {
                            visited[x, y] = true;
                            cameFrom[x, y] = current;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            if (!found)
                return null; // No path found

            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int curr = targetCell;
            while (curr != startCell)
            {
                path.Add(curr);
                curr = cameFrom[curr.x, curr.y];
            }
            // We don't add the startCell to the path since we are already there.
            path.Reverse();
            return path;
        }
    }
}
