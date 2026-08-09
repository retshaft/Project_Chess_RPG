using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Data;

namespace CheckmateRPG.Progression
{
    public class DeckManager : MonoBehaviour
    {
        public static DeckManager Instance { get; private set; }

        [Header("Unit Database")]
        [Tooltip("Assign all available UnitData assets here via Inspector.")]
        public List<UnitData> AllUnitsDatabase = new List<UnitData>();

        [Header("Progression Database")]
        [Tooltip("Assign all available progression assets here.")]
        public ResonanceProfileData DefaultResonanceProfile;
        public List<NotationNodeData> AllNotationNodes = new List<NotationNodeData>();
        public SyncCapacityData DefaultSyncCapacity;
        public List<EdictData> AllEdicts = new List<EdictData>();

        [Header("Runtime Deck")]
        public UnitData[] CurrentDeck { get; private set; } = new UnitData[MAX_DECK_SIZE];

        public const int MAX_DECK_SIZE = 8;

        public event Action OnDeckChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Automatically register any Edicts placed in Resources/Edicts so the designer doesn't have to manually bind them
            var resourceEdicts = Resources.LoadAll<EdictData>("Edicts");
            foreach (var edict in resourceEdicts)
            {
                if (edict != null && !AllEdicts.Contains(edict))
                {
                    AllEdicts.Add(edict);
                }
            }

            // Automatically register any Notation nodes placed in Resources/Notations
            var resourceNotations = Resources.LoadAll<NotationNodeData>("Notations");
            foreach (var node in resourceNotations)
            {
                if (node != null && !AllNotationNodes.Contains(node))
                {
                    AllNotationNodes.Add(node);
                }
            }
        }

        private void Start()
        {
            LoadDeckFromSave();
        }

        public void LoadDeckFromSave()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return;

            var savedDeck = SaveManager.Instance.CurrentData.CurrentDeck;
            
            // 만약 저장된 덱이 비어있다면, 초기 기본 덱 설정
            if (savedDeck.Count == 0 && AllUnitsDatabase.Count > 0)
            {
                // Slot 0 is King
                var king = AllUnitsDatabase.Find(u => u != null && u.PieceType == ChessPieceType.King);
                if (king != null) {
                    CurrentDeck[0] = king;
                }
                
                // 임시: 나머지 채우기
                int slot = 1;
                for (int i = 0; i < AllUnitsDatabase.Count && slot < MAX_DECK_SIZE; i++)
                {
                    if (AllUnitsDatabase[i] != king)
                    {
                        CurrentDeck[slot] = AllUnitsDatabase[i];
                        slot++;
                        if (slot >= 3) break;
                    }
                }
                SaveDeck();
            }
            else
            {
                for (int i = 0; i < MAX_DECK_SIZE; i++)
                {
                    if (i < savedDeck.Count && !string.IsNullOrEmpty(savedDeck[i]))
                    {
                        CurrentDeck[i] = AllUnitsDatabase.Find(u => u != null && u.name == savedDeck[i]);
                    }
                    else
                    {
                        CurrentDeck[i] = null;
                    }
                }

                // King Validation
                if (CurrentDeck[0] == null || CurrentDeck[0].PieceType != ChessPieceType.King)
                {
                    var king = AllUnitsDatabase.Find(u => u != null && u.PieceType == ChessPieceType.King);
                    if (king != null) CurrentDeck[0] = king;
                }
            }

