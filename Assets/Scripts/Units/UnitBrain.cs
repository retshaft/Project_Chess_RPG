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
    [RequireComponent(typeof(StatusEffectComponent))]
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

        /// <summary>Read-only access to this unit's status effect state.</summary>
        public StatusEffectComponent StatusEffects { get; private set; }

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
            public float Score;
        }

        private const float KillValueWeight = 24f;
        private const float MoveKillValueWeight = 10f;
        private const float LethalBonus = 150f;
        private const float KingTargetBonus = 60f;
        private const float SetupKillBonus = 30f;
        private const float WoundedTargetBonus = 24f;
        private const float DistancePenalty = 7f;
        private const float ThreatPenalty = 45f;

        private TeamComponent _team;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            Health   = GetComponent<HealthComponent>();
            Movement = GetComponent<MovementComponent>();
            Combat   = GetComponent<CombatComponent>();
            StatusEffects = GetComponent<StatusEffectComponent>();
            _team = GetComponent<TeamComponent>();
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
            StatusEffects.Initialise(_unitData);

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
        /// Set data before Start() runs. Call this from a spawner during its Awake()
        /// immediately after AddComponent&lt;UnitBrain&gt;(), so that Start() finds the fields
        /// already populated when it initialises the components.
        /// </summary>
        public void Prepare(UnitData data, Vector2Int startCell)
        {
            _unitData  = data;
            _startCell = startCell;
        }

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

            if (TryGetOverrideDecision(out DecisionCandidate overrideDecision))
            {
                ExecuteDecision(overrideDecision);
                return;
            }

            if (TryGetBestDecision(out DecisionCandidate bestDecision))
            {
                ExecuteDecision(bestDecision);
                return;
            }

            CurrentDecision = UnitDecision.Idle;
        }

        private void ExecuteDecision(DecisionCandidate decision)
        {
            CurrentDecision = decision.Decision;
            if (decision.Target != null)
                _currentTarget = decision.Target;

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

            if (target.GetComponent<IDamageable>() == null)
                return false;

            if (_team != null && target.TryGetComponent(out TeamComponent targetTeam) && targetTeam.IsEnemy == _team.IsEnemy)
                return false;

            return true;
        }

        private bool TryGetOverrideDecision(out DecisionCandidate decision)
        {
            decision = default;

            if (TryGetCheckmateDecision(out decision))
                return true;

            return TryGetDangerDecision(out decision);
        }

        private bool TryGetCheckmateDecision(out DecisionCandidate decision)
        {
            decision = default;

            if (GridSystem.Instance == null || Movement == null || Combat == null || !Combat.CanAttack)
                return false;

            float bestScore = float.MinValue;
            bool hasCandidate = false;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    GameObject occupant = GridSystem.Instance.GetOccupant(x, y);
                    if (!TryGetTargetBrain(occupant, out UnitBrain targetBrain))
                        continue;

                    if (targetBrain.UnitData == null || targetBrain.UnitData.PieceType != ChessPieceType.King)
                        continue;

                    Vector2Int targetCell = targetBrain.Movement.GridPosition;
                    if (!IsAttackRange(Movement.GridPosition, targetCell))
                        continue;

                    if (!CanEliminateTarget(targetBrain))
                        continue;

                    float score = ScoreAttackTarget(targetBrain, targetCell) + LethalBonus + KingTargetBonus;
                    if (!hasCandidate || score > bestScore)
                    {
                        bestScore = score;
                        decision = new DecisionCandidate
                        {
                            Decision = UnitDecision.Attack,
                            Target = targetBrain.gameObject,
                            Score = score
                        };
                        hasCandidate = true;
                    }
                }
            }

            return hasCandidate;
        }

        private bool TryGetDangerDecision(out DecisionCandidate decision)
        {
            decision = default;

            if (_unitData == null || _unitData.PieceType != ChessPieceType.King || Movement == null)
                return false;

            if (GridSystem.Instance == null || Movement == null)
                return false;

            int currentThreat = CountThreatsAgainstCell(Movement.GridPosition);
            if (currentThreat <= 0)
                return false;

            float bestScore = float.MinValue;
            bool hasCandidate = false;

            foreach (Vector2Int candidate in Movement.GetReachableCells())
            {
                int threatCount = CountThreatsAgainstCell(candidate);
                if (threatCount >= currentThreat)
                    continue;

                float score = (currentThreat - threatCount) * ThreatPenalty + ScoreBoardControl(candidate);
                if (!hasCandidate || score > bestScore)
                {
                    bestScore = score;
                    decision = new DecisionCandidate
                    {
                        Decision = UnitDecision.Move,
                        Destination = candidate,
                        Score = score
                    };
                    hasCandidate = true;
                }
            }

            return hasCandidate;
        }

        private bool TryGetBestDecision(out DecisionCandidate bestDecision)
        {
            bestDecision = default;

            if (GridSystem.Instance == null || Movement == null || _unitData == null)
                return false;

            bool hasCandidate = false;
            var reachableCells = Movement.GetReachableCells();

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    GameObject occupant = GridSystem.Instance.GetOccupant(x, y);
                    if (!TryGetTargetBrain(occupant, out UnitBrain targetBrain))
                        continue;

                    Vector2Int targetCell = targetBrain.Movement.GridPosition;

                    if (Combat != null && Combat.CanAttack && IsAttackRange(Movement.GridPosition, targetCell))
                    {
                        float attackScore = ScoreAttackTarget(targetBrain, targetCell);
                        if (!hasCandidate || attackScore > bestDecision.Score)
                        {
                            bestDecision = new DecisionCandidate
                            {
                                Decision = UnitDecision.Attack,
                                Target = targetBrain.gameObject,
                                Score = attackScore
                            };
                            hasCandidate = true;
                        }
                    }

                    foreach (Vector2Int cell in reachableCells)
                    {
                        float moveScore = ScoreMoveTarget(cell, targetBrain, targetCell);
                        if (!hasCandidate || moveScore > bestDecision.Score)
                        {
                            bestDecision = new DecisionCandidate
                            {
                                Decision = UnitDecision.Move,
                                Destination = cell,
                                Target = targetBrain.gameObject,
                                Score = moveScore
                            };
                            hasCandidate = true;
                        }
                    }
                }
            }

            return hasCandidate;
        }

        private bool TryGetTargetBrain(GameObject target, out UnitBrain targetBrain)
        {
            targetBrain = null;
            if (!IsValidTarget(target))
                return false;

            targetBrain = target.GetComponent<UnitBrain>();
            return targetBrain != null && targetBrain.Movement != null;
        }

        private float ScoreAttackTarget(UnitBrain targetBrain, Vector2Int targetCell)
        {
            float score = 0f;

            if (targetBrain.UnitData != null)
            {
                score += targetBrain.UnitData.KillValue * KillValueWeight;
                if (targetBrain.UnitData.PieceType == ChessPieceType.King)
                    score += KingTargetBonus;
            }

            if (targetBrain.Health != null && targetBrain.Health.MaxHealth > 0f)
            {
                float missingRatio = 1f - (targetBrain.Health.CurrentHealth / targetBrain.Health.MaxHealth);
                score += missingRatio * WoundedTargetBonus;
            }

            if (CanEliminateTarget(targetBrain))
                score += LethalBonus;

            score -= ManhattanDistance(Movement.GridPosition, targetCell) * DistancePenalty;
            return score;
        }

        private float ScoreMoveTarget(Vector2Int candidateCell, UnitBrain targetBrain, Vector2Int targetCell)
        {
            float score = 0f;

            if (targetBrain.UnitData != null)
            {
                score += targetBrain.UnitData.KillValue * MoveKillValueWeight;
                if (targetBrain.UnitData.PieceType == ChessPieceType.King)
                    score += KingTargetBonus * 0.5f;
            }

            int distance = ManhattanDistance(candidateCell, targetCell);
            score -= distance * DistancePenalty;
            score += ScoreBoardControl(candidateCell);

            if (IsAttackRange(candidateCell, targetCell))
                score += SetupKillBonus;

            if (_unitData.PieceType == ChessPieceType.King)
                score -= CountThreatsAgainstCell(candidateCell) * ThreatPenalty;

            return score;
        }

        private float ScoreBoardControl(Vector2Int cell)
        {
            Vector2 center = new Vector2((GridSystem.GridWidth - 1) * 0.5f, (GridSystem.GridHeight - 1) * 0.5f);
            float centerDistance = Mathf.Abs(cell.x - center.x) + Mathf.Abs(cell.y - center.y);
            return 8f - centerDistance;
        }

        private int CountThreatsAgainstCell(Vector2Int cell)
        {
            if (GridSystem.Instance == null)
                return 0;

            int threatCount = 0;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    GameObject occupant = GridSystem.Instance.GetOccupant(x, y);
                    if (!TryGetTargetBrain(occupant, out UnitBrain enemyBrain))
                        continue;

                    if (enemyBrain.UnitData == null)
                        continue;

                    if (IsAttackRange(enemyBrain.Movement.GridPosition, cell, enemyBrain.UnitData.AttackRange))
                        threatCount++;
                }
            }

            return threatCount;
        }

        private bool CanEliminateTarget(UnitBrain targetBrain)
        {
            if (targetBrain == null || targetBrain.Health == null || targetBrain.UnitData == null || _unitData == null)
                return false;

            float defense = Mathf.Clamp01(targetBrain.UnitData.Defense);
            float estimatedDamage = Mathf.Max(0f, _unitData.AttackDamage * (1f - defense));
            return estimatedDamage >= targetBrain.Health.CurrentHealth;
        }

        private bool IsAttackRange(Vector2Int origin, Vector2Int targetCell)
        {
            return IsAttackRange(origin, targetCell, _unitData != null ? _unitData.AttackRange : 1);
        }

        private static bool IsAttackRange(Vector2Int origin, Vector2Int targetCell, int attackRange)
        {
            int dx = Mathf.Abs(origin.x - targetCell.x);
            int dy = Mathf.Abs(origin.y - targetCell.y);
            return Mathf.Max(dx, dy) <= Mathf.Max(1, attackRange);
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

        /// <summary>
        /// Manhattan (diamond) distance between two grid cells.
        /// </summary>
        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
