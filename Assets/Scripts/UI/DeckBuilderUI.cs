using TMPro;
using UnityEngine;
using System.Collections.Generic;
using CheckmateRPG.Progression;
using CheckmateRPG.Data;

namespace CheckmateRPG.UI
{
    public class DeckBuilderUI : OutgameViewBase
    {
        [SerializeField] private Transform _rosterContainer;
        [SerializeField] private Transform _inventoryContainer;
        [SerializeField] private GameObject _inventoryPanel; // Reference to the full-screen inventory popup
        [SerializeField] private RosterSlotUI _rosterSlotPrefab;
        [SerializeField] private RosterSlotUI _inventorySlotPrefab;

        [SerializeField] private UnityEngine.UI.Button _startBattleButton;
        [SerializeField] private UnityEngine.UI.Button _backToMapButton;
        [SerializeField] private UnityEngine.UI.Button _closeInventoryButton;
        [SerializeField] private GameObject _stageSelectPanel;

        [Header("Squad Combat Rating & Synchro Banner")]
        [SerializeField] private TextMeshProUGUI _squadPowerText;
        [SerializeField] private TextMeshProUGUI _activeSynchroText;

        private List<RosterSlotUI> _rosterSlots = new List<RosterSlotUI>();
        private List<RosterSlotUI> _inventorySlots = new List<RosterSlotUI>();

        private int _selectedSlotIndex = -1;
        private UnityEngine.UI.Button _removeUnitButton;

        private void Start()
        {
            if (DeckManager.Instance != null)
            {
                DeckManager.Instance.OnDeckChanged += RefreshUI;
                InitializeUI();
                RefreshUI();
            }
            else
            {
                Debug.LogWarning("[DeckBuilderUI] DeckManager instance not found!");
            }

            if (_startBattleButton != null)
            {
                _startBattleButton.onClick.AddListener(OnStartBattleClicked);
            }

            if (_backToMapButton != null)
            {
                _backToMapButton.onClick.AddListener(OnBackToMapClicked);
            }

            if (_closeInventoryButton != null)
            {
                _closeInventoryButton.onClick.AddListener(CloseInventory);
            }

            if (_stageSelectPanel == null)
            {
                var stageSelect = FindObjectOfType<StageSelectUI>(true);
                if (stageSelect != null) _stageSelectPanel = stageSelect.gameObject;
            }

            if (_inventoryPanel != null)
            {
                _inventoryPanel.SetActive(false); // Hide inventory by default
            }

            EnsureDeckBuilderUI();
        }

        public override void Show()
        {
            base.Show();
            EnsureDeckBuilderUI();
            RefreshUI();
        }

