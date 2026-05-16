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
        public const float SwampMoveCostMultiplier = 2f;
        public const float SpikeDamagePercentPerSecond = 0.03f;
        public const float SanctuaryHealPercentPerSecond = 0.02f;
        public const float SanctuaryDefenseBonus = 0.15f;

        // ─── Serialized Fields ────────────────────────────────────────────────────

        [Tooltip("World-space size of one grid tile (assumes square tiles).")]
        [SerializeField] private float _tileSize = 1f;

        [Tooltip("World-space position of the bottom-left corner of tile (0,0).")]
        [SerializeField] private Vector3 _originWorldPosition = Vector3.zero;

        [Tooltip("Seconds between tile effect ticks (damage/heal over time).")]
        [SerializeField] private float _tileEffectTickInterval = 1f;

        // ─── Singleton ────────────────────────────────────────────────────────────

        /// <summary>
        /// Static accessor. Populated automatically in Awake.
        /// </summary>
        public static GridSystem Instance { get; private set; }

        public float TileSize => _tileSize;

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

        private TileType[,] _tileMap;
        private float _tileEffectTimer;

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
            _tileMap = new TileType[GridWidth, GridHeight];
            InitialiseTileMap();
        }

        private void Update()
        {
            // Core simulation terrain ticks are executed through EffectTickScheduler.
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

        // ─── Tile Effects ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the AP cost multiplier for moving onto the specified cell.
        /// </summary>
        public float GetMoveCostMultiplier(Vector2Int cell)
        {
            if (!IsValidCell(cell)) return 1f;
            return GetTileType(cell) == TileType.Swamp ? SwampMoveCostMultiplier : 1f;
        }

        /// <summary>
        /// Returns an additive movement speed modifier applied when entering the specified cell.
        /// </summary>
        public float GetMoveSpeedModifier(Vector2Int cell)
        {
            if (!IsValidCell(cell)) return 0f;
            return 0f;
        }

        /// <summary>
        /// Applies entry-time tile effects (e.g. sanctuary defense) to the unit on the cell.
        /// Ongoing damage/heal ticks are handled by the grid tick loop.
        /// </summary>
        public void ApplyTileEffects(GameObject unit, Vector2Int cell)
        {
            // Terrain gameplay effects are resolved by the deterministic effect runtime.
        }

        private void UpdateSanctuaryDefense(GameObject unit, TileType tileType)
        {
            if (unit == null)
                return;

            if (!unit.TryGetComponent(out HealthComponent health))
                return;

            bool applySanctuary = tileType == TileType.Sanctuary && IsSanctuaryAlly(unit);
            health.SetDefenseBonus(applySanctuary ? SanctuaryDefenseBonus : 0f);
        }

        private void ApplySpikeDamage(GameObject unit)
        {
            if (unit == null)
                return;

            if (!unit.TryGetComponent(out HealthComponent health))
                return;

            if (health.IsDead)
                return;

            float damage = health.MaxHealth * SpikeDamagePercentPerSecond;
            if (damage > 0f)
                health.ApplyTrueDamage(damage);
        }

        private void ApplySanctuaryRegen(GameObject unit)
        {
            if (unit == null)
                return;

            if (!unit.TryGetComponent(out HealthComponent health) || health.IsDead)
                return;

            float heal = health.MaxHealth * SanctuaryHealPercentPerSecond;
            if (heal > 0f)
                health.Heal(heal);
        }

        private void ApplyTileEffectTick()
        {
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    GameObject occupant = _occupancy[x, y];
                    if (occupant == null)
                        continue;

                    TileType tileType = _tileMap[x, y];
                    UpdateSanctuaryDefense(occupant, tileType);
                    switch (tileType)
                    {
                        case TileType.Spikes:
                            ApplySpikeDamage(occupant);
                            break;
                        case TileType.Sanctuary:
                            if (IsSanctuaryAlly(occupant))
                                ApplySanctuaryRegen(occupant);
                            break;
                    }
                }
            }
        }

        public TileType GetTileType(Vector2Int cell)
        {
            if (!IsValidCell(cell))
                return TileType.Normal;

            return _tileMap[cell.x, cell.y];
        }

        private void SetTileType(Vector2Int cell, TileType type)
        {
            if (!IsValidCell(cell))
                return;

            _tileMap[cell.x, cell.y] = type;
        }

        private void InitialiseTileMap()
        {
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                    _tileMap[x, y] = TileType.Normal;
            }

            SetTileType(new Vector2Int(1, 0), TileType.Sanctuary);
            SetTileType(new Vector2Int(3, 0), TileType.Sanctuary);
            SetTileType(new Vector2Int(5, 0), TileType.Sanctuary);

            SetTileType(new Vector2Int(2, 3), TileType.Swamp);
            SetTileType(new Vector2Int(3, 3), TileType.Swamp);
            SetTileType(new Vector2Int(4, 3), TileType.Swamp);
            SetTileType(new Vector2Int(5, 3), TileType.Swamp);

            SetTileType(new Vector2Int(2, 4), TileType.Spikes);
            SetTileType(new Vector2Int(3, 4), TileType.Spikes);
            SetTileType(new Vector2Int(4, 4), TileType.Spikes);
            SetTileType(new Vector2Int(5, 4), TileType.Spikes);
        }

        private bool IsSanctuaryAlly(GameObject unit)
        {
            if (unit == null)
                return false;

            if (unit.TryGetComponent(out TeamComponent team))
                return team.IsPlayer;

            return false;
        }
    }
}
