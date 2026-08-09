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
        }

        private void OnBackToMapClicked()
        {
            OutgameUIManager.Instance.ChangeView(OutgameViewType.StageSelect);
        }

        private void OnStartBattleClicked()
        {
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
                    if (unit == null) continue;
                    
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
                }
            }

            if (_squadPowerText != null)
            {
                _squadPowerText.text = $"SQUAD COMBAT RATING: <color=#00FFCC>{totalPower:N0}</color>";
            }

            if (_activeSynchroText != null)
            {
                string synchroStatus = "Offline";
                if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
                {
                    var kProg = SaveManager.Instance.CurrentData.KingProgression;
                    synchroStatus = $"Suit Lv.{kProg.SuitLevel} | Aura: {(string.IsNullOrEmpty(kProg.EquippedAbsoluteEdictId) ? "None" : kProg.EquippedAbsoluteEdictId)}";
                }
                _activeSynchroText.text = $"• KING SYNCHRO: <color=#FFD700>{synchroStatus}</color>";
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
            _selectedSlotIndex = -1;
        }

        private void HandleInventorySlotClicked(RosterSlotUI slot)
        {
            if (_selectedSlotIndex != -1 && slot.CurrentUnit != null)
            {
                DeckManager.Instance.SetDeckSlot(_selectedSlotIndex, slot.CurrentUnit);
                CloseInventory();
            }
        }
    }
}
