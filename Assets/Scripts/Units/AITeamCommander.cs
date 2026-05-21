using System.Collections.Generic;
using UnityEngine;
using ProjectChessRPG.Core.Actions;
using ProjectChessRPG.Core; // APManager 등 핵심 코어 네임스페이스에 맞게 수정

namespace ProjectChessRPG.Units
{
    public sealed class AITeamCommander : MonoBehaviour
    {
        [SerializeField] private Team _team = Team.Enemy;
        
        // Float 시간 대신 논리적 틱(Tick)을 사용한다. (예: 초당 10틱 시뮬레이션 시 5틱 = 0.5초)
        [SerializeField, Min(1)] private int _evaluationTickInterval = 5;
        
        [SerializeField] private List<UnitBrain> _teamMembers = new List<UnitBrain>();

        public Team Team => _team;

        // GC 방지를 위해 리스트를 미리 할당하여 재사용
        private readonly List<ActionBid> _pendingBids = new List<ActionBid>();
        private int _tickCounter = 0;
        private ActionRuntimeController _runtimeController;

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

        // Time.deltaTime 대신 논리적 프레임이 넘어갈 때마다 호출되어야 함
        public void TickCommander() 
        {
            _tickCounter++;
            if (_tickCounter < _evaluationTickInterval)
                return;
                
            _tickCounter = 0; // 주기 초기화
            EvaluateAndExecuteTeamActions();
        }

        private void EvaluateAndExecuteTeamActions()
        {
            _pendingBids.Clear();

            // 1. 모든 유닛의 입찰 수집
            for (int i = 0; i < _teamMembers.Count; i++)
            {
                UnitBrain unit = _teamMembers[i];
                if (unit == null || unit.IsDead) continue; // 추후 상태 검사 로직 추가 필요

                ActionBid bid = unit.GetBestActionBid();
                if (bid.IsValid)
                {
                    _pendingBids.Add(bid);
                }
            }

            // 입찰이 없으면 조기 종료
            if (_pendingBids.Count == 0) return;

            // 2. 점수(Score) 기반 내림차순 정렬 (구조체 리스트의 in-place 정렬로 GC 방지)
            _pendingBids.Sort((a, b) => b.Score.CompareTo(a.Score));

            // 3. AP 기반 승인 및 실행
            // 주의: APManager 구조에 맞게 변경
            float currentTeamAP = APManager.Instance.GetCurrentAP(_team);

            for (int i = 0; i < _pendingBids.Count; i++)
            {
                ActionBid bid = _pendingBids[i];

                if (currentTeamAP >= bid.RequiredAP)
                {
                    if (TryExecuteCommand(bid.Command))
                    {
                        // 시뮬레이션 파이프라인에서 AP를 차감할 것이므로, 
                        // 여기서는 로컬 검증용으로만 차감 처리
                        currentTeamAP -= bid.RequiredAP;
                    }
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
    }
}
