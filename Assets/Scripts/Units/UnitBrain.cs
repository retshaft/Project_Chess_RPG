// UnitBrain.cs
// The controller layer for a single unit.
// Wires together HealthComponent, MovementComponent, and CombatComponent,
// and exposes a simple command API (Move, Attack) consumed by player input
// or an AI decision system.

using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;

namespace CheckmateRPG.Units
{
    /// <summary>
    /// Orchestrates all components attached to a unit.
    /// Acts as the single entry point for issuing orders to the unit.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(MovementComponent))]
    [RequireComponent(typeof(CombatComponent))]
    public class UnitBrain : MonoBehaviour
    {
        // ─── Serialized Fields ────────────────────────────────────────────────────

        [Tooltip("Data asset that defines this unit's stats. Must be assigned before play.")]
        [SerializeField] private UnitData _unitData;

        [Tooltip("Grid cell where this unit spawns. (column = x, row = y)")]
        [SerializeField] private Vector2Int _startCell = Vector2Int.zero;

        [Tooltip("Optional target for the decision loop to pursue.")]
        [SerializeField] private GameObject _currentTarget;

        // ─── Component References ─────────────────────────────────────────────────

        /// <summary>Read-only access to this unit's health state.</summary>
        public HealthComponent   Health   { get; private set; }

        /// <summary>Read-only access to this unit's movement state.</summary>
        public MovementComponent Movement { get; private set; }

        /// <summary>Read-only access to this unit's combat state.</summary>
        public CombatComponent   Combat   { get; private set; }

        // ─── State ────────────────────────────────────────────────────────────────

        /// <summary>True once the unit has been killed.</summary>
        public bool IsDead => Health != null && Health.IsDead;

        /// <summary>Current decision made by the unit's brain.</summary>
        public UnitDecision CurrentDecision { get; private set; } = UnitDecision.Idle;

        private bool _isInitialised;

        public enum UnitDecision
        {
            Idle,
            Move,
            Attack
        }

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            Health   = GetComponent<HealthComponent>();
            Movement = GetComponent<MovementComponent>();
            Combat   = GetComponent<CombatComponent>();
        }

        private void Start()
        {
            if (_unitData == null)
            {
                Debug.LogError($"[UnitBrain] {gameObject.name} has no UnitData assigned!");
                return;
            }

            // Initialise each component with data from the ScriptableObject
            Health.Initialise(_unitData);
            Movement.Initialise(_unitData, _startCell);
            Combat.Initialise(_unitData);

            // Wire death notification to combat so attacks stop after death
            Health.OnDeath += HandleDeath;

            _isInitialised = true;
            Debug.Log($"[UnitBrain] {_unitData.UnitName} initialised at cell {_startCell}.");
        }

        private void OnDestroy()
        {
            if (Health != null)
                Health.OnDeath -= HandleDeath;
        }

        private void FixedUpdate()
        {
            if (!_isInitialised || IsDead)
                return;

            EvaluateDecision();
        }

        // ─── Public Command API ───────────────────────────────────────────────────

        /// <summary>
        /// Order the unit to move to <paramref name="targetCell"/>.
        /// The MovementComponent validates range and occupancy.
        /// </summary>
        public void Move(Vector2Int targetCell)
        {
            if (IsDead)
            {
                Debug.LogWarning($"[UnitBrain] {gameObject.name} is dead and cannot move.");
                return;
            }

            Movement.MoveTo(targetCell);
        }

        /// <summary>
        /// Order the unit to attack <paramref name="target"/>.
        /// The CombatComponent validates cooldown and range.
        /// </summary>
        public void Attack(GameObject target)
        {
            if (IsDead)
            {
                Debug.LogWarning($"[UnitBrain] {gameObject.name} is dead and cannot attack.");
                return;
            }

            Combat.Attack(target);
        }

