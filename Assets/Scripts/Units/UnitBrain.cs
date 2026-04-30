// UnitBrain.cs
// The controller layer for a single unit.
// Wires together HealthComponent, MovementComponent, and CombatComponent,
// and exposes a simple command API (Move, Attack) consumed by player input
// or an AI decision system.

using System.Collections.Generic;
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

        /// <summary>Read-only access to the assigned unit data.</summary>
        public UnitData UnitData => _unitData;

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

        private struct DecisionCandidate
        {
            public UnitDecision Decision;
            public GameObject Target;
            public Vector2Int Destination;
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
            if (Movement == null || Combat == null || _unitData == null)
            {
                CurrentDecision = UnitDecision.Idle;
                return;
            }

            if (Movement.IsMoving)
            {
                CurrentDecision = UnitDecision.Move;
                return;
            }

            if (!TryAcquireTarget(out GameObject target, out Vector2Int targetCell))
            {
                CurrentDecision = UnitDecision.Idle;
                return;
            }

            int distance = ManhattanDistance(Movement.GridPosition, targetCell);
            if (distance <= _unitData.AttackRange)
            {
                ExecuteDecision(new DecisionCandidate
                {
                    Decision = UnitDecision.Attack,
                    Target = target
                });
                return;
            }

            if (TryGetChaseDestination(targetCell, out Vector2Int destination))
            {
                ExecuteDecision(new DecisionCandidate
                {
                    Decision = UnitDecision.Move,
                    Destination = destination
                });
                return;
            }

            CurrentDecision = UnitDecision.Idle;
        }

        private void ExecuteDecision(DecisionCandidate decision)
        {
            CurrentDecision = decision.Decision;

            switch (decision.Decision)
            {
                case UnitDecision.Attack:
                    if (decision.Target != null)
                        Attack(decision.Target);
                    break;
                case UnitDecision.Move:
                    Move(decision.Destination);
                    break;
            }
        }

        private bool IsValidTarget(GameObject target)
        {
            if (target == null || target == gameObject)
                return false;

            if (target.TryGetComponent(out HealthComponent health) && health.IsDead)
                return false;

            return target.GetComponent<IDamageable>() != null;
        }

        private bool TryAcquireTarget(out GameObject target, out Vector2Int targetCell)
        {
            target = null;
            targetCell = default;

            if (GridSystem.Instance == null || Movement == null)
                return false;

            if (!GridSystem.Instance.IsValidCell(Movement.GridPosition))
                return false;

            if (IsValidTarget(_currentTarget) && TryGetTargetCell(_currentTarget, out targetCell))
            {
                target = _currentTarget;
                return true;
            }

            target = FindNearestTarget(out targetCell);
            _currentTarget = target;
            return target != null;
        }

        private GameObject FindNearestTarget(out Vector2Int targetCell)
        {
            targetCell = default;

            if (GridSystem.Instance == null || Movement == null)
                return null;

            Vector2Int origin = Movement.GridPosition;
            if (!GridSystem.Instance.IsValidCell(origin))
                return null;

            int bestDistance = int.MaxValue;
            GameObject bestTarget = null;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    GameObject occupant = GridSystem.Instance.GetOccupant(cell);
                    if (!IsValidTarget(occupant))
                        continue;

                    int distance = ManhattanDistance(origin, cell);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestTarget = occupant;
                        targetCell = cell;
                    }
                }
            }

            return bestTarget;
        }

        private bool TryGetChaseDestination(Vector2Int targetCell, out Vector2Int destination)
        {
            destination = default;
            bool hasCandidate = false;
            int bestDistance = int.MaxValue;

            foreach (Vector2Int candidate in EnumerateReachableCells())
            {
                int distance = ManhattanDistance(candidate, targetCell);
                if (!hasCandidate || distance < bestDistance)
                {
                    bestDistance = distance;
                    destination = candidate;
                    hasCandidate = true;
                }
            }

            return hasCandidate;
        }

        private IEnumerable<Vector2Int> EnumerateReachableCells()
        {
            if (_unitData == null || Movement == null || GridSystem.Instance == null)
                yield break;

            int moveRange = Mathf.Max(1, _unitData.MoveRange);
            Vector2Int origin = Movement.GridPosition;

            for (int dx = -moveRange; dx <= moveRange; dx++)
            {
                for (int dy = -moveRange; dy <= moveRange; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    Vector2Int candidate = new Vector2Int(origin.x + dx, origin.y + dy);
                    if (GridSystem.Instance.IsValidCell(candidate) && GridSystem.Instance.IsCellFree(candidate))
                        yield return candidate;
                }
            }
        }

        private bool TryGetTargetCell(GameObject target, out Vector2Int targetCell)
        {
            targetCell = default;

            if (target == null)
                return false;

            if (target.TryGetComponent(out MovementComponent targetMovement))
            {
                if (GridSystem.Instance == null)
                    return false;

                targetCell = targetMovement.GridPosition;
                return GridSystem.Instance.IsValidCell(targetCell);
            }

            if (GridSystem.Instance == null)
                return false;

            targetCell = GridSystem.Instance.WorldToGrid(target.transform.position);
            return GridSystem.Instance.IsValidCell(targetCell);
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
