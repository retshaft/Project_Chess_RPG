// MovementComponent.cs
// Handles grid-based movement with smooth world-space lerp interpolation.
// Implements IMovable so the UnitBrain and external systems share a stable contract.

using System;
using System.Collections;
using UnityEngine;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
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
        [SerializeField] private float _boundarySplatDamagePercent = 0.25f;

        private int   _moveRange;
        private float _moveSpeed;
        private float _moveAPCost;
        private float _actionSpeed = 1f;
        private StatusEffectComponent _statusEffects;
        private Coroutine _movementRoutine;

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

            _moveRange = data.MoveRange;
            _moveSpeed = data.MoveSpeed;
            _moveAPCost = Mathf.Max(0f, data.MoveCostAP);
            _actionSpeed = Mathf.Max(0.1f, data.ActionSpeed);
            Weight = Mathf.Clamp(data.Weight, 0, 4);
            IsBoss = data.IsBoss;

            GridPosition = startCell;

            // Register occupancy and snap transform
            GridSystem.Instance.SetOccupant(startCell, gameObject);
            transform.position = GridSystem.Instance.GridToWorld(startCell);

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

            if (_statusEffects != null && !_statusEffects.CanMove)
            {
                Debug.LogWarning($"[MovementComponent] {gameObject.name} cannot move due to status effects.");
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

            float apCost = GetMoveAPCost(targetGridPosition);
            if (!TrySpendAP(apCost))
                return;

            StartMovementCoroutine(MoveCoroutine(targetGridPosition));
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

            CurrentMoveCostMultiplier = GridSystem.Instance.GetMoveCostMultiplier(destination) * GetActionCostMultiplier();
            GridSystem.Instance.ApplyTileEffects(gameObject, destination);

            Vector3 startPos  = transform.position;
            Vector3 targetPos = GridSystem.Instance.GridToWorld(destination);
            float   elapsed   = 0f;
            float   actionSpeed = GetActionSpeedMultiplier();
            float   baseSpeed = _moveSpeed + GridSystem.Instance.GetMoveSpeedModifier(destination);
            float   speed     = Mathf.Max(0.1f, baseSpeed * actionSpeed);
            float   duration  = Vector3.Distance(startPos, targetPos) / speed;

            // Avoid NaN/Infinity if speed is extremely small or positions are identical.
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                transform.position = targetPos;
                IsMoving = false;
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

            transform.position = targetPos;
            IsMoving = false;

            OnMoveCompleted?.Invoke(destination);

            _statusEffects?.NotifyAction(Core.UnitActionType.Move);
        }

        public void ApplyKnockback(Vector2Int direction, int force, bool applySplatDamage = true)
        {
            if (GridSystem.Instance == null)
                return;

            if (force <= 0 || direction == Vector2Int.zero)
                return;

            direction = new Vector2Int(Mathf.Clamp(direction.x, -1, 1), Mathf.Clamp(direction.y, -1, 1));

            int effectiveWeight = Weight;
            if (_statusEffects != null && _statusEffects.HasStatus(Core.StatusEffectType.Stagger) && !IsBoss)
                effectiveWeight = Mathf.Max(0, effectiveWeight - 1);

            int distance = Mathf.Max(0, force - effectiveWeight);
            if (distance <= 0)
                return;

            Vector2Int origin = GridPosition;
            Vector2Int finalCell = origin;
            GameObject collisionTarget = null;
            bool hitBoundary = false;

            for (int step = 1; step <= distance; step++)
            {
                Vector2Int next = origin + direction * step;

                if (!GridSystem.Instance.IsValidCell(next))
                {
                    hitBoundary = true;
                    break;
                }

                if (!GridSystem.Instance.IsCellFree(next))
                {
                    collisionTarget = GridSystem.Instance.GetOccupant(next);
                    break;
                }

                finalCell = next;
            }

            if (applySplatDamage)
            {
                if (collisionTarget != null)
                {
                    ApplySplatDamage(gameObject, _splatDamagePercent);
                    ApplySplatDamage(collisionTarget, _splatDamagePercent);
                }
                else if (hitBoundary)
                {
                    ApplySplatDamage(gameObject, _boundarySplatDamagePercent);
                }
            }

            if (finalCell != origin)
                StartMovementCoroutine(ForcedMoveCoroutine(finalCell, _knockbackSpeed));
        }

        public void ApplyGrab(Vector2Int sourceCell, int force)
        {
            Vector2Int delta = sourceCell - GridPosition;
            Vector2Int direction;

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), 0);
            else
                direction = new Vector2Int(0, Mathf.Clamp(delta.y, -1, 1));

            if (direction == Vector2Int.zero)
                return;

            ApplyKnockback(direction, force);

            if (_statusEffects != null && _statusEffects.HasStatus(Core.StatusEffectType.Stagger))
                _statusEffects.ApplyGrabVulnerability();
        }

        private IEnumerator ForcedMoveCoroutine(Vector2Int destination, float speed)
        {
            IsMoving = true;
            OnMoveStarted?.Invoke(destination);

            GridSystem.Instance.ClearCell(GridPosition);
            GridSystem.Instance.SetOccupant(destination, gameObject);
            GridPosition = destination;

            CurrentMoveCostMultiplier = GridSystem.Instance.GetMoveCostMultiplier(destination) * GetActionCostMultiplier();
            GridSystem.Instance.ApplyTileEffects(gameObject, destination);

            Vector3 startPos = transform.position;
            Vector3 targetPos = GridSystem.Instance.GridToWorld(destination);
            float elapsed = 0f;
            float duration = Vector3.Distance(startPos, targetPos) / Mathf.Max(0.1f, speed);

            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                transform.position = targetPos;
                IsMoving = false;
                OnMoveCompleted?.Invoke(destination);
                yield break;
            }

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

        private void StartMovementCoroutine(IEnumerator routine)
        {
            if (_movementRoutine != null)
            {
                StopCoroutine(_movementRoutine);
                IsMoving = false;
            }

            _movementRoutine = StartCoroutine(routine);
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
            if (cost <= 0f)
                return true;

            if (Core.APManager.Instance == null)
            {
                Debug.LogWarning("[MovementComponent] APManager not found. Move cancelled.");
                return false;
            }

            if (!Core.APManager.Instance.TrySpend(new Core.ActionPointCost(cost, Core.APActionReason.Move), out _))
                return false;

            return true;
        }

        private static void ApplySplatDamage(GameObject target, float percent)
        {
            if (target == null)
                return;

            if (!target.TryGetComponent(out HealthComponent health) || health.IsDead)
                return;

            float damage = health.MaxHealth * Mathf.Max(0f, percent);
            if (damage > 0f)
                health.ApplyTrueDamage(damage);
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
