using System.Collections.Generic;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;
using UnityEngine;

namespace CheckmateRPG.Units
{
    public sealed class AITeamCommander : MonoBehaviour
    {
        [SerializeField] private bool _isEnemyTeam = true;
        [SerializeField, Min(1)] private int _evaluationTickInterval = 5;
        [SerializeField] private List<UnitBrain> _teamMembers = new List<UnitBrain>();

        private readonly List<ActionBid> _pendingBids = new List<ActionBid>();
        private TickScheduler _tickScheduler;
        private int _manualTickCounter;
        private int _lastEvaluatedTick = -1;
        private ActionRuntimeController _runtimeController;

        public bool IsEnemyTeam => _isEnemyTeam;

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
            EvaluateAndExecuteTeamActions();
        }

        private void EvaluateAndExecuteTeamActions()
        {
            _pendingBids.Clear();

            for (int i = 0; i < _teamMembers.Count; i++)
            {
                UnitBrain unit = _teamMembers[i];
                if (unit == null || unit.IsDead || !IsMatchingTeam(unit))
                    continue;

                ActionBid bid = unit.GetBestActionBid();
                if (bid.IsValid)
                    _pendingBids.Add(bid);
            }

            if (_pendingBids.Count == 0)
                return;

            _pendingBids.Sort((a, b) => b.Score.CompareTo(a.Score));

            APManager apManager = APManager.Instance;
            if (apManager == null)
                return;

            float currentTeamAP = apManager.CurrentAP;

            for (int i = 0; i < _pendingBids.Count; i++)
            {
                ActionBid bid = _pendingBids[i];

                if (currentTeamAP >= bid.RequiredAP)
                {
                    if (TryExecuteCommand(bid.Command))
                        currentTeamAP -= bid.RequiredAP;
                }
            }
        }

        private bool TryExecuteCommand(IActionCommand command)
        {
            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();

            if (_runtimeController?.Scheduler == null) return false;

            ActionAdmissionResult result = _runtimeController.Scheduler.ScheduleAction(command);
            return result.Status != ActionAdmissionStatus.Rejected;
        }

        private bool IsMatchingTeam(UnitBrain unit)
        {
            return unit != null &&
                   unit.TryGetComponent(out TeamComponent teamComponent) &&
                   teamComponent.IsEnemy == _isEnemyTeam;
        }
    }
}
