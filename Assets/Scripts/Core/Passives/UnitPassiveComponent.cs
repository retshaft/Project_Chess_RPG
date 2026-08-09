using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Core.Events;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Units;
using CheckmateRPG.Components;

namespace CheckmateRPG.Core.Passives
{
    public class UnitPassiveComponent : MonoBehaviour
    {
        [Tooltip("이 유닛이 가진 패시브 정의 목록")]
        public List<PassiveDefinitionSO> Passives = new();

        private UnitBrain _brain;
        private IEventBus _eventBus;
        
        // 상태 저장용 (예: 패시브별 누적 타격 횟수)
        private readonly Dictionary<string, int> _hitCounts = new();

        private void Awake()
        {
            _brain = GetComponent<UnitBrain>();
        }

        private void Start()
        {
            _eventBus = ActionRuntimeController.Instance.EventBus;
            if (_eventBus != null)
            {
                _eventBus.Subscribe<DamageAppliedEvent>(OnDamageApplied);
            }
        }

        private void OnDestroy()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<DamageAppliedEvent>(OnDamageApplied);
            }
        }

        // ─── Trigger: OnDamageDealt ───
        private void OnDamageApplied(DamageAppliedEvent evt)
        {
            if (_brain == null || evt.Payload.SourceId != _brain.ActorId)
                return;

            foreach (var passive in Passives)
            {
                if (passive == null) continue;

                int level = _brain.UnitData != null ? _brain.UnitData.GetSkillLevel(passive.PassiveId) : 1;
                var levelData = passive.GetLevelData(level);

                if (levelData.Trigger != PassiveTriggerType.OnDamageDealt)
                    continue;

                // 타격 횟수 누적
                string key = passive.PassiveId;
                if (!_hitCounts.TryGetValue(key, out int count)) count = 0;
                count++;
                _hitCounts[key] = count;

                // 수식 평가 (Evaluator 호출)
                bool conditionMet = PassiveExpressionEvaluator.Evaluate(
                    levelData.ConditionExpression, 
                    _brain.ActorId, 
                    evt.Payload.TargetId, 
                    count);

                if (conditionMet)
                {
                    _hitCounts[key] = 0; // 조건 달성 시 초기화 (추후 룰에 따라 변경 가능)

                    int tick = ActionRuntimeController.Instance.SimulationRuntime?.CurrentTick ?? 0;
                    if (levelData.Effects != null)
                    {
                        foreach (var effect in levelData.Effects)
                        {
                            effect?.Execute(_brain.ActorId, evt.Payload.TargetId, tick);
                        }
                    }
                }
            }
        }

        // ─── Trigger: OnAttackStart ───
        public void NotifyAttackStart(Guid targetId)
        {
            foreach (var passive in Passives)
            {
                if (passive == null) continue;

                int level = _brain.UnitData != null ? _brain.UnitData.GetSkillLevel(passive.PassiveId) : 1;
                var levelData = passive.GetLevelData(level);

                if (levelData.Trigger != PassiveTriggerType.OnAttackStart)
                    continue;

                // 타격 횟수는 공격 전에는 참조하지 않음 (0 전달)
                bool conditionMet = PassiveExpressionEvaluator.Evaluate(
                    levelData.ConditionExpression, 
                    _brain.ActorId, 
                    targetId, 
                    0);

                if (conditionMet)
                {
                    int tick = ActionRuntimeController.Instance.SimulationRuntime?.CurrentTick ?? 0;
                    if (levelData.Effects != null)
                    {
                        foreach (var effect in levelData.Effects)
                        {
                            effect?.Execute(_brain.ActorId, targetId, tick);
                        }
                    }
                }
            }
        }
    }
}
