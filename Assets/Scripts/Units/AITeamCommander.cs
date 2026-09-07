using System.Collections.Generic;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Progression;
using UnityEngine;

namespace CheckmateRPG.Units
{
    public sealed class AITeamCommander : MonoBehaviour
    {
        [SerializeField] private bool _isEnemyTeam = true;
        [SerializeField, Min(1)] private int _evaluationTickInterval = 5;
        [Tooltip("한 평가 틱에 집행할 최대 입찰 수. 1이면 유닛 행동이 순차적으로 분산된다.")]
        [SerializeField, Min(1)] private int _maxExecutionsPerTick = 1;
        [SerializeField] private List<UnitBrain> _teamMembers = new List<UnitBrain>();

        private readonly List<ActionBid> _pendingBids = new List<ActionBid>();
        private TickScheduler _tickScheduler;
        private int _manualTickCounter;
        private int _lastEvaluatedTick = -1;
        private int _lastNoActionWarningTick = -1000;
        private ActionRuntimeController _runtimeController;
        
        [Header("Enemy AP Pool")]
        [SerializeField] private float _maxAP = 100f;
        [SerializeField] private float _regenPerSecond = 4f;
        public float CurrentTeamAP { get; private set; }
        public float MaxTeamAP => _maxAP;

        public bool IsEnemyTeam => _isEnemyTeam;

        public void ConfigureFromStage(StageData stage)
        {
            if (stage == null) return;
            _maxAP = stage.MaxEnemyAP > 0 ? stage.MaxEnemyAP : 100f;
            _regenPerSecond = stage.EnemyAPRegen;
            CurrentTeamAP = stage.InitialEnemyAP;
            Debug.Log($"[AITeamCommander] Configured from stage '{stage.StageName}': AP={CurrentTeamAP}/{_maxAP}, Regen={_regenPerSecond}");
        }

        private void OnEnable()
        {
            BindTickScheduler();
        }

        private void OnDisable()
        {
            if (_tickScheduler != null)
                _tickScheduler.OnTick -= HandleSchedulerTick;
        }

        public void RegisterUnit(UnitBrain unit)
        {
            if (unit != null && !_teamMembers.Contains(unit))
                _teamMembers.Add(unit);
        }

        public void UnregisterUnit(UnitBrain unit)
        {
            if (unit != null)
                _teamMembers.Remove(unit);
        }

        public void TickCommander()
        {
            if (_tickScheduler != null)
            {
                HandleSchedulerTick(_tickScheduler.CurrentTick);
                return;
            }

            _manualTickCounter++;
            HandleSchedulerTick(_manualTickCounter);
        }

        public void TickCommander(int logicalTick)
        {
            HandleSchedulerTick(logicalTick);
        }

        private void BindTickScheduler()
        {
            TickScheduler scheduler = TickScheduler.EnsureExists();

            if (_tickScheduler != null)
                _tickScheduler.OnTick -= HandleSchedulerTick;

            _tickScheduler = scheduler;
            _tickScheduler.OnTick += HandleSchedulerTick;
        }

        private void Update()
        {
            if (_runtimeController != null && _runtimeController.Scheduler != null)
            {
                // Regenerate Enemy AP every frame based on real delta time
                CurrentTeamAP = Mathf.Min(_maxAP, CurrentTeamAP + _regenPerSecond * Time.deltaTime);
            }
        }

        private void OnGUI()
        {
            if (!IsEnemyTeam) return;
            
            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.normal.textColor = Color.red;
            style.alignment = TextAnchor.UpperRight;

            GUI.Label(new Rect(Screen.width - 220, 20, 200, 40), $"Enemy AP: {Mathf.FloorToInt(CurrentTeamAP)} / {_maxAP}", style);
        }

        private void HandleSchedulerTick(int currentTick)
        {
            Debug.Assert(currentTick >= 0, "[AITeamCommander] Logical tick must not be negative.");
            if (_evaluationTickInterval <= 0)
                return;
            if (currentTick < 0 || currentTick % _evaluationTickInterval != 0)
                return;
            if (_lastEvaluatedTick == currentTick)
                return;

            _lastEvaluatedTick = currentTick;


            EvaluateAndExecuteTeamActions(currentTick);
        }

        private void EvaluateAndExecuteTeamActions(int currentTick)
        {
            _pendingBids.Clear();
            ThreatMap.UpdateThreatMap();

            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();

            for (int i = 0; i < _teamMembers.Count; i++)
            {
                UnitBrain unit = _teamMembers[i];
                if (unit == null || unit.IsDead || !IsMatchingTeam(unit))
                    continue;

                // Skip if unit is already busy (locked in an action or moving)
                if (unit.Movement != null && unit.Movement.IsMoving)
                    continue;
                    
                if (_runtimeController != null && _runtimeController.Scheduler != null)
                {
                    // ActionScheduler handles locks natively on ScheduleAction, so we don't need to manually check here.
                }

                ActionBid bid = unit.GetBestActionBid(CurrentTeamAP, _maxAP);
                if (bid.IsValid)
                    _pendingBids.Add(bid);
            }

            if (_pendingBids.Count == 0)
            {
                // 이동 쿨타임 등으로 입찰이 비는 것은 정상 상태이므로, 진단 경고는 주기를 제한해 노이즈를 막는다.
                if (CurrentTeamAP > 10f && currentTick - _lastNoActionWarningTick >= 100)
                {
                    _lastNoActionWarningTick = currentTick;
                    Debug.LogWarning($"[AITeamCommander] No actions found for {_teamMembers.Count} members. AP: {CurrentTeamAP}. Check if targets have TeamComponent(IsEnemy=false).");
                }
                return;
            }

            _pendingBids.Sort((a, b) => b.Score.CompareTo(a.Score));

            // 동시성 제어: 한 틱에 최대 _maxExecutionsPerTick개의 입찰만 집행해
            // 팀 전체가 같은 순간에 움직이는 것을 방지한다.
            int executed = 0;
            for (int i = 0; i < _pendingBids.Count && executed < _maxExecutionsPerTick; i++)
            {
                ActionBid bid = _pendingBids[i];

                if (CurrentTeamAP >= bid.RequiredAP)
                {
                    if (TryExecuteCommand(bid.Executor, bid.Command))
                    {
                        CurrentTeamAP -= bid.RequiredAP;
                        executed++;
                    }
                }
            }
        }

        private bool TryExecuteCommand(UnitBrain actor, IActionCommand command)
        {
            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();

            if (_runtimeController == null) return false;
            
            return _runtimeController.TryReserveAndQueueAction(
                actor, 
                command, 
                null, 
                null, 
                $"AI_Action Actor={actor.ActorId:N} Action={command.GetType().Name}");
        }

        private bool IsMatchingTeam(UnitBrain unit)
        {
            return unit != null &&
                   unit.TryGetComponent(out TeamComponent teamComponent) &&
                   teamComponent.IsEnemy == _isEnemyTeam;
        }

        public UnitBrain GetAllyKing()
        {
            for (int i = 0; i < _teamMembers.Count; i++)
            {
                var unit = _teamMembers[i];
                if (unit != null && !unit.IsDead && unit.UnitData != null && unit.UnitData.PieceType == Data.ChessPieceType.King)
                {
                    return unit;
                }
            }
            return null;
        }
    }
}
