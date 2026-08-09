// MovementComponent.cs
// Handles grid-based movement with smooth world-space lerp interpolation.
// Implements IMovable so the UnitBrain and external systems share a stable contract.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
using CheckmateRPG.MovementPatterns;
using CheckmateRPG.Units;
using Core = CheckmateRPG.Core;

namespace CheckmateRPG.Components
{
    /// <summary>
    /// Moves a unit from one grid cell to another using a coroutine-driven lerp.
    /// </summary>
    public class MovementComponent : MonoBehaviour, Core.IMovable
    {
        // ─── Events ───────────────────────────────────────────────────────────────

        /// <summary>Raised when the unit begins moving. Parameter: destination cell.</summary>
        public event Action<Vector2Int> OnMoveStarted;

        /// <summary>Raised when the unit has fully arrived at its destination.</summary>
        public event Action<Vector2Int> OnMoveCompleted;

        // ─── IMovable ─────────────────────────────────────────────────────────────

        public Vector2Int GridPosition { get; private set; }
        public bool       IsMoving     { get; private set; }
        public float      CurrentMoveCostMultiplier { get; private set; } = 1f;
        public int        Weight { get; private set; }
        public bool       IsBoss { get; private set; }

        // ─── Private State ────────────────────────────────────────────────────────

        [SerializeField] private float _knockbackSpeed = 8f;
        [SerializeField] private float _splatDamagePercent = 0.1f;
        [SerializeField] private float _boundarySplatDamagePercent = 0.1f;
        [SerializeField] private float _yOffset = 0.75f;

        private int   _moveRange;
        private float _moveSpeed;
        private float _moveAPCost;
        private float _actionSpeed = 1f;
        private StatusEffectComponent _statusEffects;
        private Coroutine _movementRoutine;
        private UnitData _unitData;
        private IMovePattern _movePattern;

        private void Awake()
        {
            _statusEffects = GetComponent<StatusEffectComponent>();
        }

        // ─── Initialisation ───────────────────────────────────────────────────────

        /// <summary>
        /// Initialise from a UnitData asset and snap the unit to its starting cell.
        /// </summary>
        public void Initialise(UnitData data, Vector2Int startCell)
        {
            if (data == null)
            {
                Debug.LogError($"[MovementComponent] Initialise called with null UnitData on {gameObject.name}.");
                enabled = false;
                return;
            }

            if (GridSystem.Instance == null)
            {
                Debug.LogError($"[MovementComponent] GridSystem.Instance is null during Initialise on {gameObject.name}.");
                enabled = false;
                return;
            }

            _unitData = data;
            _movePattern = MovePatternFactory.Create(data);
            _moveRange = data.MoveRange;
            _moveSpeed = data.MoveSpeed;
            _moveAPCost = Mathf.Max(0f, data.MoveCostAP);
            _actionSpeed = Mathf.Max(0.1f, data.ActionSpeed);
            Weight = Mathf.Clamp(data.Weight, 0, 4);
            IsBoss = data.IsBoss;

            GridPosition = startCell;

            // Register occupancy and snap transform
            GridSystem.Instance.SetOccupant(startCell, gameObject);
            transform.position = GridSystem.Instance.GridToWorld(startCell) + new Vector3(0f, _yOffset, 0f);

            CurrentMoveCostMultiplier = GridSystem.Instance.GetMoveCostMultiplier(startCell) * GetActionCostMultiplier();
            GridSystem.Instance.ApplyTileEffects(gameObject, startCell);
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

            if (GridSystem.Instance == null)
            {
                Debug.LogWarning($"[MovementComponent] GridSystem.Instance is null. Move cancelled for {gameObject.name}.");
                return;
            }

            if (!CanReachCell(targetGridPosition, logFailures: true))
                return;

            float apCost = GetMoveAPCost(targetGridPosition);
            if (!TrySpendAP(apCost))
                return;

            StartMovementCoroutine(MoveCoroutine(targetGridPosition));
        }

        public bool ApplyResolvedMovement(Vector2Int destination)
        {
            if (GridSystem.Instance == null)
                return false;
            if (!GridSystem.Instance.IsValidCell(destination))
                return false;
            if (destination != GridPosition && !GridSystem.Instance.IsCellFree(destination))
                return false;

            StartMovementCoroutine(MoveCoroutine(destination));
            return true;
        }

        // ─── Coroutine ────────────────────────────────────────────────────────────

        private IEnumerator MoveCoroutine(Vector2Int destination)
        {
            IsMoving = true;
            OnMoveStarted?.Invoke(destination);

            Vector2Int startGridPos = GridPosition;

            if (TryGetComponent(out UnitBrain brain) && Core.ActionRuntimeController.Instance != null)
            {
                var payload = new Core.MoveStartedPayload(brain.ActorId, startGridPos.x, startGridPos.y, destination.x, destination.y);
                Core.ActionRuntimeController.Instance.EventBus.Publish(new Core.MoveStartedEvent(payload, brain.ActorId.ToString("N")));
            }

            // Update occupancy immediately to prevent double-booking
            GridSystem.Instance.ClearCell(GridPosition);
            GridSystem.Instance.SetOccupant(destination, gameObject);
            GridPosition = destination;

            CurrentMoveCostMultiplier = GridSystem.Instance.GetMoveCostMultiplier(destination) * GetActionCostMultiplier();
            GridSystem.Instance.ApplyTileEffects(gameObject, destination);

            Vector3 startPos  = transform.position;
            Vector3 targetPos = GridSystem.Instance.GridToWorld(destination) + new Vector3(0f, _yOffset, 0f);
            float   elapsed   = 0f;
            
            float   baseSpeed = _moveSpeed + GridSystem.Instance.GetMoveSpeedModifier(destination);
            float   speedInCellsPerSecond = Mathf.Max(0.1f, baseSpeed);
            float   worldSpeed = speedInCellsPerSecond * GridSystem.Instance.TileSize;
            float   duration  = Vector3.Distance(startPos, targetPos) / worldSpeed;

            // Avoid NaN/Infinity if speed is extremely small or positions are identical.
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                transform.position = targetPos;
                IsMoving = false;
                TryHandlePawnPromotion(destination);
                OnMoveCompleted?.Invoke(destination);
                _statusEffects?.NotifyAction(Core.UnitActionType.Move);
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
                yield return null;
            }

