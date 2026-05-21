using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Units
{
    public sealed class AITeamCommander : MonoBehaviour
    {
        [SerializeField] private Team _team;
        [SerializeField, Min(0.1f)] private float _evaluationInterval = 0.5f;
        [SerializeField] private List<UnitBrain> _teamMembers = new();

        public Team Team => _team;

        private readonly List<ActionBid> _pendingBids = new();
        private float _elapsed;
        private ActionRuntimeController _runtimeController;

        private static readonly Comparison<ActionBid> BidComparison = CompareBids;

        public void RegisterUnit(UnitBrain unit)
        {
            if (unit == null || _teamMembers.Contains(unit))
                return;
            _teamMembers.Add(unit);
        }

        public void UnregisterUnit(UnitBrain unit)
        {
            if (unit == null)
                return;
            _teamMembers.Remove(unit);
        }

        public void TickCommander(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _elapsed += deltaTime;
            if (_elapsed < _evaluationInterval)
                return;
            _elapsed = 0f;

            _pendingBids.Clear();
            for (int i = 0; i < _teamMembers.Count; i++)
            {
                UnitBrain unit = _teamMembers[i];
                if (unit == null || unit.IsDead)
                    continue;
                if (unit.RuntimeState != null && unit.RuntimeState.CurrentActionId.HasValue)
                    continue;

                ActionBid bid = unit.GetBestActionBid();
                if (!bid.IsValid)
                    continue;

                _pendingBids.Add(bid);
            }

            if (_pendingBids.Count == 0)
                return;

            _pendingBids.Sort(BidComparison);

            float teamAp = GetCurrentTeamAP();
            for (int i = 0; i < _pendingBids.Count; i++)
            {
                ActionBid bid = _pendingBids[i];
                if (bid.RequiredAP > teamAp)
                    continue;

                if (!TryExecuteCommand(bid.Command))
                    continue;

                if (!TrySpendTeamAP(bid.RequiredAP))
                    continue;

                teamAp -= bid.RequiredAP;
            }
        }

        private static int CompareBids(ActionBid a, ActionBid b)
        {
            return b.Score.CompareTo(a.Score);
        }

        private bool TryExecuteCommand(IActionCommand command)
        {
            if (command == null)
                return false;

            if (_runtimeController == null)
                _runtimeController = ActionRuntimeController.EnsureExists();
            if (_runtimeController == null || _runtimeController.Scheduler == null)
                return false;

            ActionAdmissionResult result = _runtimeController.Scheduler.ScheduleAction(command);
            return result.Status != ActionAdmissionStatus.Rejected;
        }

        private static float GetCurrentTeamAP()
        {
            if (APManager.Instance == null)
                return 0f;
            return APManager.Instance.CurrentAP;
        }

        private static bool TrySpendTeamAP(float amount)
        {
            if (APManager.Instance == null)
                return false;
            return APManager.Instance.TrySpend(amount);
        }
    }
}
