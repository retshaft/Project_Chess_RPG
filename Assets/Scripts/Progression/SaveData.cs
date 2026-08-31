using System;
using System.Collections.Generic;

namespace CheckmateRPG.Progression
{
    [Serializable]
    public class UnitProgressionSaveData
    {
        public string UnitId;
        public int ResonanceStage = 1;
        public List<string> UnlockedNodeIds = new List<string>();
        public int SelectedSkillIndex = 0;
    }

    [Serializable]
    public class KingProgressionSaveData
    {
        public int SuitLevel = 1;
        public int PlayTP = 0;
        public string EquippedAbsoluteEdictId = "";
        public List<string> EquippedGeneralEdictIds = new List<string>();
        public List<PolarityType> SlotPolarities = new List<PolarityType>();
    }

    [Serializable]
    public class SaveData
    {
        public int TacticalPoints;
        
        // 보유 기물 목록 (UnitData의 Asset Name 기준)
        public List<string> UnlockedUnits = new List<string>();
        
        // 현재 출전 엔트리 (최대 8명, UnitData의 Asset Name 기준)
        public List<string> CurrentDeck = new List<string>();

        // 개별 유닛 육성 현황
        public List<UnitProgressionSaveData> UnitProgressions = new List<UnitProgressionSaveData>();

        // 킹 전용 육성 및 칙령 장착 현황
        public KingProgressionSaveData KingProgression = new KingProgressionSaveData();

        public SaveData()
        {
            TacticalPoints = 0;
        }
    }
}
