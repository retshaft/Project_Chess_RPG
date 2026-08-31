using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Data;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    public class OperatorHubUI : OutgameViewBase
    {
        [SerializeField] private Button _backButton;
        
        private RectTransform _rootContainer;
        private RectTransform _rosterContainer;
        private RectTransform _detailsContainer;
        private RectTransform _skillsPanel;
        private RectTransform _notationPanel;
        
        private TextMeshProUGUI _unitNameText;
        private TextMeshProUGUI _unitStatsText;
        
        private List<UnitData> _availableUnits = new();
        private UnitData _selectedUnit;
        
        private void Awake()
        {
            if (_backButton != null)
                _backButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby));
                
            BuildProgrammaticUI();
            LoadUnits();
        }
        
        private void OnEnable()
        {
            if (_selectedUnit != null)
                SelectUnit(_selectedUnit);
        }

        private void BuildProgrammaticUI()
        {
            // Root Container
            _rootContainer = new GameObject("OperatorHub_Layout").AddComponent<RectTransform>();
            _rootContainer.SetParent(transform, false);
            _rootContainer.anchorMin = Vector2.zero;
            _rootContainer.anchorMax = Vector2.one;
            _rootContainer.offsetMin = new Vector2(50, 50);
            _rootContainer.offsetMax = new Vector2(-50, -50);
            
            var hl = _rootContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;
            hl.spacing = 30;

            // 1. Roster (Left)
            _rosterContainer = new GameObject("RosterPanel").AddComponent<RectTransform>();
            _rosterContainer.SetParent(_rootContainer, false);
            var le1 = _rosterContainer.gameObject.AddComponent<LayoutElement>();
            le1.preferredWidth = 300;
            var vl1 = _rosterContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vl1.spacing = 15;
            vl1.childControlHeight = false;
            vl1.childControlWidth = true;
            vl1.childForceExpandHeight = false;
            vl1.childAlignment = TextAnchor.UpperCenter;
            vl1.padding = new RectOffset(20, 20, 20, 20);

            // 2. Details (Center)
            _detailsContainer = new GameObject("DetailsPanel").AddComponent<RectTransform>();
            _detailsContainer.SetParent(_rootContainer, false);
            var le2 = _detailsContainer.gameObject.AddComponent<LayoutElement>();
            le2.preferredWidth = 500;
            var vl2 = _detailsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vl2.spacing = 30;
            vl2.padding = new RectOffset(30, 30, 30, 30);
            
            var nameGo = new GameObject("UnitNameText");
            nameGo.transform.SetParent(_detailsContainer, false);
            _unitNameText = nameGo.AddComponent<TextMeshProUGUI>();
            _unitNameText.fontSize = 56;
            _unitNameText.fontStyle = FontStyles.Bold;
            _unitNameText.color = new Color(0f, 0.898f, 1f); // Accent_Cyan
            
            var statsGo = new GameObject("StatsText");
            statsGo.transform.SetParent(_detailsContainer, false);
            _unitStatsText = statsGo.AddComponent<TextMeshProUGUI>();
            _unitStatsText.fontSize = 24;
            _unitStatsText.color = new Color(0.9f, 0.9f, 0.9f);
            
            var skillsLabelGo = new GameObject("SkillsLabel");
            skillsLabelGo.transform.SetParent(_detailsContainer, false);
            var skillsLabel = skillsLabelGo.AddComponent<TextMeshProUGUI>();
            skillsLabel.text = "// TACTICAL_SKILL_SELECT";
            skillsLabel.fontSize = 20;
            skillsLabel.color = Color.gray;

            _skillsPanel = new GameObject("SkillsPanel").AddComponent<RectTransform>();
            _skillsPanel.SetParent(_detailsContainer, false);
            var hlSkills = _skillsPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlSkills.spacing = 20;
            hlSkills.childControlWidth = true;
            hlSkills.childControlHeight = false;
            hlSkills.childForceExpandWidth = true;

            // 3. Notation (Right)
            _notationPanel = new GameObject("NotationPanel").AddComponent<RectTransform>();
            _notationPanel.SetParent(_rootContainer, false);
            var le3 = _notationPanel.gameObject.AddComponent<LayoutElement>();
            le3.preferredWidth = 600;
            var vl3 = _notationPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            vl3.spacing = 20;
            vl3.padding = new RectOffset(30, 30, 30, 30);
            vl3.childControlHeight = false;
            
            AddPanelBackground(_rosterContainer);
            AddPanelBackground(_detailsContainer);
            AddPanelBackground(_notationPanel);
        }
        
        private void AddPanelBackground(RectTransform rt)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.17f, 0.95f); // BG_Dark_Surface
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.898f, 1f, 0.3f);
            outline.effectDistance = new Vector2(2, -2);
        }

        private void LoadUnits()
        {
            _availableUnits.Clear();
            var units = Resources.LoadAll<UnitData>("Units");
            foreach (var u in units)
            {
                if (u.AIProfile == null && !u.name.Contains("Enemy") && !u.name.Contains("UnitData"))
                {
                    _availableUnits.Add(u);
                }
            }
            
            foreach (Transform child in _rosterContainer) Destroy(child.gameObject);
            
            var rosterLabelGo = new GameObject("RosterLabel");
            rosterLabelGo.transform.SetParent(_rosterContainer, false);
            var rosterLabel = rosterLabelGo.AddComponent<TextMeshProUGUI>();
            rosterLabel.text = "// OPERATOR_ROSTER";
            rosterLabel.fontSize = 20;
            rosterLabel.color = Color.gray;

            foreach (var u in _availableUnits)
            {
                var btnGo = new GameObject($"Btn_{u.name}");
                btnGo.transform.SetParent(_rosterContainer, false);
                var img = btnGo.AddComponent<Image>();
                img.color = new Color(0.18f, 0.2f, 0.25f);
                
                var btn = btnGo.AddComponent<Button>();
                btnGo.AddComponent<ButtonFeedback>(); 
                
                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(btnGo.transform, false);
                var txt = txtGo.AddComponent<TextMeshProUGUI>();
                txt.text = u.UnitName;
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = Color.white;
                txt.fontSize = 28;
                
                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;

                var le = btnGo.AddComponent<LayoutElement>();
                le.minHeight = 70;
                
                btn.onClick.AddListener(() => SelectUnit(u));
            }
            
            if (_availableUnits.Count > 0)
                SelectUnit(_availableUnits[0]);
        }

        private void SelectUnit(UnitData unit)
        {
            _selectedUnit = unit;
            _unitNameText.text = unit.UnitName;
            _unitStatsText.text = $"<color=#FFB300>Class:</color> {unit.BaseRole} | <color=#FFB300>Cost:</color> {unit.DeploymentCost} | <color=#FFB300>W:</color> {unit.Weight}\n" +
                                  $"<color=#FFB300>HP:</color> {unit.MaxHealth} | <color=#FFB300>ATK:</color> {unit.AttackDamage} | <color=#FFB300>DEF:</color> {unit.Defense} | <color=#FFB300>RES:</color> {unit.Resistance * 100}%\n" +
                                  $"<color=#FFB300>Delay:</color> {unit.ActionDelay}s | <color=#FFB300>SpeedLv:</color> {unit.SpeedLevel}";
            
            RenderSkills(unit);
            RenderNotation(unit);
        }
        
        private void RenderSkills(UnitData unit)
        {
            foreach (Transform child in _skillsPanel) Destroy(child.gameObject);
            
            int selectedIdx = UnitSkillRuntime.GetSelectedSkillIndex(unit.name);
            
            for (int i = 0; i < unit.SelectableActiveSkills.Count; i++)
            {
                int index = i;
                var skill = unit.SelectableActiveSkills[i];
                var btnGo = new GameObject($"Skill_{i}");
                btnGo.transform.SetParent(_skillsPanel, false);
                var img = btnGo.AddComponent<Image>();
                
                bool isSelected = (index == selectedIdx);
                img.color = isSelected ? new Color(0f, 0.4f, 0.5f, 1f) : new Color(0.2f, 0.2f, 0.25f, 1f);
                
                if (isSelected)
                {
                    var outline = btnGo.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0.898f, 1f, 1f); // Accent_Cyan
                    outline.effectDistance = new Vector2(3, -3);
                }
                
                var btn = btnGo.AddComponent<Button>();
                btnGo.AddComponent<ButtonFeedback>();
                
                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(btnGo.transform, false);
                var txt = txtGo.AddComponent<TextMeshProUGUI>();
                txt.text = skill != null ? skill.SkillName : "None";
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = isSelected ? new Color(0f, 0.898f, 1f) : Color.white;
                txt.fontSize = 24;
                
                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;

                var le = btnGo.AddComponent<LayoutElement>();
                le.minHeight = 120;
                le.flexibleWidth = 1;
                
                btn.onClick.AddListener(() => {
                    UnitSkillRuntime.SetSelectedSkillIndex(unit.name, index);
                    RenderSkills(unit);
                });
            }
        }
        
        private void RenderNotation(UnitData unit)
        {
            foreach (Transform child in _notationPanel) Destroy(child.gameObject);
            
            var titleGo = new GameObject("NotationTitle");
            titleGo.transform.SetParent(_notationPanel, false);
            var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
            titleTxt.fontSize = 32;
            titleTxt.color = new Color(1f, 0.7f, 0f); // Accent_Gold
            
            var tree = Resources.Load<NotationTreeData>($"NotationTrees/{unit.name}_Tree");
            if (tree == null)
            {
                titleTxt.text = "// NO_NOTATION_DATA_FOUND";
                titleTxt.color = new Color(1f, 0.2f, 0.29f); // Warning_Red
                return;
            }
            
            var state = NotationProgressState.CreateForUnit(unit.name, tree);
            titleTxt.text = $"[ {tree.NotationTitle} ]\n<size=24><color=#00E5FF>Remaining TP: {state.RemainingTP} / {state.TotalMaxTP}</color></size>";
            
            var listGo = new GameObject("NodeList");
            listGo.transform.SetParent(_notationPanel, false);
            var vl = listGo.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 15;
            vl.childControlHeight = false;
            
            var allNodes = new List<NotationNodeData>();
            if (tree.RootNode != null) allNodes.Add(tree.RootNode);
            if (tree.Nodes != null) allNodes.AddRange(tree.Nodes);

            foreach (var node in allNodes)
            {
                bool unlocked = state.IsUnlocked(node);
                bool canUnlock = state.CanUnlock(node);
                
                var btnGo = new GameObject($"Node_{node.NodeId}");
                btnGo.transform.SetParent(listGo.transform, false);
                var img = btnGo.AddComponent<Image>();
                img.color = unlocked ? new Color(1f, 0.7f, 0f, 0.5f) : (canUnlock ? new Color(0.2f, 0.3f, 0.2f) : new Color(0.15f, 0.15f, 0.15f));
                
                var btn = btnGo.AddComponent<Button>();
                if (canUnlock) btnGo.AddComponent<ButtonFeedback>();
                
                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(btnGo.transform, false);
                var txt = txtGo.AddComponent<TextMeshProUGUI>();
                txt.text = $"[{node.NotationCoord}] {node.Description} <color=#FFB300>(TP: {node.TPCost})</color>";
                txt.alignment = TextAlignmentOptions.Left;
                txt.margin = new Vector4(20, 0, 0, 0);
                txt.color = unlocked ? Color.white : (canUnlock ? Color.white : Color.gray);
                txt.fontSize = 22;
                
                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;

                var le = btnGo.AddComponent<LayoutElement>();
                le.minHeight = 70;
                
                btn.interactable = canUnlock;
                btn.onClick.AddListener(() => {
                    if (state.TryUnlock(node))
                    {
                        RenderNotation(unit); // Refresh tree UI
                    }
                });
            }
        }
    }
}