        private void EnsureDeckBuilderUI()
        {
            TacticalLayout.Label(transform,"FormationTitle","작전 부대 편성",44,OutgameStyle.TextWhite,.05f,.77f,.7f,.08f);
            TacticalLayout.Label(transform,"FormationCode","05 / FORMATION  —  출전 엔트리",16,OutgameStyle.TextMuted,.052f,.845f,.7f,.035f);
            TacticalLayout.Label(transform,"FormationHelp","슬롯을 선택해 대원을 교체하세요.  킹은 부대에 반드시 포함됩니다.",20,OutgameStyle.TextMuted,.05f,.19f,.7f,.045f);
            TacticalLayout.Place(_rosterContainer,.05f,.28f,.90f,.36f);
            var layout = _rosterContainer ? _rosterContainer.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>() : null;
            if (layout) { layout.spacing = 14; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = true; }
            if (_startBattleButton) { TacticalLayout.Place(_startBattleButton.transform,.76f,.085f,.19f,.075f); _startBattleButton.GetComponentInChildren<TextMeshProUGUI>().text = "출격 확정  /  SORTIE"; }
            if (_closeInventoryButton) OutgameStyle.ApplyLightButtonStyle(_closeInventoryButton);

            if (_startBattleButton != null)
            {
                OutgameStyle.ApplyCombatButtonStyle(_startBattleButton, _startBattleButton.GetComponentInChildren<TextMeshProUGUI>());
            }

            if (_backToMapButton != null)
            {
                OutgameStyle.ApplyPrimaryButtonStyle(_backToMapButton, _backToMapButton.GetComponentInChildren<TextMeshProUGUI>());
            }

            if (_closeInventoryButton != null)
            {
                OutgameStyle.ApplyPrimaryButtonStyle(_closeInventoryButton, _closeInventoryButton.GetComponentInChildren<TextMeshProUGUI>());
            }

            if (_squadPowerText == null)
            {
                var bannerGo = new GameObject("SquadRatingBanner", typeof(RectTransform));
                bannerGo.transform.SetParent(transform, false);
                var bRt = (RectTransform)bannerGo.transform;
                bRt.anchorMin = new Vector2(0.5f, 0.5f);
                bRt.anchorMax = new Vector2(0.5f, 0.5f);
                bRt.pivot = new Vector2(0.5f, 0.5f);
                bRt.anchoredPosition = new Vector2(0f, 205f);
                bRt.sizeDelta = new Vector2(1200f, 60f);

                var powerGo = new GameObject("SquadPowerText", typeof(RectTransform), typeof(TextMeshProUGUI));
                powerGo.transform.SetParent(bannerGo.transform, false);
                var pRt = (RectTransform)powerGo.transform;
                pRt.anchorMin = new Vector2(0f, 0f);
                pRt.anchorMax = new Vector2(0.5f, 1f);
                pRt.sizeDelta = Vector2.zero;
                _squadPowerText = powerGo.GetComponent<TextMeshProUGUI>();
                _squadPowerText.font = UiFontProvider.GetTmpFont();
                _squadPowerText.fontSize = 24;
                _squadPowerText.fontStyle = FontStyles.Bold;
                _squadPowerText.alignment = TextAlignmentOptions.MidlineLeft;
                _squadPowerText.color = OutgameStyle.TextWhite;

                var synchroGo = new GameObject("ActiveSynchroText", typeof(RectTransform), typeof(TextMeshProUGUI));
                synchroGo.transform.SetParent(bannerGo.transform, false);
                var sRt = (RectTransform)synchroGo.transform;
                sRt.anchorMin = new Vector2(0.5f, 0f);
                sRt.anchorMax = new Vector2(1f, 1f);
                sRt.sizeDelta = Vector2.zero;
                _activeSynchroText = synchroGo.GetComponent<TextMeshProUGUI>();
                _activeSynchroText.font = UiFontProvider.GetTmpFont();
                _activeSynchroText.fontSize = 20;
                _activeSynchroText.alignment = TextAlignmentOptions.MidlineRight;
                _activeSynchroText.color = OutgameStyle.AccentGold;
            }
            TacticalLayout.Place(transform.Find("SquadRatingBanner"),.05f,.675f,.90f,.05f);
            EnsureInventoryLayout();
            if (_inventoryPanel) _inventoryPanel.transform.SetAsLastSibling();
        }

        private void EnsureInventoryLayout()
        {
            if (!_inventoryPanel || !_inventoryContainer) return;
            UILayers.SetOverlay(_inventoryPanel, UILayers.Modal);
            if (_inventoryPanel.TryGetComponent<UnityEngine.UI.Image>(out var background)) background.color = OutgameStyle.BackgroundBase;
            TacticalLayout.Place(_closeInventoryButton ? _closeInventoryButton.transform : null, .76f, .07f, .16f, .065f);
            if (_closeInventoryButton) _closeInventoryButton.GetComponentInChildren<TextMeshProUGUI>().text = "닫기";
            if (_inventoryPanel.transform.Find("CandidateScroll")) return;
            var viewport = TacticalLayout.Panel(_inventoryPanel.transform, "CandidateScroll", OutgameStyle.SurfaceDark, .08f, .19f, .84f, .57f);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            _inventoryContainer.SetParent(viewport.transform, false);
            var content = (RectTransform)_inventoryContainer;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var grid = _inventoryContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (grid)
            {
                grid.cellSize = new Vector2(220, 300); grid.spacing = new Vector2(18, 18);
                grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 6;
                grid.childAlignment = TextAnchor.UpperLeft; grid.padding = new RectOffset(18, 18, 18, 18);
            }
            var fit = _inventoryContainer.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 36;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        }

        private void OnBackToMapClicked()
        {
            OutgameUIManager.Instance.ChangeView(OutgameViewType.StageSelect);
        }

