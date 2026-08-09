using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Effects;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public enum AbilityTargetingRule
    {
        None,
        Self,
        SingleTarget,
        MultiTarget
    }

    // AbilityTargetingRule is defined above.

    public enum AreaTargetShape
    {
        None,
        SelfAttackRange,      // 시전자의 기본 공격 범위 내 모든 적
        TargetAroundCross,    // 타겟 기준 십자형
        TargetAroundCircle,   // 타겟 기준 주변 1칸 원형
    }

    [Serializable]
    public class AbilityLevelData
    {
        [Min(0f)] public float Cost = 0f;
        [Min(0)] public int SPCost = 0;
        [Min(0)] public float InitSP = 0f;
        [Min(0)] public int Cooldown = 0;
        public CheckmateRPG.Data.SPChargeType ChargeType = CheckmateRPG.Data.SPChargeType.Auto;
        public ActionSpeedTier CastSpeed = ActionSpeedTier.Normal;
        public AbilityTargetingRule TargetingRule = AbilityTargetingRule.SingleTarget;

        [Tooltip("액티브 스킬 실행 시 발동될 이펙트 모듈들")]
        [SerializeReference]
        public List<CheckmateRPG.Core.Abilities.IAbilityEffect> Effects = new();
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Ability Definition", fileName = "NewAbilityDefinition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public string AbilityId;
        [Tooltip("UI 및 컷인에 표시될 스킬명")]
        public string SkillName = "Skill Name";
        [TextArea(2, 5)] public string Description = "Skill Description";
        public Sprite SkillIcon;
        public CheckmateRPG.Data.SPChargeType DefaultChargeType = CheckmateRPG.Data.SPChargeType.Auto;

        [Tooltip("레벨별 스킬 데이터. 인덱스 0 = 1레벨")]
        public List<AbilityLevelData> Levels = new();

        public AbilityLevelData GetLevelData(int currentLevel)
        {
            if (Levels == null || Levels.Count == 0)
                return new AbilityLevelData(); // fallback
            
            // Level 1이 인덱스 0이므로, Level - 1
            int index = Mathf.Clamp(currentLevel - 1, 0, Levels.Count - 1);
            return Levels[index];
        }
    }
}
