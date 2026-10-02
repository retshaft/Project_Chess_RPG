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
        private readonly Dictionary<StageData, Button> _chartButtons = new Dictionary<StageData, Button>();

        // 확정된 공통 Outgame 스타일 팔레트
        private static readonly Color SurfaceColor = OutgameStyle.BackgroundBase;
        private static readonly Color ModalBgColor = OutgameStyle.SurfaceDark;
        private static readonly Color ModalCardColor = OutgameStyle.SurfaceCard;
        private static readonly Color AccentColor = OutgameStyle.TextWhite;
        private static readonly Color NormalBtnColor = OutgameStyle.SurfaceCard;
        private static readonly Color SelectedBtnColor = OutgameStyle.SurfaceCardLight;
        private static readonly Color DisabledBtnColor = OutgameStyle.TextDisabled;

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
            if (_availableStages == null) _availableStages = new List<StageData>();
            foreach (var stage in Resources.LoadAll<StageData>("Stages"))
            {
                if (stage != null && !_availableStages.Contains(stage)) _availableStages.Add(stage);
            }
            _availableStages.Sort((a, b) =>
            {
                bool readyA = a != null && a.TryGetReadiness(out _);
                bool readyB = b != null && b.TryGetReadiness(out _);
                int readiness = readyB.CompareTo(readyA);
                if (readiness != 0) return readiness;
                int level = (a != null ? a.RecommendedLevel : 0).CompareTo(b != null ? b.RecommendedLevel : 0);
                return level != 0 ? level : string.CompareOrdinal(a != null ? a.StageID : "", b != null ? b.StageID : "");
            });
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

                    string clearBadge = isCleared ? "<color=#C9D4D0>[클리어 완료]</color> " : "<color=#D7BB79>[작전 가능]</color> ";
                    if (!stage.TryGetReadiness(out _)) clearBadge = "<color=#AAAAAA>[준비 중]</color> ";
                    string levelBadge = stage.RecommendedLevel > 0 ? $"<size=80%><color=#B1B7B4>Lv.{stage.RecommendedLevel}</color></size> " : "";
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
            OutgameStyle.ApplyPrimaryButtonStyle(btn);
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = Color.white;
                var colors = btn.colors; colors.normalColor = isSelected ? SelectedBtnColor : NormalBtnColor; btn.colors = colors;
            }
        }

        private void OnStageSelected(StageData stage)
        {
            _selectedStage = stage;
            foreach (var item in _chartButtons) OutgameStyle.ApplySelectionStyle(item.Value,item.Key==stage);

            foreach (var kvp in _stageButtons)
            {
                UpdateStageButtonVisual(kvp.Value, kvp.Key == _selectedStage);
            }

            if (stage == null) return;

            EnsureSelfConfiguredUI();

            if (_briefingModalPanel != null)
            {
                _briefingModalPanel.SetActive(true);
                UILayers.SetOverlay(_briefingModalPanel, UILayers.Modal);
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
                string statusTag = isCleared ? "<color=#B8D1BC>[클리어]</color> " : "<color=#D7BB79>[작전 미완료]</color> ";
                string levelInfo = stage.RecommendedLevel > 0 ? $" <size=75%><color=#AAAAAA>(권장 레벨 Lv.{stage.RecommendedLevel})</color></size>" : "";
                _stageTitleText.text = $"{statusTag}<b>[{stage.StageID}]</b> {stage.StageName}{levelInfo}";
            }

            if (_objectiveText != null)
            {
                string objName = stage.Objective == ObjectiveType.Annihilation ? "섬멸전 (적 전원 격파)" : "체크메이트 (적 지휘관 킹 처치)";
                _objectiveText.text = $"• 작전 목표: <color=#D7BB79><b>{objName}</b></color>";
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
                    sb.Append("<color=#C9D4D0><b>[전술 브리핑]</b></color>\n");
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
                    _enemyIntelText.text = "• 적군 병력: <color=#AAAAAA>배치 준비 중 · 출격 불가</color>";
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
                var sb = new StringBuilder();
                if (!stage.EconomyConfigured) sb.Append("전력 소모·크레딧·기록 칩 보상 설정 필요\n");
                else
                {
                    long repeatCredits=stage.RepeatCredits>0?stage.RepeatCredits:stage.RewardCredits;
                    int repeatT1=stage.RepeatChipT1, repeatT2=stage.RepeatChipT2, repeatT3=stage.RepeatChipT3, repeatT4=stage.RepeatChipT4;
                    sb.Append($"출격 전력  {EconomyService.GetSanityCost(stage)}\n반복 크레딧  +{repeatCredits:N0}\n반복 기록 칩  T1 {repeatT1} / T2 {repeatT2} / T3 {repeatT3} / T4 {repeatT4}\n");
                    sb.Append(isCleared ? "첫 클리어 보상 지급 완료" : $"첫 클리어: 크레딧 {(stage.FirstClearCredits>0?stage.FirstClearCredits:stage.FirstClearBonusCredits):N0} / 공명석 {stage.FirstClearCrystals}");
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
                        sb.Append($"\n  - 오퍼레이터 동기화 보상: <color=#B8D1BC>{string.Join(", ", unitNames)}</color>");
                    }
                }
                _rewardText.text = sb.ToString();
            }

            bool hasContent = stage.TryGetReadiness(out string readiness);
            if (!hasContent && _enemyIntelText != null) _enemyIntelText.text += "\n" + readiness;
            if (_confirmSortieButton != null)
            {
                _confirmSortieButton.interactable = hasContent;
                var btnText = _confirmSortieButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (btnText != null)
                {
                    btnText.text = hasContent ? "작전 출격 준비" : "콘텐츠 준비 중";
                    btnText.color = OutgameStyle.Ink;
                }
                var btnImg = _confirmSortieButton.GetComponent<Image>();
                if (btnImg != null)
                {
                    OutgameStyle.ApplyCombatButtonStyle(_confirmSortieButton);
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

            bool hasContent = _selectedStage.TryGetReadiness(out _);
            if (!hasContent)
            {
                Debug.LogWarning($"[StageSelectUI] {_selectedStage.StageName} 콘텐츠가 준비되지 않아 출격할 수 없습니다.");
                return;
            }

            if (!EconomyService.CanEnter(SaveManager.EnsureInstance().CurrentData,_selectedStage,System.DateTime.UtcNow,out string reason))
            { EconomyDialog.Open(false,reason); return; }
            CloseBriefingModal();

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

            TacticalLayout.Label(transform,"OperationsTitle","작전 구역",44,OutgameStyle.TextWhite,.05f,.77f,.42f,.08f);
            TacticalLayout.Label(transform,"OperationsCode","02 / OPERATIONS  —  전술 브리핑",16,OutgameStyle.TextMuted,.052f,.845f,.5f,.035f);
            if (_stageListContainer && _stageListContainer.parent == transform)
            {
                var viewport = TacticalLayout.Panel(transform,"StageViewport",OutgameStyle.BackgroundBase,.05f,.15f,.53f,.59f);
                viewport.raycastTarget = true;
                viewport.gameObject.AddComponent<RectMask2D>();
                _stageListContainer.SetParent(viewport.transform,false);
                var content = (RectTransform)_stageListContainer;
                content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f,1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
                var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport.rectTransform; scroll.content = content;
                scroll.horizontal = false; scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped;
            }
            BuildOperationChart();
            if (_briefingModalPanel) _briefingModalPanel.transform.SetAsLastSibling();
            WireModalComponents();
        }

        private void BuildOperationChart()
        {
            // A schematic index of existing missions; connectors do not impose unlock rules.
            if (transform.Find("SectorIntel")) return;
            var chart=TacticalLayout.Panel(transform,"SectorIntel",OutgameStyle.Paper,.62f,.15f,.33f,.59f);
            TacticalLayout.Label(chart.transform,"Code","TACTICAL INDEX / 01",16,OutgameStyle.Ink,.06f,.90f,.88f,.05f);
            TacticalLayout.Label(chart.transform,"Title","정규 작전",30,OutgameStyle.Ink,.06f,.80f,.88f,.09f);
            TacticalLayout.Label(chart.transform,"Help","구역 선택 → 전술 브리핑",17,OutgameStyle.Ink,.06f,.035f,.88f,.06f);
            for(int i=1;i<8;i++)
            {
                TacticalLayout.Panel(chart.transform,"GridH"+i,new Color(0,0,0,.07f),.04f,.11f+i*.09f,.92f,.001f);
                TacticalLayout.Panel(chart.transform,"GridV"+i,new Color(0,0,0,.07f),.04f+i*.115f,.11f,.001f,.66f);
            }
            var stages=_availableStages.FindAll(s=>s!=null && !string.IsNullOrEmpty(s.StageID) && s.StageID.StartsWith("OP",System.StringComparison.Ordinal));
            stages.Sort((a,b)=>string.CompareOrdinal(a.StageID,b.StageID));
            float step=.62f/Mathf.Max(1,stages.Count);
            for(int i=0;i<stages.Count;i++)
            {
                var stage=stages[i]; float y=.72f-i*step; float x=i%2==0?.10f:.39f;
                if(i>0)
                {
                    float previousX=(i-1)%2==0?.10f:.39f;
                    TacticalLayout.Panel(chart.transform,"RouteH"+i,OutgameStyle.BorderSubtle,Mathf.Min(x,previousX)+.16f,y+step/2,.29f,.002f);
                    TacticalLayout.Panel(chart.transform,"RouteV"+i,OutgameStyle.BorderSubtle,x+.16f,y,.002f,step/2);
                    TacticalLayout.Panel(chart.transform,"RouteUpper"+i,OutgameStyle.BorderSubtle,previousX+.16f,y+step/2,.002f,step/2);
                }
                var node=TacticalLayout.Panel(chart.transform,"Node_"+stage.StageID,OutgameStyle.Ink,x,y,.46f,Mathf.Min(.09f,step*.72f));
                node.raycastTarget=true;
                var button=node.gameObject.AddComponent<Button>();button.targetGraphic=node;
                var title=TacticalLayout.Label(node.transform,"Label",stage.StageID,23,OutgameStyle.TextWhite,.12f,.1f,.78f,.8f);
                title.overflowMode=TextOverflowModes.Overflow;
                OutgameStyle.ApplyPrimaryButtonStyle(button,title);
                button.onClick.AddListener(()=>OnStageSelected(stage));
                _chartButtons[stage]=button;
            }
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
            scrollGo.GetComponent<Image>().color = OutgameStyle.SurfaceDark;

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
            OutgameStyle.ApplyPrimaryButtonStyle(btn, label);
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