            OnDeckChanged?.Invoke();
        }

        public void SaveDeck()
        {
            if (SaveManager.Instance == null) return;

            var savedDeck = SaveManager.Instance.CurrentData.CurrentDeck;
            savedDeck.Clear();

            for (int i = 0; i < MAX_DECK_SIZE; i++)
            {
                if (CurrentDeck[i] != null)
                {
                    savedDeck.Add(CurrentDeck[i].name);
                }
                else
                {
                    savedDeck.Add("");
                }
            }

            SaveManager.Instance.SaveGame();
        }

        public bool SetDeckSlot(int slotIndex, UnitData unit)
        {
            if (slotIndex < 0 || slotIndex >= MAX_DECK_SIZE) return false;
            
            // Slot 0 constraint
            if (slotIndex == 0)
            {
                if (unit == null || unit.PieceType != ChessPieceType.King) return false;
            }
            else
            {
                // slots 1-7 cannot be King
                if (unit != null && unit.PieceType == ChessPieceType.King) return false;
            }

            // Remove unit from other slots if it already exists
            if (unit != null)
            {
                for (int i = 0; i < MAX_DECK_SIZE; i++)
                {
                    if (i != slotIndex && CurrentDeck[i] == unit)
                    {
                        CurrentDeck[i] = null;
                    }
                }
            }

            CurrentDeck[slotIndex] = unit;
            SaveDeck();
            OnDeckChanged?.Invoke();
            return true;
        }

        public bool RemoveFromDeck(int slotIndex)
        {
             if (slotIndex < 0 || slotIndex >= MAX_DECK_SIZE) return false;
             if (slotIndex == 0) return false; // Cannot leave slot 0 empty
             
             CurrentDeck[slotIndex] = null;
             SaveDeck();
             OnDeckChanged?.Invoke();
             return true;
        }

        public UnitData[] GetModifiedDeck()
        {
            UnitData[] modifiedDeck = new UnitData[MAX_DECK_SIZE];
            for (int i = 0; i < MAX_DECK_SIZE; i++)
            {
                modifiedDeck[i] = GetModifiedUnitData(i);
            }
            return modifiedDeck;
        }

        public UnitData GetModifiedUnitData(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_DECK_SIZE) return null;
            var original = CurrentDeck[slotIndex];
            if (original == null) return null;

            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return original;

            MetaProgressionLoadout loadout = null;

            if (original.PieceType == ChessPieceType.King)
            {
                var kingSave = SaveManager.Instance.CurrentData.KingProgression;
                EdictData absolute = null;
                if (!string.IsNullOrEmpty(kingSave.EquippedAbsoluteEdictId))
                    absolute = AllEdicts.Find(e => e != null && e.name == kingSave.EquippedAbsoluteEdictId);
                
                List<EdictData> generals = new List<EdictData>();
                if (kingSave.EquippedGeneralEdictIds != null)
                {
                    foreach (var id in kingSave.EquippedGeneralEdictIds)
                    {
                        var edict = AllEdicts.Find(e => e != null && e.name == id);
                        if (edict != null) generals.Add(edict);
                    }
                }
                loadout = MetaProgressionBuilder.BuildKingLoadout(kingSave, DefaultSyncCapacity, absolute, generals);
            }
            else
            {
                var unitSaves = SaveManager.Instance.CurrentData.UnitProgressions;
                UnitProgressionSaveData unitSave = unitSaves.Find(u => u.UnitId == original.name);
                
                if (unitSave == null)
                {
                    // No progression saved yet, generate a default one
                    unitSave = new UnitProgressionSaveData { UnitId = original.name, ResonanceStage = 1 };
                }

                loadout = MetaProgressionBuilder.BuildUnitLoadout(unitSave, DefaultResonanceProfile, AllNotationNodes);
            }

            return MetaProgressionCalculator.CreateModifiedUnitData(original, loadout, "_Modified");
        }

        /// <summary>
        /// 현재 장착된 칙령 중 액티브 해방 기능(HasActiveSkill)이 활성화된 칙령을 반환합니다.
        /// 장착된 액티브 칙령이 없을 경우 null을 반환하며, 전투 HUD 버튼은 활성화되지 않습니다.
        /// </summary>
        public EdictData GetEquippedActiveEdict()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null || AllEdicts == null)
                return null;

            var kingSave = SaveManager.Instance.CurrentData.KingProgression;
            if (kingSave != null && !string.IsNullOrEmpty(kingSave.EquippedAbsoluteEdictId))
            {
                var absolute = AllEdicts.Find(e => e != null && e.name == kingSave.EquippedAbsoluteEdictId);
                if (absolute != null && absolute.HasActiveSkill)
                    return absolute;
            }

            if (kingSave != null && kingSave.EquippedGeneralEdictIds != null)
            {
                foreach (var id in kingSave.EquippedGeneralEdictIds)
                {
                    var edict = AllEdicts.Find(e => e != null && e.name == id);
                    if (edict != null && edict.HasActiveSkill)
                        return edict;
                }
            }

            return null;
        }
    }
}
