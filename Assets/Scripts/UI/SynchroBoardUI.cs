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
            UiFontProvider.EnsureTmpKoreanFallback();
            // 씬/빌더에서 참조가 주입되지 않아도 칙령 화면이 스스로 구성되도록 한다 —
            // null 가드에 가려진 "조용한 무동작"을 제거하는 P0 수정.
            EnsureBoardUI();

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

        /// <summary>
        /// 직렬화 참조가 비어 있으면 표준 레이아웃으로 스스로 구성한다(부분 주입도 필드별로 보수한다).
        /// 선택 모달은 씬에 없으면 이 뷰 하위에 런타임 생성한다.
        /// </summary>
        private void EnsureBoardUI()
        {
            if (capacityMeterText == null)
                capacityMeterText = CreateText("CapacityMeter", new Vector2(0f, 430f), new Vector2(940f, 90f), 30);

            if (absoluteSlotButton == null)
            {
                absoluteSlotButton = CreateSlotButton("AbsoluteSlot", new Vector2(0f, 185f), new Vector2(470f, 170f),
                    out absoluteSlotText, out absoluteSlotFrame);
            }
            else
            {
                if (absoluteSlotText == null) absoluteSlotText = absoluteSlotButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (absoluteSlotFrame == null) absoluteSlotFrame = absoluteSlotButton.GetComponent<Image>();
            }

            for (int i = 0; i < generalSlotButtons.Length; i++)
            {
                if (generalSlotButtons[i] == null)
                {
                    float x = (i - 1) * 350f;
                    generalSlotButtons[i] = CreateSlotButton($"GeneralSlot{i + 1}", new Vector2(x, -40f), new Vector2(320f, 170f),
                        out generalSlotTexts[i], out generalSlotFrames[i]);
                }
                else
                {
                    if (generalSlotTexts[i] == null) generalSlotTexts[i] = generalSlotButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
                    if (generalSlotFrames[i] == null) generalSlotFrames[i] = generalSlotButtons[i].GetComponent<Image>();
                }
            }

            if (selectorModal == null)
                selectorModal = FindFirstObjectByType<EdictSelectorModal>(FindObjectsInactive.Include);
            if (selectorModal == null)
            {
                var modalGo = new GameObject("EdictSelectorModal", typeof(RectTransform));
                modalGo.transform.SetParent(transform, false);
                var modalRt = (RectTransform)modalGo.transform;
                modalRt.anchorMin = Vector2.zero;
                modalRt.anchorMax = Vector2.one;
                modalRt.sizeDelta = Vector2.zero;
                modalRt.anchoredPosition = Vector2.zero;
                selectorModal = modalGo.AddComponent<EdictSelectorModal>();
            }
            selectorModal.EnsureRuntimeUI();
        }

        private TextMeshProUGUI CreateText(string objectName, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.color = Color.white;
            return tmp;
        }

        private Button CreateSlotButton(string objectName, Vector2 position, Vector2 size, out TextMeshProUGUI label, out Image frame)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var frameImg = go.AddComponent<Image>();
            frameImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            var btn = go.AddComponent<Button>();

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(8f, 8f);
            labelRt.offsetMax = new Vector2(-8f, -8f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.color = Color.white;
            tmp.text = "비어 있음";

            label = tmp;
            frame = frameImg;
            return btn;
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
                    TryEquip(-1, edict);
                }, edict => GetSelectionDescription(-1, edict));
            }
        }

        private void OnGeneralSlotClicked(int slotIndex)
        {
            if (selectorModal != null)
            {
                selectorModal.OpenSelector(EdictKind.General, edict => {
                    TryEquip(slotIndex, edict);
                }, edict => GetSelectionDescription(slotIndex, edict));
            }
        }

        private KingProgressionSaveData KingSave => SaveManager.Instance?.CurrentData?.KingProgression;
        private SyncCapacityData CapacityData => defaultCapacityData != null ? defaultCapacityData : DeckManager.Instance?.DefaultSyncCapacity;
        private PolarityType SlotPolarity(int slot) => PolarityCalculator.GetSlotPolarity(KingSave, slot,
            CapacityData != null ? CapacityData.InnatePolarity : PolarityType.Neutral);
        private int Capacity(EdictData absolute) => PolarityCalculator.CalculateMaxSyncCapacity(
            CapacityData != null ? CapacityData.BaseCapacity : 0, Mathf.Max(0, KingSave?.PlayTP ?? 0), absolute, SlotPolarity(0));

        private bool ValidateSelection(int slot, EdictData candidate, out string reason)
        {
            // Allow repairing legacy invalid boards one general slot at a time.
            if (slot >= 0 && candidate == null) { reason = "일반 칙령을 해제합니다."; return true; }
            if (candidate != null && candidate.Kind != (slot < 0 ? EdictKind.Absolute : EdictKind.General))
            { reason = "이 슬롯에 장착할 수 없는 칙령입니다."; return false; }
            int used = 0;
            var seen = new HashSet<EdictData>();
            for (int i = 0; i < currentGeneralEdicts.Count; i++)
            {
                var edict = i == slot ? candidate : currentGeneralEdicts[i];
                if (edict == null) continue;
                if (!seen.Add(edict)) { reason = "같은 칙령을 중복 장착할 수 없습니다."; return false; }
                used += PolarityCalculator.CalculateSlotCost(edict, SlotPolarity(i + 1));
            }
            int capacity = Capacity(slot < 0 ? candidate : currentAbsoluteEdict);
            if (used > capacity) { reason = $"수용량 부족: {used} / {capacity}. 일반 칙령을 먼저 해제하세요."; return false; }
            reason = $"장착 후 수용량 {used} / {capacity}";
            return true;
        }

        private string GetSelectionDescription(int slot, EdictData candidate)
        {
            bool valid = ValidateSelection(slot, candidate, out string reason);
            string detail = slot < 0 ? $"최대 수용량 {Capacity(currentAbsoluteEdict)} → {Capacity(candidate)}" :
                $"슬롯 극성 {PolarityLabel(SlotPolarity(slot + 1))} · 비용 {PolarityCalculator.CalculateSlotCost(candidate, SlotPolarity(slot + 1))}";
            return $"{detail}\n<color={(valid ? "#83E7C3" : "#FF8888")}>{reason}</color>";
        }

        private void TryEquip(int slot, EdictData edict)
        {
            if (!ValidateSelection(slot, edict, out string reason))
            {
                if (capacityMeterText) capacityMeterText.text = $"<color=#FF8888>{reason}</color>";
                return;
            }
            if (slot < 0) currentAbsoluteEdict = edict;
            else currentGeneralEdicts[slot] = edict;
            SaveBoardState();
            UpdateBoardDisplay();
        }

        internal static string PolarityLabel(PolarityType type) => type == PolarityType.Order ? "질서" : type == PolarityType.Chaos ? "혼돈" : "중립";

        private void LoadBoardFromSave()
        {
            if (DeckManager.Instance == null) return;
            var allEdicts = DeckManager.Instance.AllEdicts;
            if (allEdicts == null) return;

            currentGeneralEdicts.Clear();
            currentAbsoluteEdict = null;
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
            int maxCapacity = Capacity(currentAbsoluteEdict);
            int totalUsed = 0;

            // 1. Render Absolute Slot (Aura)
            if (currentAbsoluteEdict != null)
            {
                if (absoluteSlotText) absoluteSlotText.text = $"<b><color=#FFDF00>{currentAbsoluteEdict.EdictName}</color></b>\n<size=80%>절대 칙령 · 슬롯 {PolarityLabel(SlotPolarity(0))} / 카드 {PolarityLabel(currentAbsoluteEdict.Polarity)}</size>";
                if (absoluteSlotFrame) absoluteSlotFrame.color = GetPolarityColor(currentAbsoluteEdict.Polarity, true);
            }
            else
            {
                if (absoluteSlotText) absoluteSlotText.text = $"<b>절대 칙령</b>\n<color=#B0B0B0><size=75%>{PolarityLabel(SlotPolarity(0))} 슬롯 · 선택하여 장착</size></color>";
                if (absoluteSlotFrame) absoluteSlotFrame.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            }

            // 2. Render General Slots (Mod Circuits)
            for (int i = 0; i < generalSlotButtons.Length; i++)
            {
                if (i < currentGeneralEdicts.Count && currentGeneralEdicts[i] != null)
                {
                    EdictData edict = currentGeneralEdicts[i];
                    int cost = PolarityCalculator.CalculateSlotCost(edict, SlotPolarity(i + 1));
                    totalUsed += cost;

                    string costHex = cost < edict.SyncCost ? "#00FF66" : (cost > edict.SyncCost ? "#FF3333" : "#FFFFFF");
                    if (i < generalSlotTexts.Length && generalSlotTexts[i] != null)
                    {
                        generalSlotTexts[i].text = $"<b>{edict.EdictName}</b>\n<size=75%>슬롯 {PolarityLabel(SlotPolarity(i + 1))} / 카드 {PolarityLabel(edict.Polarity)}</size>\n비용 <b><color={costHex}>{cost}</color></b> (기본 {edict.SyncCost})";
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
                        generalSlotTexts[i].text = $"<b>일반 칙령 {i + 1}</b>\n<color=#B0B0B0><size=75%>{PolarityLabel(SlotPolarity(i + 1))} 슬롯 · 비어 있음</size></color>";
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
                capacityMeterText.text = $"싱크로 수용량 <color={drainColor}><b>{totalUsed}</b></color> / <b>{maxCapacity}</b>\n<size=65%>슈트 기본 {CapacityData?.BaseCapacity ?? 0} + 플레이 TP {KingSave?.PlayTP ?? 0} + 절대 칙령 보너스</size>";

                bool hasEdictContent = DeckManager.Instance != null && DeckManager.Instance.AllEdicts != null && DeckManager.Instance.AllEdicts.Count > 0;
                if (!hasEdictContent)
                {
                    // 데이터가 없으면 거짓 정상 상태가 아니라 준비 중임을 그대로 표시한다.
                    capacityMeterText.text += "\n<size=60%><color=#FF8888>장착 가능한 칙령 데이터가 없습니다 — 콘텐츠 준비 중</color></size>";
                }
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
