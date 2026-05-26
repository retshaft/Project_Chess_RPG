// UnitBrain.cs
// The controller layer for a single unit.
// Wires together HealthComponent, MovementComponent, and CombatComponent,
// and exposes a simple command API (Move, Attack) consumed by player input
// or an AI decision system.

using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Prediction;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Runtime;
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
        [SerializeField] private string _runtimeActorId;

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
        public Guid ActorId { get; private set; }

        /// <summary>Read-only view of this unit's simulation runtime state.</summary>
        public IReadOnlyUnitRuntimeState RuntimeState => MutableRuntimeState;

        /// <summary>Internal mutable access to this unit's runtime state. Use only from the mutation pipeline.</summary>
        internal UnitRuntimeState MutableRuntimeState { get; private set; }

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

        private struct TargetCandidate
        {
            public UnitBrain Brain;
            public Vector2Int Cell;
        }

        private const float KillValueWeight = 24f;
        private const float MoveKillValueWeight = 10f;
        private const float LethalBonus = 150f;
        private const float KingTargetBonus = 60f;
        private const float SetupKillBonus = 30f;
        private const float WoundedTargetBonus = 24f;
        private const float DistancePenalty = 7f;
        private const float ThreatPenalty = 45f;
        private const float MovePredictionScoreMultiplier = 0.25f;
        private const float MoveOccupancyConflictPenalty = 20f;
        private const float BidKingKillValue = 1000f;
        private const float BidLethalBonus = 200f;
        private const float BidMoveDistancePenalty = 5f;
        private const float BidIdleMoveScore = 1f;

        [SerializeField, Min(1)] private int _maxScenariosPerTick = 24;

        private TeamComponent _team;
        private ActionRuntimeController _runtimeController;
        private AIPredictionAdapter _aiPredictionAdapter;
        private readonly AIEvaluationMetrics _aiEvaluationMetrics = new();

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            Health   = GetComponent<HealthComponent>();
            Movement = GetComponent<MovementComponent>();
            Combat   = GetComponent<CombatComponent>();
            StatusEffects = GetComponent<StatusEffectComponent>();
            _team = GetComponent<TeamComponent>();

            if (!Guid.TryParseExact(_runtimeActorId, "N", out Guid actorId))
            {
                actorId = SeededRandomProvider.Shared.NextGuid();
                _runtimeActorId = actorId.ToString("N");
            }

            ActorId = actorId;
            MutableRuntimeState = new UnitRuntimeState();
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

            _runtimeController = ActionRuntimeController.EnsureExists();
            _runtimeController.RegisterUnit(this);
            _runtimeController.SyncRuntimeState(this);
            _aiPredictionAdapter = _runtimeController.AIPrediction;

            _isInitialised = true;
            Debug.Log($"[UnitBrain] {_unitData.UnitName} initialised at cell {_startCell}.");
        }

        private void OnDestroy()
        {
            if (Health != null)
                Health.OnDeath -= HandleDeath;
            _runtimeController?.UnregisterUnit(this);
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
        /// Queue a move action command for scheduler-driven resolution.
        /// </summary>
        public bool QueueMoveAction(Vector2Int targetCell)
        {
            if (IsDead)
            {
                Debug.LogWarning($"[UnitBrain] {gameObject.name} is dead and cannot move.");
                return false;
            }

            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();

            return _runtimeController.TryEnqueueMove(this, targetCell);
        }

        /// <summary>
        /// Queue an attack action command for scheduler-driven resolution.
        /// </summary>
        public bool QueueAttackAction(GameObject target)
        {
            if (IsDead)
            {
                Debug.LogWarning($"[UnitBrain] {gameObject.name} is dead and cannot attack.");
                return false;
            }

            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();

            return _runtimeController.TryEnqueueAttack(this, target);
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

        public ActionBid GetBestActionBid()
        {
            if (!_isInitialised || IsDead || _unitData == null || Movement == null)
                return default;

            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();
            if (_runtimeController == null)
                return default;

            GridSystem grid = GridSystem.Instance;
            if (grid == null)
                return default;

            bool canAttack = Combat != null && Combat.CanAttack;
            Vector2Int origin = Movement.GridPosition;
            float bestScore = float.MinValue;
            bool bestIsAttack = false;
            Guid bestAttackTargetId = Guid.Empty;
            Vector2Int bestMoveDestination = default;
            float bestRequiredAp = 0f;

            UnitBrain bestTarget = null;
            Vector2Int bestTargetCell = default;
            float bestTargetValue = 0f;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    GameObject occupant = grid.GetOccupant(x, y);
                    if (!TryGetTargetBrain(occupant, out UnitBrain targetBrain))
                        continue;

                    Vector2Int targetCell = targetBrain.Movement.GridPosition;
                    float targetValue = 0f;
                    if (targetBrain.UnitData != null && targetBrain.UnitData.PieceType == ChessPieceType.King)
                        targetValue = BidKingKillValue;
                    else if (targetBrain.UnitData != null)
                        targetValue = Mathf.Max(0f, targetBrain.UnitData.KillValue);

                    if (targetValue > bestTargetValue)
                    {
                        bestTargetValue = targetValue;
                        bestTarget = targetBrain;
                        bestTargetCell = targetCell;
                    }

                    if (!canAttack || !IsAttackRange(origin, targetCell))
                        continue;

                    float attackScore = targetValue;
                    if (CanEliminateTarget(targetBrain))
                        attackScore += BidLethalBonus;

                    if (attackScore > bestScore)
                    {
                        bestScore = attackScore;
                        bestIsAttack = true;
                        bestAttackTargetId = targetBrain.ActorId;
                        bestRequiredAp = Mathf.Max(0f, _unitData.AttackCostAP);
                    }
                }
            }

            float moveBaseCost = Mathf.Max(0f, _unitData.MoveCostAP);
            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);
                    if (!Movement.CanReachCell(candidate))
                        continue;

                    float moveScore = BidIdleMoveScore;
                    if (bestTarget != null)
                    {
                        int distance = ManhattanDistance(candidate, bestTargetCell);
                        moveScore = bestTargetValue - distance * BidMoveDistancePenalty;
                    }

                    if (moveScore <= bestScore)
                        continue;

                    float moveCost = moveBaseCost * grid.GetMoveCostMultiplier(candidate);
                    bestScore = moveScore;
                    bestIsAttack = false;
                    bestMoveDestination = candidate;
                    bestRequiredAp = moveCost;
                }
            }

            if (bestScore == float.MinValue)
                return default;

            IActionCommand bestCommand = bestIsAttack
                ? _runtimeController.BuildAttackPredictionCommand(this, bestAttackTargetId)
                : _runtimeController.BuildMovePredictionCommand(this, bestMoveDestination);

            if (bestCommand == null)
                return default;

            return new ActionBid(this, bestCommand, bestRequiredAp, bestScore);
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

            if (TryExecutePredictionDecision())
                return;

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

        public IReadOnlyList<IActionCommand> BuildPredictionActionCandidates(int maxScenariosPerTick)
        {
            var candidates = new List<IActionCommand>();

            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();
            if (_runtimeController == null || Movement == null)
                return candidates;

            int scenarioCap = Mathf.Max(1, maxScenariosPerTick);

            if (Combat != null && Combat.CanAttack)
            {
                List<TargetCandidate> targets = GetPotentialTargets();
                for (int i = 0; i < targets.Count && candidates.Count < scenarioCap; i++)
                {
                    TargetCandidate target = targets[i];
                    if (target.Brain == null || !IsAttackRange(Movement.GridPosition, target.Cell))
                        continue;

                    IActionCommand attack = _runtimeController.BuildAttackPredictionCommand(this, target.Brain.ActorId);
                    if (attack != null)
                        candidates.Add(attack);
                }
            }

            foreach (Vector2Int cell in Movement.GetReachableCells())
            {
                if (candidates.Count >= scenarioCap)
                    break;

                IActionCommand move = _runtimeController.BuildMovePredictionCommand(this, cell);
                if (move != null)
                    candidates.Add(move);
            }

            AppendAbilityPredictionCandidates(candidates, scenarioCap);
            return candidates;
        }

        private void AppendAbilityPredictionCandidates(List<IActionCommand> _candidates, int _maxScenariosPerTick)
        {
            // TODO(Milestone 13-2): add usable ability/skill action candidates when ability targeting data is exposed.
        }

        private bool TryExecutePredictionDecision()
        {
            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();
            if (_runtimeController == null)
                return false;

            if (_aiPredictionAdapter == null)
                _aiPredictionAdapter = _runtimeController.AIPrediction;
            if (_aiPredictionAdapter == null)
                return false;

            if (!_aiPredictionAdapter.TryGetBestAction(
                    this,
                    _aiEvaluationMetrics,
                    Mathf.Max(1, _maxScenariosPerTick),
                    out IActionCommand bestAction))
            {
                return false;
            }

            return EnqueuePredictedAction(bestAction);
        }

        private bool EnqueuePredictedAction(IActionCommand action)
        {
            if (action == null || action.ActorId != ActorId)
                return false;

            switch (action)
            {
                case MoveActionCommand move:
                    if (QueueMoveAction(move.To))
                    {
                        CurrentDecision = UnitDecision.Move;
                        return true;
                    }
                    break;
                case AttackActionCommand attack:
                    if (TryResolveTargetByActorId(attack.TargetId, out GameObject target) && QueueAttackAction(target))
                    {
                        _currentTarget = target;
                        CurrentDecision = UnitDecision.Attack;
                        return true;
                    }
                    break;
            }

            return false;
        }

        private static bool TryResolveTargetByActorId(Guid actorId, out GameObject target)
        {
            target = null;
            if (actorId == Guid.Empty)
                return false;

            if (GridSystem.Instance == null)
                return false;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    GameObject occupant = GridSystem.Instance.GetOccupant(x, y);
                    if (occupant == null || !occupant.TryGetComponent(out UnitBrain brain))
                        continue;
                    if (brain.ActorId != actorId || brain.IsDead)
                        continue;

                    target = occupant;
                    return true;
                }
            }

            return false;
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
                        QueueAttackAction(decision.Target);
                    break;
                case UnitDecision.Move:
                    QueueMoveAction(decision.Destination);
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

            if (AreAllies(target))
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
            var potentialTargets = GetPotentialTargets();

            foreach (TargetCandidate target in potentialTargets)
            {
                if (Combat != null && Combat.CanAttack && IsAttackRange(Movement.GridPosition, target.Cell))
                {
                    float attackScore = ScoreAttackTarget(target.Brain, target.Cell);
                    if (!hasCandidate || attackScore > bestDecision.Score)
                    {
                        bestDecision = new DecisionCandidate
                        {
                            Decision = UnitDecision.Attack,
                            Target = target.Brain.gameObject,
                            Score = attackScore
                        };
                        hasCandidate = true;
                    }
                }
            }

            foreach (Vector2Int cell in reachableCells)
            {
                foreach (TargetCandidate target in potentialTargets)
                {
                    float moveScore = ScoreMoveTarget(cell, target.Brain, target.Cell);
                    if (!hasCandidate || moveScore > bestDecision.Score)
                    {
                        bestDecision = new DecisionCandidate
                        {
                            Decision = UnitDecision.Move,
                            Destination = cell,
                            Target = target.Brain.gameObject,
                            Score = moveScore
                        };
                        hasCandidate = true;
                    }
                }
            }

            return hasCandidate;
        }

        private System.Collections.Generic.List<TargetCandidate> GetPotentialTargets()
        {
            var targets = new System.Collections.Generic.List<TargetCandidate>();
            if (GridSystem.Instance == null)
                return targets;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    GameObject occupant = GridSystem.Instance.GetOccupant(x, y);
                    if (TryGetTargetBrain(occupant, out UnitBrain targetBrain))
                    {
                        targets.Add(new TargetCandidate
                        {
                            Brain = targetBrain,
                            Cell = targetBrain.Movement.GridPosition
                        });
                    }
                }
            }

            return targets;
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
            score += ScorePredictionAttack(targetBrain);
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

            score += ScorePredictionMove(candidateCell);
            return score;
        }

        private float ScorePredictionAttack(UnitBrain targetBrain)
        {
            if (targetBrain == null || _runtimeController == null)
                return 0f;
            if (_aiPredictionAdapter == null)
                _aiPredictionAdapter = _runtimeController.AIPrediction;
            if (_aiPredictionAdapter == null)
                return 0f;

            IActionCommand command = _runtimeController.BuildAttackPredictionCommand(this, targetBrain.ActorId);
            if (command == null)
                return 0f;

            PredictionActionEvaluation evaluation = _aiPredictionAdapter.Evaluate(command);
            return evaluation.Score;
        }

        private float ScorePredictionMove(Vector2Int targetCell)
        {
            if (_runtimeController == null)
                return 0f;
            if (_aiPredictionAdapter == null)
                _aiPredictionAdapter = _runtimeController.AIPrediction;
            if (_aiPredictionAdapter == null)
                return 0f;

            IActionCommand command = _runtimeController.BuildMovePredictionCommand(this, targetCell);
            if (command == null)
                return 0f;

            PredictionActionEvaluation evaluation = _aiPredictionAdapter.Evaluate(command);
            return evaluation.OccupancyConflict
                ? -MoveOccupancyConflictPenalty
                : evaluation.Score * MovePredictionScoreMultiplier;
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

        private bool AreAllies(GameObject target)
        {
            if (_team == null || target == null || !target.TryGetComponent(out TeamComponent targetTeam))
                return false;

            return targetTeam.IsEnemy == _team.IsEnemy;
        }

        private bool IsAttackRange(Vector2Int origin, Vector2Int targetCell)
        {
            ChessPieceType pieceType = _unitData != null ? _unitData.PieceType : ChessPieceType.Pawn;
            int attackRange = _unitData != null ? _unitData.AttackRange : 1;
            bool isEnemy = _team != null && _team.IsEnemy;
            return CombatPatternRules.IsAttackReachable(pieceType, isEnemy, origin, targetCell, attackRange);
        }

        private static bool IsAttackRange(Vector2Int origin, Vector2Int targetCell, int attackRange, ChessPieceType pieceType = ChessPieceType.Pawn) =>
            CombatPatternRules.IsAttackReachable(pieceType, isEnemy: false, origin, targetCell, attackRange);

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
