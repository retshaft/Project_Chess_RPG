using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    public class StageSelectUI : OutgameViewBase
    {
        [SerializeField] private Transform _stageListContainer;
        [SerializeField] private GameObject _stageButtonPrefab;
        [SerializeField] private List<StageData> _availableStages = new List<StageData>();

        [SerializeField] private GameObject _deckBuilderPanel;

        [Header("Tactical Briefing Modal")]
        [SerializeField] private GameObject _briefingModalPanel;
        [SerializeField] private TextMeshProUGUI _stageTitleText;
        [SerializeField] private TextMeshProUGUI _objectiveText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _enemyIntelText;
        [SerializeField] private TextMeshProUGUI _rewardText;
        [SerializeField] private Button _confirmSortieButton;
        [SerializeField] private Button _closeBriefingButton;
        [SerializeField] private Button _backToLobbyButton;

        private StageData _selectedStage;

        private void Start()
        {
            if (_availableStages == null || _availableStages.Count == 0)
            {
                var stages = Resources.LoadAll<StageData>("Stages");
                if (stages != null && stages.Length > 0)
                    _availableStages.AddRange(stages);
            }

            if (_stageListContainer == null || _stageButtonPrefab == null)
            {
                Debug.LogError("[StageSelectUI] Missing references.");
                return;
            }

            if (_deckBuilderPanel == null)
            {
                var deckBuilder = FindObjectOfType<DeckBuilderUI>(true);
                if (deckBuilder != null) _deckBuilderPanel = deckBuilder.gameObject;
            }

            if (_confirmSortieButton != null) _confirmSortieButton.onClick.AddListener(OnConfirmSortieClicked);
            if (_closeBriefingButton != null) _closeBriefingButton.onClick.AddListener(CloseBriefingModal);
            if (_backToLobbyButton != null) _backToLobbyButton.onClick.AddListener(() => {
                if (OutgameUIManager.Instance != null) OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby);
            });

            if (_briefingModalPanel != null) _briefingModalPanel.SetActive(false);

            PopulateStageList();
        }

        private void PopulateStageList()
        {
            foreach (Transform child in _stageListContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (var stage in _availableStages)
            {
                if (stage == null) continue;

                var btnObj = Instantiate(_stageButtonPrefab, _stageListContainer);
                btnObj.SetActive(true); // 비활성화된 프리팹 복제 시 보이도록 활성화

                var btn = btnObj.GetComponent<Button>();
                
                var tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = $"{stage.StageID} : {stage.StageName}";
                }

                btn.onClick.AddListener(() => OnStageSelected(stage));
            }
        }

        private void OnStageSelected(StageData stage)
        {
            _selectedStage = stage;

            if (_briefingModalPanel != null)
            {
                _briefingModalPanel.SetActive(true);

                if (_stageTitleText != null) _stageTitleText.text = $"<b>[{stage.StageID}]</b> {stage.StageName}";
                if (_objectiveText != null) 
                {
                    string objName = stage.Objective == ObjectiveType.Annihilation ? "Annihilation (Defeat All Enemies)" : "Checkmate (Defeat Enemy Commander)";
                    _objectiveText.text = $"• MISSION OBJECTIVE: <color=#FF8800>{objName}</color>";
                }
                if (_descriptionText != null) _descriptionText.text = string.IsNullOrEmpty(stage.Description) ? "Standard tactical reconnaissance and perimeter cleanup operation." : stage.Description;

                if (_enemyIntelText != null)
                {
                    int totalEnemies = stage.EnemySpawns != null ? stage.EnemySpawns.Count : 0;
                    if (totalEnemies == 0)
                    {
                        _enemyIntelText.text = "• HOSTILE FORCES: <color=#AAAAAA>Undertracked Patrol Units (Default Garrison)</color>";
                    }
                    else
                    {
                        string intel = $"• HOSTILE FORCES (<color=#FF4444>Est. {totalEnemies} Units</color>):\n";
                        Dictionary<string, int> counts = new Dictionary<string, int>();
                        foreach (var spawn in stage.EnemySpawns)
                        {
                            if (spawn.EnemyUnit == null) continue;
                            string uname = spawn.EnemyUnit.UnitName;
                            if (counts.ContainsKey(uname)) counts[uname]++;
                            else counts[uname] = 1;
                        }
                        foreach (var kvp in counts)
                        {
                            intel += $"  - {kvp.Key} x{kvp.Value}\n";
                        }
                        _enemyIntelText.text = intel;
                    }
                }

                if (_rewardText != null)
                {
                    int tp = stage.RewardTP > 0 ? stage.RewardTP : 50;
                    string rewardStr = $"• REWARD FORECAST:\n  - Tactical Points: <color=#00FFFF>+{tp} TP</color>\n  - King EXP & Resonance Unlock";
                    _rewardText.text = rewardStr;
                }
            }
            else
            {
                // Fallback direct sortie if briefing modal is not wired
                OnConfirmSortieClicked();
            }
        }

        private void CloseBriefingModal()
        {
            if (_briefingModalPanel != null) _briefingModalPanel.SetActive(false);
        }

        private void OnConfirmSortieClicked()
        {
            if (_selectedStage == null) return;
            if (_briefingModalPanel != null) _briefingModalPanel.SetActive(false);

            if (StageManager.Instance != null)
            {
                StageManager.Instance.SetCurrentStage(_selectedStage);
                OutgameUIManager.Instance.ChangeView(OutgameViewType.DeckBuilder);
            }
            else
            {
                Debug.LogError("[StageSelectUI] StageManager not found! Ensure GameManagers are loaded or DontDestroyOnLoad.");
            }
        }
    }
}
