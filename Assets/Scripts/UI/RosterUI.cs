using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Data;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    public class RosterUI : OutgameViewBase
    {
        [Header("Left Navigation & Unit List")]
        [SerializeField] private Button backToLobbyButton;
        [SerializeField] private Transform unitListContainer;
        [SerializeField] private Button unitSlotButtonPrefab;

        [Header("Center Profile & Stat Inspection")]
        [SerializeField] private TextMeshProUGUI unitNameText;
        [SerializeField] private TextMeshProUGUI pieceTypeText;
        [SerializeField] private TextMeshProUGUI statSummaryText;
        [SerializeField] private TextMeshProUGUI resonanceStageText;
        [SerializeField] private TextMeshProUGUI resonanceDiscountText;

        [Header("Progression Buttons")]
        [SerializeField] private Button enhanceResonanceButton;
        [SerializeField] private Button openNotationButton;
        [SerializeField] private TextMeshProUGUI enhanceCostText;

        [Header("Notation Puzzle Modal Overlay")]
        [SerializeField] private GameObject notationModalPanel;
        [SerializeField] private Transform notationGridContainer;
        [SerializeField] private Button closeNotationButton;
        [SerializeField] private TextMeshProUGUI tpBudgetText;

        private UnitData selectedUnit;
        private List<GameObject> activeUnitButtons = new List<GameObject>();
        private List<GameObject> activeNotationCards = new List<GameObject>();

        private void Awake()
        {
            ViewType = OutgameViewType.Roster;

            if (backToLobbyButton != null)
                backToLobbyButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby));

            if (enhanceResonanceButton != null)
                enhanceResonanceButton.onClick.AddListener(OnEnhanceResonanceClicked);

            if (openNotationButton != null)
                openNotationButton.onClick.AddListener(OnOpenNotationClicked);

            if (closeNotationButton != null)
                closeNotationButton.onClick.AddListener(CloseNotationModal);

            if (notationModalPanel != null)
                notationModalPanel.SetActive(false);
        }

        public override void Show()
        {
            base.Show();
            RefreshUnitList();
            
            if (notationModalPanel != null)
                notationModalPanel.SetActive(false);

            if (DeckManager.Instance != null && DeckManager.Instance.AllUnitsDatabase.Count > 0)
            {
                SelectUnit(DeckManager.Instance.AllUnitsDatabase[0]);
            }
            else
            {
                ClearDetails();
            }
        }

        private void RefreshUnitList()
        {
            if (unitListContainer == null || unitSlotButtonPrefab == null) return;

            foreach (var go in activeUnitButtons)
            {
                Destroy(go);
            }
            activeUnitButtons.Clear();

            if (DeckManager.Instance == null) return;

            foreach (var unit in DeckManager.Instance.AllUnitsDatabase)
            {
                if (unit == null) continue;

                var btnObj = Instantiate(unitSlotButtonPrefab, unitListContainer, false);
                btnObj.gameObject.SetActive(true);
                activeUnitButtons.Add(btnObj.gameObject);

                var tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = $"[{unit.PieceType}] {unit.UnitName}";
                }

                var button = btnObj.GetComponent<Button>();
                if (button != null)
                {
                    UnitData capturedUnit = unit;
                    button.onClick.AddListener(() => SelectUnit(capturedUnit));
                }
            }
        }

        public void SelectUnit(UnitData unit)
        {
            selectedUnit = unit;
            RefreshUnitDetails();
        }

        private void RefreshUnitDetails()
        {
            if (selectedUnit == null)
            {
                ClearDetails();
                return;
            }

            if (unitNameText != null) unitNameText.text = selectedUnit.UnitName;
            if (pieceTypeText != null) pieceTypeText.text = $"Class: <color=#00FFFF>{selectedUnit.PieceType}</color>";

            int stage = 1;
            UnitProgressionSaveData prog = GetOrCreateProgression(selectedUnit.name);
            if (prog != null) stage = prog.ResonanceStage;

            if (resonanceStageText != null)
            {
                resonanceStageText.text = $"Resonance Stage: <color=#FFD700>Lv. {stage} / 5</color>";
            }

            int enhanceCost = stage * 30;
            if (enhanceCostText != null)
            {
                enhanceCostText.text = stage >= 5 ? "MAX STAGE" : $"Breakthrough ({enhanceCost} TP)";
            }

            // AP Discount indication from ResonanceSystem
            if (resonanceDiscountText != null)
            {
                float discount = (stage >= 3) ? GetPieceAPDiscount(selectedUnit.PieceType) : 0f;
                resonanceDiscountText.text = stage >= 3 
                    ? $"<color=#00FF00>[Active Perk] Stage 3 AP Discount: -{discount} AP</color>" 
                    : "<color=#888888>[Locked] Reach Stage 3 to unlock permanent AP cost reduction.</color>";
            }

            // Calculate boosted stats using MetaProgressionCalculator
            if (statSummaryText != null)
            {
                var allNotations = DeckManager.Instance != null ? DeckManager.Instance.AllNotationNodes : new List<NotationNodeData>();
                var profile = DeckManager.Instance != null ? DeckManager.Instance.DefaultResonanceProfile : null;
                var loadout = MetaProgressionBuilder.BuildUnitLoadout(prog, profile, allNotations);
                
                UnitData modified = MetaProgressionCalculator.CreateModifiedUnitData(selectedUnit, loadout, "_preview");

                float hpDiff = modified.MaxHealth - selectedUnit.MaxHealth;
                float atkDiff = modified.AttackDamage - selectedUnit.AttackDamage;
                float spdDiff = modified.ActionSpeed - selectedUnit.ActionSpeed;
                float movDiff = modified.MoveRange - selectedUnit.MoveRange;

                string hpStr = hpDiff > 0 ? $"{selectedUnit.MaxHealth} (<color=#00FF00>+{hpDiff:0}</color>)" : $"{selectedUnit.MaxHealth}";
                string atkStr = atkDiff > 0 ? $"{selectedUnit.AttackDamage} (<color=#00FF00>+{atkDiff:0}</color>)" : $"{selectedUnit.AttackDamage}";
                string spdStr = spdDiff > 0 ? $"{selectedUnit.ActionSpeed} (<color=#00FF00>+{spdDiff:0.0}</color>)" : $"{selectedUnit.ActionSpeed}";
                string movStr = movDiff > 0 ? $"{selectedUnit.MoveRange} (<color=#00FF00>+{movDiff:0}</color>)" : $"{selectedUnit.MoveRange}";

                statSummaryText.text = 
                    $"<b>Max HP:</b> {hpStr}\n\n" +
                    $"<b>Attack Power:</b> {atkStr}\n\n" +
                    $"<b>Action Speed:</b> {spdStr}\n\n" +
                    $"<b>Movement Range:</b> {movStr}\n\n" +
                    $"<b>Move AP Cost:</b> {modified.MoveCostAP}\n\n" +
                    $"<b>Attack AP Cost:</b> {modified.AttackCostAP}";
            }
        }

        private float GetPieceAPDiscount(ChessPieceType type)
        {
            return type switch
            {
                ChessPieceType.Pawn => 1f,
                ChessPieceType.Knight => 2f,
                ChessPieceType.Bishop => 2f,
                ChessPieceType.Rook => 5f,
                ChessPieceType.Queen => 5f,
                _ => 0f
            };
        }

        private void ClearDetails()
        {
            if (unitNameText != null) unitNameText.text = "No Unit Selected";
            if (pieceTypeText != null) pieceTypeText.text = "";
            if (statSummaryText != null) statSummaryText.text = "";
            if (resonanceStageText != null) resonanceStageText.text = "";
            if (resonanceDiscountText != null) resonanceDiscountText.text = "";
        }

        private UnitProgressionSaveData GetOrCreateProgression(string unitId)
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return null;
            var data = SaveManager.Instance.CurrentData;
            var prog = data.UnitProgressions.Find(u => u.UnitId == unitId);
            if (prog == null)
            {
                prog = new UnitProgressionSaveData { UnitId = unitId, ResonanceStage = 1 };
                data.UnitProgressions.Add(prog);
                SaveManager.Instance.SaveGame();
            }
            return prog;
        }

        private void OnEnhanceResonanceClicked()
        {
            if (selectedUnit == null) return;
            var prog = GetOrCreateProgression(selectedUnit.name);
            if (prog == null) return;

            if (prog.ResonanceStage >= 5)
            {
                Debug.Log("[RosterUI] Resonance is already at maximum stage (5)!");
                return;
            }

            int cost = prog.ResonanceStage * 30;
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                // Give some free test TP if insufficient to make debugging smooth
                if (SaveManager.Instance.CurrentData.TacticalPoints < cost)
                {
                    SaveManager.Instance.CurrentData.TacticalPoints += cost;
                }
                SaveManager.Instance.CurrentData.TacticalPoints -= cost;
                prog.ResonanceStage++;
                SaveManager.Instance.SaveGame();
            }

            RefreshUnitDetails();
        }

        private void OnOpenNotationClicked()
        {
            if (selectedUnit == null) return;
            if (notationModalPanel != null)
            {
                notationModalPanel.SetActive(true);
                RefreshNotationCircuit();
            }
        }

        private void CloseNotationModal()
        {
            if (notationModalPanel != null)
                notationModalPanel.SetActive(false);
            RefreshUnitDetails();
        }

        private void RefreshNotationCircuit()
        {
            if (notationGridContainer == null || DeckManager.Instance == null) return;

            foreach (var go in activeNotationCards)
            {
                Destroy(go);
            }
            activeNotationCards.Clear();

            var prog = GetOrCreateProgression(selectedUnit != null ? selectedUnit.name : "");
            if (prog == null) return;

            int currentTP = SaveManager.Instance != null && SaveManager.Instance.CurrentData != null 
                ? SaveManager.Instance.CurrentData.TacticalPoints : 100;

            if (tpBudgetText != null)
            {
                tpBudgetText.text = $"Tactical Points Budget: <color=#00FFCC>{currentTP} TP</color>";
            }

            foreach (var node in DeckManager.Instance.AllNotationNodes)
            {
                if (node == null) continue;

                GameObject cardObj = CreateNotationCardUI(node, prog, currentTP);
                cardObj.transform.SetParent(notationGridContainer, false);
                activeNotationCards.Add(cardObj);
            }
        }

        private GameObject CreateNotationCardUI(NotationNodeData node, UnitProgressionSaveData prog, int currentTP)
        {
            GameObject card = new GameObject($"Node_{node.NodeId}");
            var img = card.AddComponent<Image>();
            var btn = card.AddComponent<Button>();

            bool isUnlocked = prog.UnlockedNodeIds.Contains(node.NodeId);
            bool prereqMet = true;
            if (node.Prerequisites != null)
            {
                foreach (var pre in node.Prerequisites)
                {
                    if (pre != null && !prog.UnlockedNodeIds.Contains(pre.NodeId))
                    {
                        prereqMet = false;
                        break;
                    }
                }
            }

            Color cardBg = isUnlocked ? new Color(0.1f, 0.35f, 0.15f, 1f) :
                           prereqMet ? new Color(0.15f, 0.2f, 0.3f, 1f) :
                                       new Color(0.18f, 0.18f, 0.18f, 0.6f);
            img.color = cardBg;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(card.transform, false);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            
            var tRect = tmp.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = new Vector2(10, 10);
            tRect.offsetMax = new Vector2(-10, -10);

            string statusStr = isUnlocked ? "<color=#00FF00>[ UNLOCKED ]</color>" :
                               prereqMet ? $"<color=#00FFFF>[ UPGRADE: {node.TPCost} TP ]</color>" :
                                           "<color=#FF6666>[ LOCKED: Prereq Needed ]</color>";

            string bonusStr = "";
            if (node.Bonuses != null)
            {
                foreach (var b in node.Bonuses)
                {
                    if (b.FlatBonus != 0) bonusStr += $"• {b.Stat}: +{b.FlatBonus}\n";
                    if (b.PercentBonus != 0) bonusStr += $"• {b.Stat}: +{b.PercentBonus * 100:0}%\n";
                }
            }

            tmp.text = $"<b><size=115%>{node.NodeId}</size></b>\n\n{bonusStr}\n\n{statusStr}";
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            btn.onClick.AddListener(() =>
            {
                if (isUnlocked) return;
                if (!prereqMet)
                {
                    Debug.Log($"[NotationCircuit] Cannot unlock {node.NodeId}: prerequisites unmet!");
                    return;
                }
                if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
                {
                    if (SaveManager.Instance.CurrentData.TacticalPoints < node.TPCost)
                    {
                        SaveManager.Instance.CurrentData.TacticalPoints += node.TPCost; // Test buffer
                    }
                    SaveManager.Instance.CurrentData.TacticalPoints -= node.TPCost;
                    prog.UnlockedNodeIds.Add(node.NodeId);
                    SaveManager.Instance.SaveGame();
                    RefreshNotationCircuit();
                }
            });

            return card;
        }
    }
}