        private void OnStartBattleClicked()
        {
            if (DeckManager.Instance == null || !DeckManager.Instance.IsWithinFormationBudget)
            { EconomyDialog.Open(false, $"편성 코스트가 {DeckManager.MAX_FORMATION_COST}을 초과합니다. 편성에서 대원을 해제하거나 교체하세요."); return; }
            var save=SaveManager.EnsureInstance();
            var stage=StageManager.Instance?.CurrentStage;
            if (stage == null) { EconomyDialog.Open(false, "작전 지도에서 출격할 작전을 먼저 선택하세요."); return; }
            if (!stage.TryGetReadiness(out string readiness)) { EconomyDialog.Open(false, readiness); return; }
            if(!EconomyService.TryBeginSortie(save.CurrentData,stage,System.DateTime.UtcNow,out string reason))
            { EconomyDialog.Open(false,reason); return; }
            save.SaveGame();
            // 전투 준비가 완료되었으므로 BattleScene 로드
            UnityEngine.SceneManagement.SceneManager.LoadScene("BattleScene");
        }

        private void OnDestroy()
        {
            if (DeckManager.Instance != null)
            {
                DeckManager.Instance.OnDeckChanged -= RefreshUI;
            }
        }

        private void InitializeUI()
        {
            if (_rosterSlotPrefab == null)
            {
                var found = transform.Find("RosterSlotPrefab") ?? transform.Find("RosterGrid/RosterSlotPrefab");
                if (found != null) _rosterSlotPrefab = found.GetComponent<RosterSlotUI>();
            }
            if (_inventorySlotPrefab == null)
            {
                var found = transform.Find("InvSlotPrefab") ?? transform.Find("InventoryModalPanel/InvSlotPrefab");
                if (found != null) _inventorySlotPrefab = found.GetComponent<RosterSlotUI>();
            }

            if (_rosterSlotPrefab == null || _rosterContainer == null)
            {
                Debug.LogWarning("[DeckBuilderUI] Missing Roster references. Skipping Roster initialization. Please run Tools > Build Outgame UI Layouts.");
                return;
            }

            if (_rosterSlotPrefab != null) _rosterSlotPrefab.gameObject.SetActive(false);
            if (_inventorySlotPrefab != null) _inventorySlotPrefab.gameObject.SetActive(false);

            // 출전 엔트리 슬롯 초기화
            for (int i = 0; i < DeckManager.MAX_DECK_SIZE; i++)
            {
                var slot = Instantiate(_rosterSlotPrefab, _rosterContainer, false);
                slot.transform.localScale = Vector3.one;
                slot.gameObject.SetActive(true);
                slot.SetSlotIndex(i);
                slot.OnActionClicked += HandleRosterSlotClicked;
                _rosterSlots.Add(slot);
            }

            // 인벤토리 (전체 기물 목록) 슬롯 초기화
            if (_inventorySlotPrefab != null && _inventoryContainer != null)
            {
                foreach (var unit in DeckManager.Instance.AllUnitsDatabase)
                {
                    if (!DeckManager.Instance.CanUseUnit(unit)) continue;
                    
                    var slot = Instantiate(_inventorySlotPrefab, _inventoryContainer, false);
                    slot.transform.localScale = Vector3.one;
                    slot.gameObject.SetActive(true);
                    slot.SetUnit(unit);
                    slot.OnActionClicked += HandleInventorySlotClicked; 
                    _inventorySlots.Add(slot);
                }
            }
        }

        private void RefreshUI()
        {
            var currentDeck = DeckManager.Instance.CurrentDeck;
            int formationCost = 0;
            int memberCount = 0;
            foreach (var unit in currentDeck)
            {
                if (unit == null) continue;
                formationCost += unit.FormationCost;
                memberCount++;
            }
            TacticalLayout.Label(transform, "FormationCost",
                $"편성 코스트  <b>{formationCost} / {DeckManager.MAX_FORMATION_COST}</b>     ·     편성 인원  {memberCount} / {DeckManager.MAX_DECK_SIZE}",
                22, OutgameStyle.TextWhite, .05f, .73f, .90f, .035f);
            
            for (int i = 0; i < _rosterSlots.Count; i++)
            {
                if (i < currentDeck.Length)
                {
                    _rosterSlots[i].SetUnit(currentDeck[i]);
                }
            }

            int totalPower = 0;
            var modifiedDeck = DeckManager.Instance.GetModifiedDeck();
            for (int i = 0; i < modifiedDeck.Length; i++)
            {
                var mUnit = modifiedDeck[i];
                if (mUnit != null)
                {
                    int unitScore = Mathf.RoundToInt(mUnit.MaxHealth + (mUnit.AttackDamage * 10f) + (mUnit.ActionSpeed * 10f));
                    totalPower += unitScore;
                    if (mUnit != DeckManager.Instance.CurrentDeck[i]) Destroy(mUnit);
                }
            }

            if (_squadPowerText != null)
            {
                _squadPowerText.text = $"SQUAD COMBAT RATING: <color=#B8D1BC>{totalPower:N0}</color>";
            }

            if (_activeSynchroText != null)
            {
                string synchroStatus = "Offline";
                if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
                {
                    var kProg = SaveManager.Instance.CurrentData.KingProgression;
                    synchroStatus = $"Suit Lv.{kProg.SuitLevel} | Aura: {(string.IsNullOrEmpty(kProg.EquippedAbsoluteEdictId) ? "None" : kProg.EquippedAbsoluteEdictId)}";
                }
                _activeSynchroText.text = $"• KING SYNCHRO: <color=#D7BB79>{synchroStatus}</color>";
            }
        }

