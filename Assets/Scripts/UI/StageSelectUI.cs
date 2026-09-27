using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
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
        private readonly Dictionary<StageData, Button> _stageButtons = new Dictionary<StageData, Button>();

        // 확정된 공통 Outgame 스타일 팔레트
        private static readonly Color SurfaceColor = new Color(0.04f, 0.055f, 0.085f, 1f);
        private static readonly Color ModalBgColor = new Color(0.025f, 0.038f, 0.06f, 0.96f);
        private static readonly Color ModalCardColor = new Color(0.065f, 0.095f, 0.14f, 1f);
        private static readonly Color AccentColor = new Color(0.23f, 0.86f, 0.87f, 1f);
        private static readonly Color NormalBtnColor = new Color(0.1f, 0.17f, 0.23f, 1f);
        private static readonly Color SelectedBtnColor = new Color(0.14f, 0.38f, 0.44f, 1f);
        private static readonly Color DisabledBtnColor = new Color(0.16f, 0.19f, 0.22f, 0.65f);

        private void Awake()
        {
            ViewType = OutgameViewType.StageSelect;
            UiFontProvider.EnsureTmpKoreanFallback();
        }

        private void Start()
        {
            LoadStagesIfEmpty();

            if (_deckBuilderPanel == null)
            {
                var deckBuilder = FindFirstObjectByType<DeckBuilderUI>(FindObjectsInactive.Include);
                if (deckBuilder != null) _deckBuilderPanel = deckBuilder.gameObject;
            }

            if (_confirmSortieButton != null) _confirmSortieButton.onClick.AddListener(OnConfirmSortieClicked);
            if (_closeBriefingButton != null) _closeBriefingButton.onClick.AddListener(CloseBriefingModal);
            if (_backToLobbyButton != null) _backToLobbyButton.onClick.AddListener(() => {
                if (OutgameUIManager.Instance != null) OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby);
            });

            EnsureSelfConfiguredUI();

            if (_briefingModalPanel != null) _briefingModalPanel.SetActive(false);

            RefreshUI();
        }

        public override void Show()
        {
            base.Show();
            LoadStagesIfEmpty();
            EnsureSelfConfiguredUI();
            RefreshUI();
        }

        private void LoadStagesIfEmpty()
        {
            if (_availableStages == null || _availableStages.Count == 0)
            {
                var stages = Resources.LoadAll<StageData>("Stages");
                if (stages != null && stages.Length > 0)
                {
                    if (_availableStages == null) _availableStages = new List<StageData>();
                    _availableStages.AddRange(stages);
                }
            }
        }

        public void RefreshUI()
        {
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData == null)
            {
                SaveManager.Instance.LoadGame();
            }

            PopulateStageList();

            if (_selectedStage != null && _briefingModalPanel != null && _briefingModalPanel.activeSelf)
            {
                UpdateBriefingContent(_selectedStage);
            }
        }

        private void PopulateStageList()
        {
            if (_stageListContainer == null) return;

            foreach (Transform child in _stageListContainer)
            {
                if (_stageButtonPrefab != null && child.gameObject == _stageButtonPrefab) continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            _stageButtons.Clear();

            var saveData = SaveManager.Instance != null ? SaveManager.Instance.CurrentData : null;

            int validStageCount = 0;
            if (_availableStages != null)
            {
                foreach (var stage in _availableStages)
                {
                    if (stage == null) continue;
                    validStageCount++;

                    GameObject btnObj;
                    if (_stageButtonPrefab != null)
                    {
                        btnObj = Instantiate(_stageButtonPrefab, _stageListContainer);
                    }
                    else
                    {
                        btnObj = CreateDefaultStageListItem(_stageListContainer);
                    }
                    btnObj.SetActive(true);

                    // 버튼 높이 및 레이아웃 제어 (텍스트 겹침 완전 방지)
                    var le = btnObj.GetComponent<LayoutElement>();
                    if (le == null) le = btnObj.AddComponent<LayoutElement>();
                    le.preferredHeight = 84f;
                    le.minHeight = 80f;
                    le.flexibleWidth = 1f;

                    var btnRt = btnObj.GetComponent<RectTransform>();
                    if (btnRt != null)
                    {
                        btnRt.sizeDelta = new Vector2(880f, 84f);
                    }

                    var btn = btnObj.GetComponent<Button>();
                    if (btn == null)
                    {
                        btn = btnObj.AddComponent<Button>();
                    }

                    string stageKey = !string.IsNullOrEmpty(stage.StageID) ? stage.StageID : stage.name;
                    bool isCleared = saveData != null && saveData.ClearedStages != null &&
                                     (saveData.ClearedStages.Contains(stageKey) || saveData.ClearedStages.Contains(stage.name));

                    string clearBadge = isCleared ? "<color=#00E5FF>[클리어 완료]</color> " : "<color=#FFB300>[작전 가능]</color> ";
                    string levelBadge = stage.RecommendedLevel > 0 ? $"<size=80%><color=#8A94A6>Lv.{stage.RecommendedLevel}</color></size> " : "";
                    string titleText = $"{clearBadge}{levelBadge}<b>{stage.StageID}</b> : {stage.StageName}";

                    var tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = titleText;
                        tmp.font = UiFontProvider.GetTmpFont();
                        tmp.fontSize = 20;
                        tmp.alignment = TextAlignmentOptions.MidlineLeft;
                        var tRt = tmp.rectTransform;
                        tRt.anchorMin = Vector2.zero;
                        tRt.anchorMax = Vector2.one;
                        tRt.offsetMin = new Vector2(24f, 0f);
                        tRt.offsetMax = new Vector2(-24f, 0f);
                    }

                    var capturedStage = stage;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnStageSelected(capturedStage));

                    _stageButtons[stage] = btn;
                    UpdateStageButtonVisual(btn, stage == _selectedStage);
                }
            }

            var containerVlg = _stageListContainer.GetComponent<VerticalLayoutGroup>();
            if (containerVlg != null)
            {
                containerVlg.childControlHeight = true;
                containerVlg.childControlWidth = true;
                containerVlg.childForceExpandHeight = false;
                containerVlg.spacing = 14f;
            }

            if (validStageCount == 0)
            {
                var emptyNoticeObj = new GameObject("EmptyStageNotice", typeof(RectTransform));
                emptyNoticeObj.transform.SetParent(_stageListContainer, false);
                var emptyTmp = emptyNoticeObj.AddComponent<TextMeshProUGUI>();
                emptyTmp.font = UiFontProvider.GetTmpFont();
                emptyTmp.text = "이용 가능한 작전 스테이지가 없습니다.\n<size=80%><color=#AAAAAA>스테이지 콘텐츠를 불러올 수 없습니다.</color></size>";
                emptyTmp.fontSize = 24;
                emptyTmp.alignment = TextAlignmentOptions.Center;
                emptyTmp.color = new Color(0.75f, 0.82f, 0.88f, 1f);
                var le = emptyNoticeObj.AddComponent<LayoutElement>();
                le.preferredHeight = 120;
            }
        }

        private void UpdateStageButtonVisual(Button btn, bool isSelected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = isSelected ? SelectedBtnColor : NormalBtnColor;
            }
        }

        private void OnStageSelected(StageData stage)
        {
            _selectedStage = stage;

            foreach (var kvp in _stageButtons)
            {
                UpdateStageButtonVisual(kvp.Value, kvp.Key == _selectedStage);
            }

            if (stage == null) return;

            EnsureSelfConfiguredUI();

            if (_briefingModalPanel != null)
            {
                _briefingModalPanel.SetActive(true);
                UpdateBriefingContent(stage);
                OutgameUIManager.Instance?.RegisterModalCloseAction(CloseBriefingModal);
            }
            else
            {
                OnConfirmSortieClicked();
            }
        }

        private void UpdateBriefingContent(StageData stage)
        {
            var saveData = SaveManager.Instance != null ? SaveManager.Instance.CurrentData : null;
            string stageKey = !string.IsNullOrEmpty(stage.StageID) ? stage.StageID : stage.name;
            bool isCleared = saveData != null && saveData.ClearedStages != null &&
                             (saveData.ClearedStages.Contains(stageKey) || saveData.ClearedStages.Contains(stage.name));

            if (_stageTitleText != null)
            {
                string statusTag = isCleared ? "<color=#00FFCC>[클리어]</color> " : "<color=#FFD700>[작전 미완료]</color> ";
                string levelInfo = stage.RecommendedLevel > 0 ? $" <size=75%><color=#AAAAAA>(권장 레벨 Lv.{stage.RecommendedLevel})</color></size>" : "";
                _stageTitleText.text = $"{statusTag}<b>[{stage.StageID}]</b> {stage.StageName}{levelInfo}";
            }

            if (_objectiveText != null)
            {
                string objName = stage.Objective == ObjectiveType.Annihilation ? "섬멸전 (적 전원 격파)" : "체크메이트 (적 지휘관 킹 처치)";
                _objectiveText.text = $"• 작전 목표: <color=#FF8800><b>{objName}</b></color>";
            }

            if (_descriptionText != null)
            {
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(stage.Description))
                {
                    sb.AppendLine(stage.Description);
                }
                if (!string.IsNullOrEmpty(stage.TacticalBriefing))
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append("<color=#66CCFF><b>[전술 브리핑]</b></color>\n");
                    sb.Append(stage.TacticalBriefing);
                }
                if (sb.Length == 0)
                {
                    sb.Append("작전 구역 정찰 및 표준 경계선 수색 임무입니다.");
                }
                _descriptionText.text = sb.ToString();
            }

            if (_enemyIntelText != null)
            {
                int totalEnemies = stage.EnemySpawns != null ? stage.EnemySpawns.Count : 0;
                if (totalEnemies == 0)
                {
                    _enemyIntelText.text = "• 적군 병력: <color=#AAAAAA>추적된 적 정보 없음 (기본 주둔군 배치)</color>";
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.Append($"• 적군 병력 (<color=#FF5555>총 {totalEnemies}개체 확인</color>):\n");
                    var counts = new Dictionary<string, int>();
                    foreach (var spawn in stage.EnemySpawns)
                    {
                        if (spawn.EnemyUnit == null) continue;
                        string uname = !string.IsNullOrEmpty(spawn.EnemyUnit.UnitName) ? spawn.EnemyUnit.UnitName : spawn.EnemyUnit.name;
                        if (counts.ContainsKey(uname)) counts[uname]++;
                        else counts[uname] = 1;
                    }
                    foreach (var kvp in counts)
                    {
                        sb.Append($"  - {kvp.Key} x{kvp.Value}\n");
                    }
                    _enemyIntelText.text = sb.ToString().TrimEnd('\n');
                }
            }

            if (_rewardText != null)
            {
                int repeatTP = stage.RewardTP > 0 ? stage.RewardTP : 50;
                int firstBonusTP = Mathf.Max(0, stage.FirstClearBonusTP);

                var sb = new StringBuilder();
                sb.Append("• 보상 안내:\n");

                if (isCleared)
                {
                    sb.Append($"  - 작전 반복 보상: <color=#00FFFF>+{repeatTP} TP</color> (반복 지급)\n");
                    sb.Append("  - <color=#888888>첫 클리어 보너스: 지급 완료</color>");
                }
                else
                {
                    int totalFirstTP = repeatTP + firstBonusTP;
                    sb.Append($"  - 첫 클리어 지급: <color=#FFD700><b>+{totalFirstTP} TP</b></color>");
                    if (firstBonusTP > 0)
                    {
                        sb.Append($" (기본 {repeatTP} + 첫 클리어 보너스 {firstBonusTP})");
                    }
                    sb.Append("\n");
                    sb.Append($"  - 반복 클리어 시: <color=#00FFFF>+{repeatTP} TP</color>");
                }

                if (stage.ClearRewards != null && stage.ClearRewards.Count > 0)
                {
                    var unitNames = new List<string>();
                    foreach (var u in stage.ClearRewards)
                    {
                        if (u != null) unitNames.Add(u.UnitName);
                    }
                    if (unitNames.Count > 0)
                    {
                        sb.Append($"\n  - 오퍼레이터 동기화 보상: <color=#00FFCC>{string.Join(", ", unitNames)}</color>");
                    }
                }
                _rewardText.text = sb.ToString();
            }

            bool hasContent = stage.EnemySpawns != null && stage.EnemySpawns.Count > 0;
            if (_confirmSortieButton != null)
            {
                _confirmSortieButton.interactable = hasContent;
                var btnText = _confirmSortieButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (btnText != null)
                {
                    btnText.text = hasContent ? "작전 출격 준비" : "콘텐츠 준비 중";
                    btnText.color = hasContent ? AccentColor : new Color(0.6f, 0.6f, 0.6f, 1f);
                }
                var btnImg = _confirmSortieButton.GetComponent<Image>();
                if (btnImg != null)
                {
                    btnImg.color = hasContent ? NormalBtnColor : DisabledBtnColor;
                }
            }
        }

        public void CloseBriefingModal()
        {
            if (_briefingModalPanel != null)
            {
                _briefingModalPanel.SetActive(false);
            }
            OutgameUIManager.Instance?.UnregisterModalCloseAction(CloseBriefingModal);
        }

        private void OnConfirmSortieClicked()
        {
            if (_selectedStage == null) return;

            bool hasContent = _selectedStage.EnemySpawns != null && _selectedStage.EnemySpawns.Count > 0;
            if (!hasContent)
            {
                Debug.LogWarning($"[StageSelectUI] {_selectedStage.StageName} 콘텐츠가 준비되지 않아 출격할 수 없습니다.");
                return;
            }

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

        private void EnsureSelfConfiguredUI()
        {
            if (_briefingModalPanel == null)
            {
                var existingModal = transform.Find("TacticalBriefingModal");
                if (existingModal != null)
                {
                    _briefingModalPanel = existingModal.gameObject;
                }
                else
                {
                    BuildRuntimeBriefingModal();
                }
            }

            WireModalComponents();
        }

        private void WireModalComponents()
        {
            if (_briefingModalPanel == null) return;

            if (_stageTitleText == null)
            {
                var t = _briefingModalPanel.transform.Find("Card/Header/TitleText");
                if (t != null) _stageTitleText = t.GetComponent<TextMeshProUGUI>();
            }
            if (_objectiveText == null)
            {
                var t = _briefingModalPanel.transform.Find("Card/Scroll/Content/ObjectiveText");
                if (t != null) _objectiveText = t.GetComponent<TextMeshProUGUI>();
            }
            if (_descriptionText == null)
            {
                var t = _briefingModalPanel.transform.Find("Card/Scroll/Content/DescriptionText");
                if (t != null) _descriptionText = t.GetComponent<TextMeshProUGUI>();
            }
            if (_enemyIntelText == null)
            {
                var t = _briefingModalPanel.transform.Find("Card/Scroll/Content/EnemyIntelText");
                if (t != null) _enemyIntelText = t.GetComponent<TextMeshProUGUI>();
            }
            if (_rewardText == null)
            {
                var t = _briefingModalPanel.transform.Find("Card/Scroll/Content/RewardText");
                if (t != null) _rewardText = t.GetComponent<TextMeshProUGUI>();
            }

            if (_confirmSortieButton == null)
            {
                var b = _briefingModalPanel.transform.Find("Card/Footer/SortieButton");
                if (b != null)
                {
                    _confirmSortieButton = b.GetComponent<Button>();
                    _confirmSortieButton.onClick.RemoveAllListeners();
                    _confirmSortieButton.onClick.AddListener(OnConfirmSortieClicked);
                }
            }
            if (_closeBriefingButton == null)
            {
                var b = _briefingModalPanel.transform.Find("Card/Footer/CloseButton");
                if (b != null)
                {
                    _closeBriefingButton = b.GetComponent<Button>();
                    _closeBriefingButton.onClick.RemoveAllListeners();
                    _closeBriefingButton.onClick.AddListener(CloseBriefingModal);
                }
            }
        }

        private void BuildRuntimeBriefingModal()
        {
            var modalGo = new GameObject("TacticalBriefingModal", typeof(RectTransform), typeof(Image));
            modalGo.transform.SetParent(transform, false);
            var modalRt = (RectTransform)modalGo.transform;
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.sizeDelta = Vector2.zero;
            modalGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(modalGo.transform, false);
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(880f, 660f);
            cardGo.GetComponent<Image>().color = ModalBgColor;

            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(Image));
            headerGo.transform.SetParent(cardGo.transform, false);
            var headerRt = (RectTransform)headerGo.transform;
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 70f);
            headerGo.GetComponent<Image>().color = ModalCardColor;

            _stageTitleText = CreateTmpLabel(headerGo.transform, "TitleText", "작전 브리핑", 26, TextAlignmentOptions.MidlineLeft);
            var titleRt = _stageTitleText.rectTransform;
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(24f, 0f);
            titleRt.offsetMax = new Vector2(-24f, 0f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(cardGo.transform, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(24f, 85f);
            scrollRt.offsetMax = new Vector2(-24f, -80f);
            scrollGo.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f, 0.8f);

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 35f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 14f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRt;
            scrollRect.viewport = scrollRt;

            _objectiveText = CreateTmpLabel(contentGo.transform, "ObjectiveText", "• 작전 목표", 22, TextAlignmentOptions.TopLeft);
            _descriptionText = CreateTmpLabel(contentGo.transform, "DescriptionText", "작전 설명", 20, TextAlignmentOptions.TopLeft);
            _enemyIntelText = CreateTmpLabel(contentGo.transform, "EnemyIntelText", "• 적 정보", 20, TextAlignmentOptions.TopLeft);
            _rewardText = CreateTmpLabel(contentGo.transform, "RewardText", "• 보상 안내", 20, TextAlignmentOptions.TopLeft);

            var footerGo = new GameObject("Footer", typeof(RectTransform));
            footerGo.transform.SetParent(cardGo.transform, false);
            var footerRt = (RectTransform)footerGo.transform;
            footerRt.anchorMin = new Vector2(0f, 0f);
            footerRt.anchorMax = new Vector2(1f, 0f);
            footerRt.pivot = new Vector2(0.5f, 0f);
            footerRt.sizeDelta = new Vector2(0f, 75f);

            _closeBriefingButton = CreateStyledButton(footerGo.transform, "CloseButton", "닫기", new Vector2(180f, 52f), CloseBriefingModal);
            var closeRt = (RectTransform)_closeBriefingButton.transform;
            closeRt.anchorMin = new Vector2(0.5f, 0.5f);
            closeRt.anchorMax = new Vector2(0.5f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-120f, 0f);

            _confirmSortieButton = CreateStyledButton(footerGo.transform, "SortieButton", "작전 출격 준비", new Vector2(260f, 52f), OnConfirmSortieClicked);
            var sortieRt = (RectTransform)_confirmSortieButton.transform;
            sortieRt.anchorMin = new Vector2(0.5f, 0.5f);
            sortieRt.anchorMax = new Vector2(0.5f, 0.5f);
            sortieRt.anchoredPosition = new Vector2(140f, 0f);

            _briefingModalPanel = modalGo;
        }

        private TextMeshProUGUI CreateTmpLabel(Transform parent, string name, string text, int fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = UiFontProvider.GetTmpFont();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = new Color(0.92f, 0.96f, 0.98f, 1f);
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Button CreateStyledButton(Transform parent, string name, string text, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = NormalBtnColor;

            var btn = go.GetComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);

            var label = CreateTmpLabel(go.transform, "Text", text, 20, TextAlignmentOptions.Center);
            label.color = AccentColor;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.sizeDelta = Vector2.zero;

            return btn;
        }

        private GameObject CreateDefaultStageListItem(Transform parent)
        {
            var btnGo = new GameObject("StageItem", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGo.transform.SetParent(parent, false);
            var rt = (RectTransform)btnGo.transform;
            rt.sizeDelta = new Vector2(800f, 72f);
            btnGo.GetComponent<Image>().color = NormalBtnColor;
            var le = btnGo.GetComponent<LayoutElement>();
            le.preferredHeight = 72f;
            le.flexibleWidth = 1f;

            var label = CreateTmpLabel(btnGo.transform, "Text", "Stage", 22, TextAlignmentOptions.MidlineLeft);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(24f, 0f);
            labelRt.offsetMax = new Vector2(-24f, 0f);

            return btnGo;
        }
    }
}
