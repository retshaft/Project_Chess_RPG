// UnitBrain.cs
// The controller layer for a single unit.
// Wires together HealthComponent, MovementComponent, and CombatComponent,
// and exposes a simple command API (Move, Attack) consumed by player input
// or an AI decision system.

using System;
using System.Collections;
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
    [RequireComponent(typeof(UnitVisualController))]
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

        [Header("Promotion")]
        [Tooltip("프로모션 대기 시간 (초)")]
        [SerializeField] private float _promotionDelay = 3.0f;

        // ─── Component References ─────────────────────────────────────────────────

        /// <summary>Read-only access to this unit's health state.</summary>
        public HealthComponent   Health   { get; private set; }

        /// <summary>Read-only access to this unit's movement state.</summary>
        public MovementComponent Movement { get; private set; }

        /// <summary>Read-only access to this unit's combat state.</summary>
        public CombatComponent   Combat   { get; private set; }

        /// <summary>Read-only access to this unit's status effect state.</summary>
        public StatusEffectComponent StatusEffects { get; private set; }

        /// <summary>Read-only access to this unit's subclass logic.</summary>
        public SubclassComponent SubclassComponent { get; private set; }

        /// <summary>Read-only access to this unit's SP logic.</summary>
        public SPComponent SPComponent { get; private set; }

        /// <summary>Read-only access to the assigned unit data.</summary>
        public UnitData UnitData { get => _unitData; set => _unitData = value; }
        public Guid ActorId { get; private set; }

        /// <summary>Read-only view of this unit's simulation runtime state.</summary>
        public IReadOnlyUnitRuntimeState RuntimeState => MutableRuntimeState;

        /// <summary>Internal mutable access to this unit's runtime state. Use only from the mutation pipeline.</summary>
        internal UnitRuntimeState MutableRuntimeState { get; private set; }

        // ─── State ────────────────────────────────────────────────────────────────

        /// <summary>True once the unit has been killed.</summary>
        public bool IsDead => Health != null && Health.IsDead;

        /// <summary>True if the unit is waiting to be promoted.</summary>
        public bool IsPromoting { get; private set; }

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
            SubclassComponent = GetComponent<SubclassComponent>();
            SPComponent = GetComponent<SPComponent>();
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
            if (SubclassComponent != null) SubclassComponent.Initialise(_unitData);
            if (SPComponent != null) SPComponent.Initialise(_unitData);

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
        /// Update the unit data at runtime (e.g. for promotion) and re-initialise components.
        /// </summary>
        public void ChangeUnitData(UnitData newData)
        {
            if (newData == null) return;
            
            _unitData = newData;
            
            if (Health != null) Health.Initialise(_unitData);
            if (Movement != null) Movement.Initialise(_unitData, Movement.GridPosition);
            if (Combat != null) Combat.Initialise(_unitData);
            if (StatusEffects != null) StatusEffects.Initialise(_unitData);
            if (SubclassComponent != null) SubclassComponent.Initialise(_unitData);
            if (SPComponent != null) SPComponent.Initialise(_unitData);
            
            Debug.Log($"[UnitBrain] {gameObject.name} changed data to {_unitData.UnitName}.");
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

            if (Movement != null && Movement.IsMoving)
                return false;

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

            if (Movement != null && Movement.IsMoving)
            {
                Debug.LogWarning($"[UnitBrain] {gameObject.name} cannot attack because it is moving.");
                return false;
            }

            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();

            if (UnitData != null && UnitData.BasicAttackAbility != null)
            {
                var targetBrain = target.GetComponent<UnitBrain>();
                if (targetBrain != null)
                {
                    bool success = _runtimeController.TryEnqueueAbility(this, UnitData.BasicAttackAbility, new[] { targetBrain.ActorId });
                    if (!success)
                        Debug.LogWarning($"[UnitBrain] {gameObject.name} _runtimeController.TryEnqueueAbility returned false for target {target.name}");
                    return success;
                }
            }

            bool legacySuccess = _runtimeController.TryEnqueueAttack(this, target);
            if (!legacySuccess)
                Debug.LogWarning($"[UnitBrain] {gameObject.name} _runtimeController.TryEnqueueAttack returned false for target {target.name}");
            return legacySuccess;
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

        public ActionBid GetBestActionBid(float currentTeamAP, float maxTeamAP)
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

            return TacticalAIEvaluator.EvaluateBestAction(this, currentTeamAP, maxTeamAP);
        }

        // ─── Event Handlers ───────────────────────────────────────────────────────

        private void HandleDeath()
        {
            Debug.Log($"[UnitBrain] {gameObject.name} has died.");
            Combat.OnOwnerDied();

            StartCoroutine(DeathDelayRoutine());
        }

        private IEnumerator DeathDelayRoutine()
        {
            // Future: Trigger death animation state here
            yield return new WaitForSeconds(2.0f);
            
            gameObject.SetActive(false);
        }

        // ─── Promotion Logic ──────────────────────────────────────────────────────

        public void StartPromotion()
        {
            if (IsPromoting || IsDead) return;
            StartCoroutine(PromotionRoutine());
        }

        private IEnumerator PromotionRoutine()
        {
            IsPromoting = true;
            Debug.Log($"[UnitBrain] {gameObject.name} entered Promotion Pending state.");

            // 프로모션 대기 중 받는 대미지 70% 감소 버프 (기획서 명세)
            if (Health != null)
            {
                Health.SetDamageTakenMultiplier(0.3f); 
            }

            // 대기 시간 (임시로 WaitForSeconds, 향후 Pause/틱 동기화 시 커스텀 Yield 고려)
            yield return new WaitForSeconds(_promotionDelay);

            if (IsDead)
            {
                IsPromoting = false;
                yield break;
            }

            // 대기 종료, 버프 해제
            if (Health != null)
            {
                Health.SetDamageTakenMultiplier(1.0f);
            }

            // 프로모션 타겟 지정 및 변환
            ChessPieceType targetPiece = PromotionRegistry.GetPromotionTarget(_unitData.Subclass);
            Debug.Log($"[UnitBrain] {gameObject.name} promoting to {targetPiece}!");

            // TODO: 실제 시스템에서는 Resource/Addressables 등에서 완성된 승급 데이터를 로드해 와야 합니다.
            // 여기서는 뼈대 구축을 위해 런타임에 임시 데이터로 승급을 모사합니다.
            var promotedData = ScriptableObject.CreateInstance<UnitData>();
            promotedData.UnitName = $"Promoted {_unitData.UnitName}";
            promotedData.PieceType = targetPiece;
            promotedData.Subclass = _unitData.Subclass; // 서브클래스 유지
            promotedData.SyncDefaultChessMetadata();
            
            // 능력치 상승 뼈대 (예시)
            promotedData.MaxHealth = _unitData.MaxHealth * 1.5f;
            promotedData.AttackDamage = _unitData.AttackDamage * 1.5f;
            promotedData.MoveRange = 8;
            promotedData.ActionSpeed = _unitData.ActionSpeed;
            promotedData.Weight = _unitData.Weight;
            promotedData.UpdateMoveSpeed();

            // 체력 비율 유지용 임시 저장
            float hpPercent = 1f;
            if (Health != null)
            {
                hpPercent = Health.CurrentHealth / Mathf.Max(1f, Health.MaxHealth);
            }

            // ChangeUnitData 호출로 컴포넌트들 재초기화
            ChangeUnitData(promotedData);

            // 체력 비율 복구 및 디버프/AP 처리
            if (Health != null)
            {
                float damageToTake = Health.MaxHealth * (1f - hpPercent);
                if (damageToTake > 0)
                {
                    Health.ApplyTrueDamage(damageToTake);
                }
            }

            if (StatusEffects != null)
            {
                foreach (StatusEffectType type in System.Enum.GetValues(typeof(StatusEffectType)))
                {
                    StatusEffects.RemoveStatusEffect(type);
                }
            }

            // 글로벌 AP 100% 회복 (아군일 경우에만)
            bool isEnemy = false;
            if (TryGetComponent(out TeamComponent team))
                isEnemy = team.IsEnemy;

            if (!isEnemy && APManager.Instance != null)
            {
                APManager.Instance.AddAP(APManager.Instance.MaxAP, APSource.Bonus);
            }

            IsPromoting = false;
            Debug.Log($"[UnitBrain] {gameObject.name} promotion complete.");
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

                    if (UnitData != null && UnitData.BasicAttackAbility != null)
                    {
                        IActionCommand abilityAttack = _runtimeController.BuildAbilityPredictionCommand(this, UnitData.BasicAttackAbility, new[] { target.Brain.ActorId });
                        if (abilityAttack != null)
                            candidates.Add(abilityAttack);
                    }
                    else
                    {
                        IActionCommand attack = _runtimeController.BuildAttackPredictionCommand(this, target.Brain.ActorId);
                        if (attack != null)
                            candidates.Add(attack);
                    }
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

        private void AppendAbilityPredictionCandidates(List<IActionCommand> candidates, int maxScenariosPerTick)
        {
            if (UnitData == null || UnitData.Abilities == null || candidates.Count >= maxScenariosPerTick)
                return;

            List<TargetCandidate> targets = GetPotentialTargets();

            for (int i = 0; i < UnitData.Abilities.Count; i++)
            {
                Core.AbilityDefinition ability = UnitData.Abilities[i];
                if (ability == null || candidates.Count >= maxScenariosPerTick) continue;

                // 스킬 범위/타겟 판단은 고도화가 필요하며 현재는 사거리 내 단일 타겟을 가정합니다.
                for (int j = 0; j < targets.Count && candidates.Count < maxScenariosPerTick; j++)
                {
                    TargetCandidate target = targets[j];
                    if (target.Brain == null || !IsAttackRange(Movement.GridPosition, target.Cell)) 
                        continue;

                    IActionCommand abilityCmd = _runtimeController.BuildAbilityPredictionCommand(this, ability, new[] { target.Brain.ActorId });
                    if (abilityCmd != null)
                        candidates.Add(abilityCmd);
                }
            }
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

            if (GridSystem.Instance != null)
            {
                // 특수 타일 기믹 회피 (예: AP 비용이 높거나 이동 속도를 깎는 타일 회피)
                float costMultiplier = GridSystem.Instance.GetMoveCostMultiplier(candidateCell);
                if (costMultiplier > 1f) score -= (costMultiplier - 1f) * 15f; // 늪지대 페널티
                
                float speedModifier = GridSystem.Instance.GetMoveSpeedModifier(candidateCell);
                if (speedModifier < 0f) score -= 10f; // 둔화 타일 페널티
            }

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

        public bool IsAttackRange(Vector2Int origin, Vector2Int targetCell)
        {
            ChessPieceType pieceType = _unitData != null ? _unitData.PieceType : ChessPieceType.Pawn;
            int attackRange = _unitData != null ? _unitData.AttackRange : 1;
            bool isEnemy = _team != null && _team.IsEnemy;
            return CombatPatternRules.IsAttackReachable(pieceType, isEnemy, origin, targetCell, attackRange);
        }

        public static bool IsAttackRange(Vector2Int origin, Vector2Int targetCell, int attackRange, ChessPieceType pieceType = ChessPieceType.Pawn, bool isEnemy = false) =>
            CombatPatternRules.IsAttackReachable(pieceType, isEnemy, origin, targetCell, attackRange);

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
