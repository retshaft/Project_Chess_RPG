using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Edict", fileName = "Edict")]
    public class EdictData : ScriptableObject
    {
        public string EdictName = "New Edict";
        public EdictKind Kind = EdictKind.General;
        public PolarityType Polarity = PolarityType.Neutral;

        [Min(0)]
        [Tooltip("Sync capacity consumed while this edict is active.")]
        public int SyncCost = 1;

        public List<StatModifierEntry> Bonuses = new();

        [Header("Active Edict Skill (칙령 해방 설정 - 디자이너 커스텀)")]
        [Tooltip("체크 시 전투 중 해당 칙령을 장착했을 때만 HUD에 발동 버튼이 표출됩니다. 기본 기능이 아닙니다.")]
        public bool HasActiveSkill = false;
        public string ActiveSkillName = "킹 싱크로 해방";
        [Min(1f)] public float Cooldown = 35f;
        [Min(1f)] public float Duration = 10f;
        [Min(0f)] public float InstantAPBonus = 30f;
        [Tooltip("발동 시 아군 소대 전원에게 부여할 상태이상/버프 여부")]
        public bool ApplySquadStatusEffect = true;
        public CheckmateRPG.Core.StatusEffectType SquadStatusEffect = CheckmateRPG.Core.StatusEffectType.Haste;
        [Tooltip("발동 시 서브컬처 감성의 전역 조명 연출(오버드라이브)을 가동할지 여부")]
        public bool UseOverdriveLighting = true;
    }
}
