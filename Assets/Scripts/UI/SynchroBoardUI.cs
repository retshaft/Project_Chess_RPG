using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    /// <summary>
    /// Warframe-styled Synchro Board (Modding Interface).
    /// Manages King's Edict circuits, displaying Capacity drain, Polarity match highlights, and calling EdictSelectorModal.
    /// </summary>
    public class SynchroBoardUI : OutgameViewBase
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI capacityMeterText;
        [SerializeField] private Button absoluteSlotButton;
        [SerializeField] private TextMeshProUGUI absoluteSlotText;
        [SerializeField] private Image absoluteSlotFrame;
        
        [SerializeField] private Button[] generalSlotButtons = new Button[3];
        [SerializeField] private TextMeshProUGUI[] generalSlotTexts = new TextMeshProUGUI[3];
        [SerializeField] private Image[] generalSlotFrames = new Image[3];
        
        [SerializeField] private Button backToLobbyButton;
        [SerializeField] private EdictSelectorModal selectorModal;

        [Header("Data References")]
        [SerializeField] private SyncCapacityData defaultCapacityData;

        private EdictData currentAbsoluteEdict;
        private readonly List<EdictData> currentGeneralEdicts = new List<EdictData>();

        private void Awake()
        {
            if (backToLobbyButton) backToLobbyButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby));
            
            if (absoluteSlotButton) absoluteSlotButton.onClick.AddListener(OnAbsoluteSlotClicked);

            for (int i = 0; i < generalSlotButtons.Length; i++)
            {
                int index = i; // Closure capture
                if (generalSlotButtons[i] != null)
                {
                    generalSlotButtons[i].onClick.AddListener(() => OnGeneralSlotClicked(index));
                }
            }
        }

        public override void Show()
        {
            base.Show();
            LoadBoardFromSave();
            UpdateBoardDisplay();
        }

        private void OnAbsoluteSlotClicked()
        {
            if (selectorModal != null)
            {
                selectorModal.OpenSelector(EdictKind.Absolute, edict => {
                    currentAbsoluteEdict = edict;
                    SaveBoardState();
                    UpdateBoardDisplay();
                });
            }
        }

        private void OnGeneralSlotClicked(int slotIndex)
        {
            if (selectorModal != null)
            {
                selectorModal.OpenSelector(EdictKind.General, edict => {
                    while (currentGeneralEdicts.Count <= slotIndex) currentGeneralEdicts.Add(null);
                    currentGeneralEdicts[slotIndex] = edict;
                    SaveBoardState();
                    UpdateBoardDisplay();
                });
            }
        }

        private void LoadBoardFromSave()
        {
            if (DeckManager.Instance == null) return;
            var allEdicts = DeckManager.Instance.AllEdicts;
            if (allEdicts == null) return;

            currentGeneralEdicts.Clear();
            while (currentGeneralEdicts.Count < generalSlotButtons.Length) currentGeneralEdicts.Add(null);

            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                var kSave = SaveManager.Instance.CurrentData.KingProgression;
                if (!string.IsNullOrEmpty(kSave.EquippedAbsoluteEdictId))
                {
                    currentAbsoluteEdict = allEdicts.Find(e => e != null && e.name == kSave.EquippedAbsoluteEdictId);
                }

                if (kSave.EquippedGeneralEdictIds != null)
                {
                    for (int i = 0; i < kSave.EquippedGeneralEdictIds.Count && i < currentGeneralEdicts.Count; i++)
                    {
                        string id = kSave.EquippedGeneralEdictIds[i];
                        if (!string.IsNullOrEmpty(id))
                        {
                            currentGeneralEdicts[i] = allEdicts.Find(e => e != null && e.name == id);
                        }
                    }
                }
            }
        }

        private void SaveBoardState()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return;
            var kSave = SaveManager.Instance.CurrentData.KingProgression;

            kSave.EquippedAbsoluteEdictId = currentAbsoluteEdict != null ? currentAbsoluteEdict.name : "";
            kSave.EquippedGeneralEdictIds.Clear();

            foreach (var ge in currentGeneralEdicts)
            {
                kSave.EquippedGeneralEdictIds.Add(ge != null ? ge.name : "");
            }

            SaveManager.Instance.SaveGame();
        }

        private void UpdateBoardDisplay()
        {
            int baseCapacity = defaultCapacityData != null ? defaultCapacityData.BaseCapacity : 20;
            PolarityType innatePolarity = defaultCapacityData != null ? defaultCapacityData.InnatePolarity : PolarityType.Order;

            int maxCapacity = PolarityCalculator.CalculateCapacity(baseCapacity, innatePolarity, currentAbsoluteEdict);
            int totalUsed = 0;

            // 1. Render Absolute Slot (Aura)
            if (currentAbsoluteEdict != null)
            {
                if (absoluteSlotText) absoluteSlotText.text = $"<b><color=#FFDF00>{currentAbsoluteEdict.EdictName}</color></b>\n<size=80%>Aura: {currentAbsoluteEdict.Polarity}</size>";
                if (absoluteSlotFrame) absoluteSlotFrame.color = GetPolarityColor(currentAbsoluteEdict.Polarity, true);
            }
            else
            {
                if (absoluteSlotText) absoluteSlotText.text = "<b>[ AURA SLOT ]</b>\n<color=#808080><size=75%>Click to Equip</size></color>";
                if (absoluteSlotFrame) absoluteSlotFrame.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            }

            // 2. Render General Slots (Mod Circuits)
            for (int i = 0; i < generalSlotButtons.Length; i++)
            {
                if (i < currentGeneralEdicts.Count && currentGeneralEdicts[i] != null)
                {
                    EdictData edict = currentGeneralEdicts[i];
                    int cost = PolarityCalculator.CalculateEdictCost(edict, currentAbsoluteEdict);
                    totalUsed += cost;

                    string costHex = cost < edict.SyncCost ? "#00FF66" : (cost > edict.SyncCost ? "#FF3333" : "#FFFFFF");
                    if (i < generalSlotTexts.Length && generalSlotTexts[i] != null)
                    {
                        generalSlotTexts[i].text = $"<b>{edict.EdictName}</b>\n<color=#A0A0A0><size=75%>{edict.Polarity}</size></color>\nDrain: <b><color={costHex}>{cost}</color></b> / {edict.SyncCost}";
                    }
                    if (i < generalSlotFrames.Length && generalSlotFrames[i] != null)
                    {
                        generalSlotFrames[i].color = GetPolarityColor(edict.Polarity, false);
                    }
                }
                else
                {
                    if (i < generalSlotTexts.Length && generalSlotTexts[i] != null)
                    {
                        generalSlotTexts[i].text = $"<b>[ MOD SLOT {i + 1} ]</b>\n<color=#606060><size=75%>Empty Circuit</size></color>";
                    }
                    if (i < generalSlotFrames.Length && generalSlotFrames[i] != null)
                    {
                        generalSlotFrames[i].color = new Color(0.18f, 0.18f, 0.22f, 1f);
                    }
                }
            }

            // 3. Render Capacity Meter
            if (capacityMeterText != null)
            {
                string drainColor = totalUsed <= maxCapacity ? "#00E5FF" : "#FF3333";
                capacityMeterText.text = $"SYNCHRO CIRCUIT CAPACITY: <color={drainColor}><b>{totalUsed}</b></color> / <b>{maxCapacity}</b>   <size=80%>(Innate Core: {innatePolarity})</size>";
            }
        }

        private Color GetPolarityColor(PolarityType type, bool isAbsolute)
        {
            float alpha = isAbsolute ? 0.95f : 0.85f;
            return type switch
            {
                PolarityType.Order => new Color(0.12f, 0.35f, 0.7f, alpha),    // Blue Order Circuit
                PolarityType.Chaos => new Color(0.7f, 0.15f, 0.18f, alpha),    // Red Chaos Circuit
                PolarityType.Neutral => new Color(0.4f, 0.4f, 0.45f, alpha),   // Steel Neutral
                _ => new Color(0.2f, 0.2f, 0.25f, alpha)
            };
        }
    }
}
