// GridSystem.cs
// Manages the 8x8 battlefield grid.
// Provides world↔grid coordinate conversion and tile occupancy tracking.
// Place a single instance in the scene (singleton pattern via static accessor).

using UnityEngine;
using CheckmateRPG.Components;

namespace CheckmateRPG.Grid
{
    /// <summary>
    /// Central authority for the chess-style 8x8 battlefield grid.
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        // ─── Constants ────────────────────────────────────────────────────────────

        public const int GridWidth  = 8;
        public const int GridHeight = 8;

        // ─── Serialized Fields ────────────────────────────────────────────────────

        [Tooltip("World-space size of one grid tile (assumes square tiles).")]
        [SerializeField] private float _tileSize = 1f;

        [Tooltip("World-space position of the bottom-left corner of tile (0,0).")]
        [SerializeField] private Vector3 _originWorldPosition = Vector3.zero;

        // ─── Singleton ────────────────────────────────────────────────────────────

        /// <summary>
        /// Static accessor. Populated automatically in Awake.
        /// </summary>
        public static GridSystem Instance { get; private set; }

        // ─── Internal State ───────────────────────────────────────────────────────

        /// <summary>
        /// Tracks which GameObject occupies each cell.
        /// Null means the cell is free.
        /// </summary>
        private GameObject[,] _occupancy;

        /// <summary>
        /// Reverse map from unit to its current cell for O(1) lookups in ClearOccupant.
        /// </summary>
        private System.Collections.Generic.Dictionary<GameObject, Vector2Int> _unitToCell;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GridSystem] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _occupancy  = new GameObject[GridWidth, GridHeight];
            _unitToCell = new System.Collections.Generic.Dictionary<GameObject, Vector2Int>();
        }

        // ─── Coordinate Conversion ────────────────────────────────────────────────

        /// <summary>
        /// Convert a grid cell (column, row) to the world-space centre of that tile.
        /// </summary>
        public Vector3 GridToWorld(int column, int row)
        {
            return new Vector3(
                _originWorldPosition.x + (column + 0.5f) * _tileSize,
                _originWorldPosition.y,
                _originWorldPosition.z + (row    + 0.5f) * _tileSize
            );
        }

        /// <inheritdoc cref="GridToWorld(int,int)"/>
        public Vector3 GridToWorld(Vector2Int cell) => GridToWorld(cell.x, cell.y);

        /// <summary>
        /// Convert a world-space position to the nearest grid cell.
        /// Returns (-1,-1) if the position is outside the grid boundaries.
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            int column = Mathf.FloorToInt((worldPosition.x - _originWorldPosition.x) / _tileSize);
            int row    = Mathf.FloorToInt((worldPosition.z - _originWorldPosition.z) / _tileSize);

            if (!IsValidCell(column, row))
                return new Vector2Int(-1, -1);

            return new Vector2Int(column, row);
        }

        // ─── Validation ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when (column, row) is inside the 8x8 bounds.
        /// </summary>
        public bool IsValidCell(int column, int row)
        {
            return column >= 0 && column < GridWidth &&
                   row    >= 0 && row    < GridHeight;
        }

        /// <inheritdoc cref="IsValidCell(int,int)"/>
        public bool IsValidCell(Vector2Int cell) => IsValidCell(cell.x, cell.y);

        // ─── Occupancy ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when the cell is within bounds and no unit occupies it.
        /// </summary>
        public bool IsCellFree(int column, int row)
        {
            if (!IsValidCell(column, row)) return false;
            return _occupancy[column, row] == null;
        }

        /// <inheritdoc cref="IsCellFree(int,int)"/>
        public bool IsCellFree(Vector2Int cell) => IsCellFree(cell.x, cell.y);

        /// <summary>
        /// Returns the occupant of the cell, or null if the cell is empty / out of bounds.
        /// </summary>
        public GameObject GetOccupant(int column, int row)
        {
            if (!IsValidCell(column, row)) return null;
            return _occupancy[column, row];
        }

        /// <inheritdoc cref="GetOccupant(int,int)"/>
        public GameObject GetOccupant(Vector2Int cell) => GetOccupant(cell.x, cell.y);

        /// <summary>
        /// Mark a cell as occupied by the given unit.
        /// Clears any previous record of that unit's old cell automatically.
        /// </summary>
        public void SetOccupant(Vector2Int cell, GameObject unit)
        {
            // Remove stale occupancy entry for this unit
            ClearOccupant(unit);

            if (!IsValidCell(cell)) return;
            _occupancy[cell.x, cell.y] = unit;
            _unitToCell[unit] = cell;
        }

        /// <summary>
        /// Remove a unit from whichever cell it currently occupies.
        /// Safe to call with a null reference.
        /// </summary>
        public void ClearOccupant(GameObject unit)
        {
            if (unit == null) return;

            if (_unitToCell.TryGetValue(unit, out Vector2Int cell))
            {
                if (IsValidCell(cell) && _occupancy[cell.x, cell.y] == unit)
                    _occupancy[cell.x, cell.y] = null;

                _unitToCell.Remove(unit);
            }
        }

        /// <summary>
        /// Remove whatever occupies the given cell.
        /// </summary>
        public void ClearCell(Vector2Int cell)
        {
            if (!IsValidCell(cell)) return;

            GameObject occupant = _occupancy[cell.x, cell.y];
            if (occupant != null)
                _unitToCell.Remove(occupant);

            _occupancy[cell.x, cell.y] = null;
        }

        private void ApplySpikeDamage(GameObject unit)
        {
            if (unit == null)
                return;

            if (!unit.TryGetComponent(out HealthComponent health))
                return;

            float damage = health.CurrentHealth * 0.05f;
            if (damage > 0f)
                health.ApplyTrueDamage(damage);
        }
    }
}
