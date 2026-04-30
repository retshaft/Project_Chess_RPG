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

        // Matches CombatComponent's world-space fallback range (≈ √2 allowance for diagonals).
        private const float WorldRangeMultiplier = 1.5f;
        private const float DefaultKillValue = 10f;
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

            if (!IsValidTarget(_currentTarget))
            {
                CurrentDecision = UnitDecision.Idle;
                return;
            }

            if (TryGetOverrideDecision(_currentTarget, out DecisionCandidate overrideDecision))
            {
                ExecuteDecision(overrideDecision);
                return;
            }

            if (TryGetBestScoredDecision(_currentTarget, out DecisionCandidate scoredDecision))
            {
                ExecuteDecision(scoredDecision);
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

        private bool TryGetOverrideDecision(GameObject target, out DecisionCandidate decision)
        {
            if (TryGetCheckmateDecision(target, out decision))
                return true;

            if (TryGetDangerDecision(target, out decision))
                return true;

            decision = default;
            return false;
        }

        private bool TryGetCheckmateDecision(GameObject target, out DecisionCandidate decision)
        {
            decision = default;

            if (!Combat.CanAttack || !IsTargetInAttackRange(target))
                return false;

            if (!TryGetTargetHealth(target, out HealthComponent targetHealth))
                return false;

            if (targetHealth.CurrentHealth > _unitData.AttackDamage)
                return false;

            decision = new DecisionCandidate
            {
                Decision = UnitDecision.Attack,
                Target = target
            };
            return true;
        }

        private bool TryGetDangerDecision(GameObject target, out DecisionCandidate decision)
        {
            decision = default;

            if (!TryGetTargetCell(target, out Vector2Int targetCell))
                return false;

            if (!TryGetTargetAttackRange(target, out int targetRange) || targetRange <= 0)
                return false;

            int currentDistance = ChebyshevDistance(Movement.GridPosition, targetCell);
            if (currentDistance > targetRange)
                return false;

            if (!TryGetSafestDestination(targetCell, targetRange, out Vector2Int safeCell))
                return false;

            decision = new DecisionCandidate
            {
                Decision = UnitDecision.Move,
                Destination = safeCell
            };
            return true;
        }

        private bool TryGetBestScoredDecision(GameObject target, out DecisionCandidate decision)
        {
            decision = default;

            if (!TryGetTargetCell(target, out Vector2Int targetCell))
                return false;

            float bestScore = 0f;
            bool hasDecision = false;

            if (Combat.CanAttack && IsTargetInAttackRange(target))
            {
                float attackScore = ScoreAttackCandidate(target);
                if (attackScore > bestScore)
                {
                    bestScore = attackScore;
                    decision = new DecisionCandidate
                    {
                        Decision = UnitDecision.Attack,
                        Target = target
                    };
                    hasDecision = true;
                }
            }

            int targetRange = TryGetTargetAttackRange(target, out int range) ? range : 0;
            foreach (Vector2Int destination in EnumerateReachableCells())
            {
                float moveScore = ScoreMoveCandidate(destination, targetCell, targetRange);
                if (moveScore > bestScore)
                {
                    bestScore = moveScore;
                    decision = new DecisionCandidate
                    {
                        Decision = UnitDecision.Move,
                        Destination = destination
                    };
                    hasDecision = true;
                }
            }

            return hasDecision;
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

            float worldRange = _unitData.AttackRange * WorldRangeMultiplier;
            return Vector3.Distance(transform.position, target.transform.position) <= worldRange;
        }

        private float ScoreAttackCandidate(GameObject target)
        {
            float score = _unitData.AttackDamage;

            if (TryGetTargetHealth(target, out HealthComponent targetHealth) &&
                targetHealth.CurrentHealth <= _unitData.AttackDamage)
            {
                score += GetKillValue(target);
            }

            return score;
        }

        private float ScoreMoveCandidate(Vector2Int destination, Vector2Int targetCell, int targetRange)
        {
            int currentDistance = ChebyshevDistance(Movement.GridPosition, targetCell);
            int newDistance = ChebyshevDistance(destination, targetCell);
            int distanceImprovement = currentDistance - newDistance;

            float attackRangeBonus = newDistance <= _unitData.AttackRange ? _unitData.AttackDamage : 0f;
            float safetyBonus = targetRange > 0 && newDistance > targetRange
                ? (Health != null ? Health.CurrentHealth : _unitData.MaxHealth)
                : 0f;

            return distanceImprovement + attackRangeBonus + safetyBonus;
        }

        private bool TryGetSafestDestination(Vector2Int targetCell, int targetRange, out Vector2Int destination)
        {
            destination = default;
            bool hasCandidate = false;
            int bestDistance = int.MinValue;

            foreach (Vector2Int candidate in EnumerateReachableCells())
            {
                int distance = ChebyshevDistance(candidate, targetCell);
                if (distance <= targetRange)
                    continue;

                if (!hasCandidate || distance > bestDistance)
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

        private bool TryGetTargetAttackRange(GameObject target, out int attackRange)
        {
            attackRange = 0;

            if (target == null)
                return false;

            if (target.TryGetComponent(out UnitBrain targetBrain) && targetBrain.UnitData != null)
            {
                attackRange = Mathf.Max(0, targetBrain.UnitData.AttackRange);
                return true;
            }

            return false;
        }

        private bool TryGetTargetHealth(GameObject target, out HealthComponent health)
        {
            health = null;

            if (target == null)
                return false;

            if (target.TryGetComponent(out HealthComponent targetHealth))
            {
                health = targetHealth;
                return true;
            }

            return false;
        }

        private float GetKillValue(GameObject target)
        {
            if (target != null && target.TryGetComponent(out UnitBrain targetBrain) && targetBrain.UnitData != null)
                return Mathf.Max(0f, targetBrain.UnitData.KillValue);

            return DefaultKillValue;
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
            return GridSystem.Instance.IsValidCell(targetCell);
        }

        private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }
    }
}
