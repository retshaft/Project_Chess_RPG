// MovementComponent.cs
// Handles grid-based movement with smooth world-space lerp interpolation.
// Implements IMovable so the UnitBrain and external systems share a stable contract.

using System;
using System.Collections;
using UnityEngine;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;

namespace CheckmateRPG.Components
{
    /// <summary>
    /// Moves a unit from one grid cell to another using a coroutine-driven lerp.
    /// </summary>
    public class MovementComponent : MonoBehaviour, IMovable
    {
        // ─── Events ───────────────────────────────────────────────────────────────

        /// <summary>Raised when the unit begins moving. Parameter: destination cell.</summary>
        public event Action<Vector2Int> OnMoveStarted;

        /// <summary>Raised when the unit has fully arrived at its destination.</summary>
        public event Action<Vector2Int> OnMoveCompleted;

        // ─── IMovable ─────────────────────────────────────────────────────────────

        public Vector2Int GridPosition { get; private set; }
        public bool       IsMoving     { get; private set; }

        // ─── Private State ────────────────────────────────────────────────────────

        private int   _moveRange;
        private float _moveSpeed;

        // ─── Initialisation ───────────────────────────────────────────────────────

        /// <summary>
        /// Initialise from a UnitData asset and snap the unit to its starting cell.
        /// </summary>
        public void Initialise(UnitData data, Vector2Int startCell)
        {
            _moveRange = data.MoveRange;
            _moveSpeed = data.MoveSpeed;

            GridPosition = startCell;

            // Register occupancy and snap transform
            GridSystem.Instance.SetOccupant(startCell, gameObject);
            transform.position = GridSystem.Instance.GridToWorld(startCell);
        }

        // ─── IMovable Implementation ──────────────────────────────────────────────

        /// <summary>
        /// Move to <paramref name="targetGridPosition"/> if the cell is reachable and free.
        /// </summary>
        public void MoveTo(Vector2Int targetGridPosition)
        {
            if (IsMoving)
            {
                Debug.LogWarning($"[MovementComponent] {gameObject.name} is already moving.");
                return;
            }

            if (!GridSystem.Instance.IsValidCell(targetGridPosition))
            {
                Debug.LogWarning($"[MovementComponent] Target cell {targetGridPosition} is out of bounds.");
                return;
            }

            if (!GridSystem.Instance.IsCellFree(targetGridPosition))
            {
                Debug.LogWarning($"[MovementComponent] Target cell {targetGridPosition} is occupied.");
                return;
            }

            int distance = ChebyshevDistance(GridPosition, targetGridPosition);
            if (distance > _moveRange)
            {
                Debug.LogWarning($"[MovementComponent] Target cell is {distance} steps away, move range is {_moveRange}.");
                return;
            }

            StartCoroutine(MoveCoroutine(targetGridPosition));
        }

        // ─── Coroutine ────────────────────────────────────────────────────────────

        private IEnumerator MoveCoroutine(Vector2Int destination)
        {
            IsMoving = true;
            OnMoveStarted?.Invoke(destination);

            // Update occupancy immediately to prevent double-booking
            GridSystem.Instance.ClearCell(GridPosition);
            GridSystem.Instance.SetOccupant(destination, gameObject);
            GridPosition = destination;

            Vector3 startPos  = transform.position;
            Vector3 targetPos = GridSystem.Instance.GridToWorld(destination);
            float   elapsed   = 0f;
            float   duration  = Vector3.Distance(startPos, targetPos) / _moveSpeed;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
                yield return null;
            }

            transform.position = targetPos;
            IsMoving = false;

            OnMoveCompleted?.Invoke(destination);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Chebyshev (chess-king) distance: max(|Δcol|, |Δrow|).
        /// </summary>
        private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }
    }
}