        /// <summary>
        /// Assign a target for the decision loop.
        /// </summary>
        public void SetTarget(GameObject target)
        {
            _currentTarget = target;
        }

        /// <summary>
        /// Clears the current decision loop target.
        /// </summary>
        public void ClearTarget()
        {
            _currentTarget = null;
        }

        // ─── Event Handlers ───────────────────────────────────────────────────────

        private void HandleDeath()
        {
            Debug.Log($"[UnitBrain] {gameObject.name} has died.");
            Combat.OnOwnerDied();

            // Future: trigger death animation, notify game manager, drop loot, etc.
        }

        // ─── Decision Logic ───────────────────────────────────────────────────────

        private void EvaluateDecision()
        {
            if (Movement == null || Combat == null)
            {
                CurrentDecision = UnitDecision.Idle;
                return;
            }

            if (Movement.IsMoving)
            {
                CurrentDecision = UnitDecision.Idle;
                return;
            }

            if (!IsValidTarget(_currentTarget))
            {
                CurrentDecision = UnitDecision.Idle;
                return;
            }

            bool inRange = IsTargetInAttackRange(_currentTarget);
            if (inRange && Combat.CanAttack)
            {
                CurrentDecision = UnitDecision.Attack;
                Attack(_currentTarget);
                return;
            }

            if (!inRange && TryGetMoveDestination(_currentTarget, out Vector2Int destination))
            {
                CurrentDecision = UnitDecision.Move;
                Move(destination);
                return;
            }

            CurrentDecision = UnitDecision.Idle;
        }

        private bool IsValidTarget(GameObject target)
        {
            if (target == null || target == gameObject)
                return false;

            if (target.TryGetComponent(out HealthComponent health) && health.IsDead)
                return false;

            return target.GetComponent<IDamageable>() != null;
        }

        private bool IsTargetInAttackRange(GameObject target)
        {
            if (target == null || _unitData == null)
                return false;

            MovementComponent targetMovement = target.GetComponent<MovementComponent>();
            if (Movement != null && targetMovement != null)
            {
                int distance = ChebyshevDistance(Movement.GridPosition, targetMovement.GridPosition);
                return distance <= _unitData.AttackRange;
            }

            float worldRange = _unitData.AttackRange * 1.5f;
            return Vector3.Distance(transform.position, target.transform.position) <= worldRange;
        }

        private bool TryGetMoveDestination(GameObject target, out Vector2Int destination)
        {
            destination = default;

            if (_unitData == null || Movement == null || GridSystem.Instance == null)
                return false;

            if (!TryGetTargetCell(target, out Vector2Int targetCell))
                return false;

            Vector2Int currentCell = Movement.GridPosition;
            Vector2Int delta = targetCell - currentCell;
            if (delta == Vector2Int.zero)
                return false;

            int moveRange = Mathf.Max(1, _unitData.MoveRange);
            Vector2Int direction = new Vector2Int(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
            Vector2Int clampedDelta = new Vector2Int(Mathf.Clamp(delta.x, -moveRange, moveRange),
                                                     Mathf.Clamp(delta.y, -moveRange, moveRange));

            Vector2Int candidate = currentCell + clampedDelta;
            while (candidate != currentCell)
            {
                if (GridSystem.Instance.IsValidCell(candidate) && GridSystem.Instance.IsCellFree(candidate))
                {
                    destination = candidate;
                    return true;
                }

                candidate -= direction;
            }

            return false;
        }

        private bool TryGetTargetCell(GameObject target, out Vector2Int targetCell)
        {
            targetCell = default;

            if (target == null)
                return false;

            if (target.TryGetComponent(out MovementComponent targetMovement))
            {
                targetCell = targetMovement.GridPosition;
                return true;
            }

            if (GridSystem.Instance == null)
                return false;

            targetCell = GridSystem.Instance.WorldToGrid(target.transform.position);
            return targetCell.x >= 0 && targetCell.y >= 0;
        }

        private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }
    }
}