            IsMoving = false;

            TryHandlePawnPromotion(destination);
            OnMoveCompleted?.Invoke(destination);

            if (TryGetComponent(out UnitBrain finalBrain) && Core.ActionRuntimeController.Instance != null)
            {
                var payload = new Core.Events.ActionEvents.MoveCompletedPayload(finalBrain.ActorId, startGridPos, destination);
                Core.ActionRuntimeController.Instance.EventBus.Publish(new Core.Events.ActionEvents.MoveCompletedEvent(payload, finalBrain.ActorId.ToString("N")));
            }

            _statusEffects?.NotifyAction(Core.UnitActionType.Move);
        }



        public bool TestCanMove(Vector2Int fromCell, Vector2Int targetCell)
        {
            if (GridSystem.Instance == null)
                return false;
            
            if (fromCell == targetCell)
                return false;

            if (_movePattern == null)
                return false;

            return _movePattern.CanMove(fromCell, targetCell, gameObject);
        }

        public bool CanReachCell(Vector2Int targetGridPosition, bool logFailures = false)
        {
            if (GridSystem.Instance == null)
            {
                if (logFailures)
                    Debug.LogWarning($"[MovementComponent] GridSystem.Instance is null. Move cancelled for {gameObject.name}.");
                return false;
            }

            if (targetGridPosition == GridPosition)
                return false;

            if (_statusEffects != null && !_statusEffects.CanMove)
            {
                if (logFailures)
                    Debug.LogWarning($"[MovementComponent] {gameObject.name} cannot move due to status effects.");
                return false;
            }

            if (!GridSystem.Instance.IsValidCell(targetGridPosition))
            {
                if (logFailures)
                    Debug.LogWarning($"[MovementComponent] Target cell {targetGridPosition} is out of bounds.");
                return false;
            }

            if (!GridSystem.Instance.IsCellFree(targetGridPosition))
            {
                if (logFailures)
                    Debug.LogWarning($"[MovementComponent] Target cell {targetGridPosition} is occupied.");
                return false;
            }

            if (_movePattern != null)
            {
                bool canMove = _movePattern.CanMove(GridPosition, targetGridPosition, gameObject);
                if (!canMove && logFailures)
                {
                    Debug.LogWarning($"[MovementComponent] Move pattern blocked move from {GridPosition} to {targetGridPosition}.");
                }

                return canMove;
            }

            int distance = ChebyshevDistance(GridPosition, targetGridPosition);
            bool inRange = distance <= _moveRange;
            if (!inRange && logFailures)
            {
                Debug.LogWarning($"[MovementComponent] Target cell is {distance} steps away, move range is {_moveRange}.");
            }

            return inRange;
        }

        public List<Vector2Int> GetReachableCells()
        {
            var reachableCells = new List<Vector2Int>();
            if (GridSystem.Instance == null)
                return reachableCells;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    if (CanReachCell(candidate))
                        reachableCells.Add(candidate);
                }
            }

            return reachableCells;
        }



        private void StartMovementCoroutine(IEnumerator routine)
        {
            if (_movementRoutine != null)
            {
                StopCoroutine(_movementRoutine);
                IsMoving = false;
            }

            _movementRoutine = StartCoroutine(routine);
        }

        private void TryHandlePawnPromotion(Vector2Int destination)
        {
            if (_unitData == null || _unitData.PieceType != ChessPieceType.Pawn)
                return;

            bool isEnemy = TryGetComponent(out TeamComponent team) && team.IsEnemy;
            int promotionRow = isEnemy ? 0 : GridSystem.GridHeight - 1;
            bool reachedPromotionRank = destination.y == promotionRow;

            if (!reachedPromotionRank)
                return;

            if (TryGetComponent(out UnitBrain brain))
            {
                brain.StartPromotion();
            }
        }

        private float GetActionCostMultiplier()
        {
            return _statusEffects != null ? _statusEffects.ActionCostMultiplier : 1f;
        }

        private float GetActionSpeedMultiplier()
        {
            float statusMultiplier = _statusEffects != null ? _statusEffects.ActionSpeedMultiplier : 1f;
            return _actionSpeed * statusMultiplier;
        }

        private float GetMoveAPCost(Vector2Int targetCell)
        {
            float multiplier = 1f;
            if (GridSystem.Instance != null)
                multiplier *= GridSystem.Instance.GetMoveCostMultiplier(targetCell);
            multiplier *= GetActionCostMultiplier();
            return Mathf.Max(0f, _moveAPCost * multiplier);
        }

        private bool TrySpendAP(float cost)
        {
            // AP is already deducted by ActionCostReservation when the scheduler commits the action.
            // AITeamCommander also manages its own AP before scheduling.
            return true;
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
