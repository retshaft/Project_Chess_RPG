using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Core.Passives
{
    public enum PassiveTriggerType
    {
        OnAttackStart,        // 공격 직전 (버프 부여 등)
        OnDamageDealt,        // 피해를 입혔을 때 (타격 스택 등)
    }

    [Serializable]
    public class PassiveLevelData
    {
        public PassiveTriggerType Trigger;

        [Tooltip("발동 조건식 (예: 'Target.IsBleeding', 'HitCount >= 3')")]
        public string ConditionExpression;

        [Tooltip("발동 시 적용할 이펙트 모듈들")]
        [SerializeReference]
        public List<IPassiveEffect> Effects = new();
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Passive Definition")]
    public class PassiveDefinitionSO : ScriptableObject
    {
        public string PassiveId;
        
        [Tooltip("레벨별 패시브 데이터. 인덱스 0 = 1레벨")]
        public List<PassiveLevelData> Levels = new();

        public PassiveLevelData GetLevelData(int currentLevel)
        {
            if (Levels == null || Levels.Count == 0)
                return new PassiveLevelData(); // fallback
            
            int index = Mathf.Clamp(currentLevel - 1, 0, Levels.Count - 1);
            return Levels[index];
        }
    }
}