        private void HandleRosterSlotClicked(RosterSlotUI slot)
        {
            _selectedSlotIndex = slot.SlotIndex;
            OpenInventoryForSlot(_selectedSlotIndex);
        }

        private void OpenInventoryForSlot(int slotIndex)
        {
            if (_inventoryPanel != null)
            {
                _inventoryPanel.SetActive(true);
                UILayers.SetOverlay(_inventoryPanel, UILayers.Modal);
                OutgameUIManager.Instance?.RegisterModalCloseAction(CloseInventory);
                TacticalLayout.Label(_inventoryPanel.transform, "SelectionContext",
                    slotIndex == 0 ? "지휘관 슬롯  /  킹은 편성에서 제외할 수 없습니다." : $"슬롯 {slotIndex + 1:00}  /  편성 중인 대원을 선택하면 이 슬롯으로 이동합니다.",
                    20, OutgameStyle.TextWhite, .08f, .82f, .84f, .05f);
                if (_removeUnitButton == null)
                {
                    var panel = TacticalLayout.Panel(_inventoryPanel.transform, "RemoveUnit", OutgameStyle.SurfaceCard, .08f, .07f, .23f, .065f);
                    panel.raycastTarget = true;
                    _removeUnitButton = panel.gameObject.AddComponent<UnityEngine.UI.Button>();
                    _removeUnitButton.targetGraphic = panel;
                    TacticalLayout.Label(panel.transform, "Text", "편성 해제", 22, OutgameStyle.TextWhite, .08f, .1f, .84f, .8f).alignment = TextAlignmentOptions.Center;
                    _removeUnitButton.onClick.AddListener(() =>
                    {
                        if (DeckManager.Instance.RemoveFromDeck(_selectedSlotIndex)) CloseInventory();
                    });
                }
                _removeUnitButton.gameObject.SetActive(slotIndex > 0);
                _removeUnitButton.interactable = slotIndex > 0 && DeckManager.Instance.CurrentDeck[slotIndex] != null;
            }

            // Filter inventory slots based on the selected slot index
            foreach (var invSlot in _inventorySlots)
            {
                if (invSlot.CurrentUnit == null) continue;

                bool isKing = invSlot.CurrentUnit.PieceType == ChessPieceType.King;
                
                if (slotIndex == 0)
                {
                    // Slot 0 only allows Kings
                    invSlot.gameObject.SetActive(isKing);
                }
                else
                {
                    // Other slots do not allow Kings
                    invSlot.gameObject.SetActive(!isKing);
                }
            }
        }

        private void CloseInventory()
        {
            if (_inventoryPanel != null)
            {
                _inventoryPanel.SetActive(false);
            }
            OutgameUIManager.Instance?.UnregisterModalCloseAction(CloseInventory);
            _selectedSlotIndex = -1;
        }

        private void HandleInventorySlotClicked(RosterSlotUI slot)
        {
            if (_selectedSlotIndex != -1 && slot.CurrentUnit != null)
            {
                if (!DeckManager.Instance.SetDeckSlot(_selectedSlotIndex, slot.CurrentUnit))
                { EconomyDialog.Open(false, $"편성할 수 없습니다. 편성 코스트 {DeckManager.MAX_FORMATION_COST} 제한과 기물 슬롯을 확인하세요."); return; }
                CloseInventory();
            }
        }
    }
}
