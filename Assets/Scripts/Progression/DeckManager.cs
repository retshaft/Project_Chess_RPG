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
        // Baseline: King(3) + Queen(5) + Rook(4) + Bishop(3) + Knight(3) + two optional Pawns(2).
        public const int MAX_FORMATION_COST = 20;
        public int FormationCost => CalculateFormationCost(CurrentDeck);
        public bool IsWithinFormationBudget => FormationCost <= MAX_FORMATION_COST;

        public static int CalculateFormationCost(IReadOnlyList<UnitData> deck, int replacementSlot = -1, UnitData replacement = null)
        {
            int total = 0;
            for (int i = 0; i < deck.Count; i++)
            {
                var unit = i == replacementSlot ? replacement : deck[i];
                // Moving an existing operator to another slot must not count it twice.
                if (i != replacementSlot && replacementSlot >= 0 && unit == replacement) continue;
                if (unit != null) total += unit.FormationCost;
            }
            return total;
        }

        public event Action OnDeckChanged;

        public static bool IsPlayerUnit(UnitData unit)
        {
            return unit != null && unit.AIProfile == null &&
                !unit.name.StartsWith("Enemy_", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAvailableToPlayer(UnitData unit, SaveData save)
        {
            if (!IsPlayerUnit(unit)) return false;
            // The king is the non-gacha player avatar. Empty inventories retain the starter prototype.
            if (unit.PieceType == ChessPieceType.King || save?.UnlockedUnits == null || save.UnlockedUnits.Count == 0)
                return true;
            if (save.UnlockedUnits.Contains(unit.name)) return true;
            if (save.CurrentDeck != null)
            {
                for (int slot = 1; slot < save.CurrentDeck.Count && slot < MAX_DECK_SIZE; slot++)
                    if (save.CurrentDeck[slot] == unit.name) return true;
            }
            return false;
        }

        public bool CanUseUnit(UnitData unit) => AllUnitsDatabase.Contains(unit) &&
            IsAvailableToPlayer(unit, SaveManager.Instance?.CurrentData);

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

            // Automatically register any Units placed in Resources/Units
            var resourceUnits = Resources.LoadAll<UnitData>("Units");
            foreach (var unit in resourceUnits)
            {
                if (unit != null && !AllUnitsDatabase.Contains(unit))
                {
                    AllUnitsDatabase.Add(unit);
                }
            }

            // The official 40-person roster is metadata-driven so new operators remain usable
            // before portrait/prefab production is complete.
            foreach (var unit in RecruitRosterCatalog.GetAllUnits())
                if (unit != null && !AllUnitsDatabase.Contains(unit)) AllUnitsDatabase.Add(unit);

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

            // 수용량 기본 데이터 자동 등록 — Resources/SyncCapacity 에셋을 사용한다(에셋 부재 시 수용량 0 함정 방지).
            if (DefaultSyncCapacity == null)
            {
                DefaultSyncCapacity = Resources.Load<SyncCapacityData>("SyncCapacity");
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
            Array.Clear(CurrentDeck, 0, CurrentDeck.Length);
            
            // 만약 저장된 덱이 비어있다면, 초기 기본 덱 설정
            if (savedDeck.Count == 0 && AllUnitsDatabase.Count > 0)
            {
                // Slot 0 is King
                var king = AllUnitsDatabase.Find(u => IsPlayerUnit(u) && u.PieceType == ChessPieceType.King);
                if (king != null) {
                    CurrentDeck[0] = king;
                }
                
                // 임시: 나머지 채우기
                int slot = 1;
                for (int i = 0; i < AllUnitsDatabase.Count && slot < MAX_DECK_SIZE; i++)
                {
                    if (CanUseUnit(AllUnitsDatabase[i]) && AllUnitsDatabase[i].PieceType != ChessPieceType.King)
                    {
                        if (CalculateFormationCost(CurrentDeck, slot, AllUnitsDatabase[i]) > MAX_FORMATION_COST) continue;
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
                        var candidate = AllUnitsDatabase.Find(u => u != null && u.name == savedDeck[i]);
                        bool correctSlot = candidate != null && ((i == 0) == (candidate.PieceType == ChessPieceType.King));
                        bool duplicate = candidate != null && Array.IndexOf(CurrentDeck, candidate, 0, i) >= 0;
                        CurrentDeck[i] = correctSlot && !duplicate && CanUseUnit(candidate) ? candidate : null;
                    }
                    else
                    {
                        CurrentDeck[i] = null;
                    }
                }

                // King Validation
                if (CurrentDeck[0] == null || CurrentDeck[0].PieceType != ChessPieceType.King)
                {
                    var king = AllUnitsDatabase.Find(u => IsPlayerUnit(u) && u.PieceType == ChessPieceType.King);
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
            if (unit != null && !CanUseUnit(unit)) return false;
            
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
            if (CalculateFormationCost(CurrentDeck, slotIndex, unit) > MAX_FORMATION_COST) return false;
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
                if (CurrentDeck[i] != null && Array.IndexOf(CurrentDeck, CurrentDeck[i], 0, i) >= 0) continue;
                modifiedDeck[i] = GetModifiedUnitData(i);
            }
            return modifiedDeck;
        }

        public UnitData GetModifiedUnitData(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_DECK_SIZE) return null;
            var original = CurrentDeck[slotIndex];
            if (original == null || !CanUseUnit(original) ||
                ((slotIndex == 0) != (original.PieceType == ChessPieceType.King))) return null;

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
                        generals.Add(edict);
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
