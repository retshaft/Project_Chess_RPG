using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    /// <summary>
    /// 명일방주(Arknights) 스타일의 아웃게임 공통 상단 네비게이션 헤더입니다.
    /// 좌측 뒤로가기/홈 네비게이션 및 화면 타이틀, 우측 지휘관 슈트 레벨 및 시스템 상태를 표시합니다.
    /// 게임 통화/재화가 아닌 TP를 헤더에 잘못 노출하지 않고 기획 원칙을 준수합니다.
    /// </summary>
    public class OutgameHeaderUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _homeButton;
        [SerializeField] private TextMeshProUGUI _viewTitleText;
        [SerializeField] private TextMeshProUGUI _commanderInfoText;
        [SerializeField] private TextMeshProUGUI _networkBadgeText;
        [SerializeField] private CanvasGroup _canvasGroup;

        private void Awake()
        {
            UiFontProvider.EnsureTmpKoreanFallback();
            EnsureHeaderUI();

            if (_backButton != null)
            {
                _backButton.onClick.AddListener(() =>
                {
                    if (OutgameUIManager.Instance != null)
                        OutgameUIManager.Instance.GoBack();
                });
            }

            if (_homeButton != null)
            {
                _homeButton.onClick.AddListener(() =>
                {
                    if (OutgameUIManager.Instance != null)
                        OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby);
                });
            }
        }

        private TextMeshProUGUI _sanityText, _creditText, _crystalText;
        private SaveManager _save;
        private void Start()
        {
            if (OutgameUIManager.Instance != null)
            {
                OutgameUIManager.Instance.OnViewChanged += HandleViewChanged;
                HandleViewChanged(OutgameUIManager.Instance.CurrentViewType);
            }
        }

        private void OnDestroy()
        {
            if (_save != null) _save.OnDataChanged -= RefreshProfileInfo;
            if (OutgameUIManager.Instance != null)
            {
                OutgameUIManager.Instance.OnViewChanged -= HandleViewChanged;
            }
        }

        public void HandleViewChanged(OutgameViewType viewType)
        {
            EnsureHeaderUI();
            EnsureCurrencyWidgets();

            // 타이틀 화면에서는 헤더를 숨깁니다.
            if (viewType == OutgameViewType.Title)
            {
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                    _canvasGroup.blocksRaycasts = false;
                }
                return;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
            }

            bool isLobby = (viewType == OutgameViewType.MainLobby);
            if (_backButton != null) _backButton.gameObject.SetActive(!isLobby);
            if (_homeButton != null) _homeButton.gameObject.SetActive(!isLobby);

            if (_viewTitleText != null)
            {
                _viewTitleText.text = GetViewTitle(viewType);
            }

            TacticalLayout.Place(_viewTitleText.transform,isLobby ? .035f : .16f,.12f,isLobby ? .43f : .33f,.76f);
            _viewTitleText.fontSize = 22;
            _viewTitleText.textWrappingMode = TextWrappingModes.NoWrap;
            _viewTitleText.overflowMode = TextOverflowModes.Ellipsis;
            TacticalLayout.Place(_commanderInfoText.transform,.64f,.01f,.2f,.3f);
            TacticalLayout.Place(_networkBadgeText.transform,.85f,.01f,.13f,.3f);
            _commanderInfoText.fontSize = 12;
            _networkBadgeText.fontSize = 12;
            GetComponent<Image>().color = OutgameStyle.Ink;
            OutgameStyle.ApplyPrimaryButtonStyle(_backButton);
            OutgameStyle.ApplyPrimaryButtonStyle(_homeButton);
            RefreshProfileInfo();
        }

        public void RefreshProfileInfo()
        {
            RefreshCurrency();
            int suitLevel = 1;
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                suitLevel = Mathf.Max(1, SaveManager.Instance.CurrentData.KingProgression.SuitLevel);
            }

            if (_commanderInfoText != null)
            {
                _commanderInfoText.text = $"<color=#B1B7B4>COMMANDER</color>  <b><color=#F3F2EC>SUIT LV.{suitLevel}</color></b>";
            }

            if (_networkBadgeText != null)
            {
                _networkBadgeText.text = "<color=#F3F2EC>●</color> <color=#F5F6F8>PRTS</color> <color=#B1B7B4>// ONLINE</color>";
            }
        }

        private void EnsureCurrencyWidgets()
        {
            if (_sanityText) return;
            _save=SaveManager.EnsureInstance();_save.OnDataChanged+=RefreshProfileInfo;
            _sanityText=Currency("Sanity",.53f,true,false);
            _creditText=Currency("Credit",.69f,false,false);
            _crystalText=Currency("Crystal",.85f,true,true);
        }
        private TextMeshProUGUI Currency(string name,float x,bool add,bool cores)
        {
            var panel=TacticalLayout.Panel(transform,name,OutgameStyle.SurfaceCard,x,.35f,.145f,.56f);
            var label=TacticalLayout.Label(panel.transform,"Value","",18,OutgameStyle.TextWhite,.04f,0,add?.77f:.94f,1);
            if(add)
            {
                var plus=TacticalLayout.Panel(panel.transform,"Plus",OutgameStyle.Paper,.82f,0,.18f,1);plus.raycastTarget=true;
                var button=plus.gameObject.AddComponent<Button>();button.targetGraphic=plus;button.onClick.AddListener(()=>EconomyDialog.Open(cores));
                var t=TacticalLayout.Label(plus.transform,"Label","+",23,OutgameStyle.Ink,0,0,1,1);t.alignment=TextAlignmentOptions.Center;
            }
            return label;
        }
        private void RefreshCurrency()
        {
            if(!_sanityText||_save?.CurrentData==null)return;
            var s=_save.CurrentData;
            _sanityText.text=$"전력 {s.Sanity}/{s.MaxSanity}";
            _creditText.text=$"크레딧 {s.AetherCredit:N0}";
            _crystalText.text=$"공명석 {s.ResonanceCrystals:N0}";
        }
        private string GetViewTitle(OutgameViewType viewType)
        {
            switch (viewType)
            {
                case OutgameViewType.MainLobby:
                    return "<color=#F3F2EC>CHECKMATE</color> <color=#7D8583>//</color> <color=#F5F6F8>COMMAND CENTER</color>";
                case OutgameViewType.StageSelect:
                    return "<color=#F3F2EC>02</color> <color=#7D8583>//</color> <color=#F5F6F8>OPERATIONS</color> <size=70%><color=#B1B7B4>작전 출격</color></size>";
                case OutgameViewType.OperatorHub:
                    return "<color=#F3F2EC>03</color> <color=#7D8583>//</color> <color=#F5F6F8>OPERATOR HUB</color> <size=70%><color=#B1B7B4>오퍼레이터 관리</color></size>";
                case OutgameViewType.SynchroBoard:
                    return "<color=#F3F2EC>04</color> <color=#7D8583>//</color> <color=#F5F6F8>SYNCHRO MODDING</color> <size=70%><color=#B1B7B4>킹 칙령 보드</color></size>";
                case OutgameViewType.DeckBuilder:
                    return "<color=#F3F2EC>05</color> <color=#7D8583>//</color> <color=#F5F6F8>FORMATION</color> <size=70%><color=#B1B7B4>작전 부대 편성</color></size>";
                case OutgameViewType.Recruit:
                    return "<color=#F3F2EC>08</color> <color=#7D8583>//</color> <color=#F5F6F8>HEADHUNTING</color> <size=70%><color=#B1B7B4>인재 채용</color></size>";
                case OutgameViewType.KingSuitBay:
                    return "<color=#F3F2EC>09</color> <color=#7D8583>//</color> <color=#F5F6F8>KING'S SUIT</color> <size=70%><color=#B1B7B4>슈트 격납고</color></size>";
                case OutgameViewType.Archive:
                    return "<color=#F3F2EC>10</color> <color=#7D8583>//</color> <color=#F5F6F8>ARCHIVE</color> <size=70%><color=#B1B7B4>전술 기록 보관소</color></size>";
                default:
                    return "<color=#F5F6F8>CHECKMATE TACTICAL</color>";
            }
        }

        public void EnsureHeaderUI()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0f, 75f);
            }

            var bgImg = GetComponent<Image>();
            if (bgImg == null)
            {
                bgImg = gameObject.AddComponent<Image>();
                bgImg.color = new Color(0.06f, 0.07f, 0.09f, 0.92f); // Deep dark header bar
            }

            // 하단 1px 네온 경계선 생성
            var border = transform.Find("HeaderBorder");
            if (border == null)
            {
                var borderGo = new GameObject("HeaderBorder", typeof(RectTransform), typeof(Image));
                borderGo.transform.SetParent(transform, false);
                var bRt = (RectTransform)borderGo.transform;
                bRt.anchorMin = new Vector2(0f, 0f);
                bRt.anchorMax = new Vector2(1f, 0f);
                bRt.pivot = new Vector2(0.5f, 0f);
                bRt.anchoredPosition = Vector2.zero;
                bRt.sizeDelta = new Vector2(0f, 2f);
                borderGo.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 0.8f);
            }

            // 1. 뒤로가기 버튼
            if (_backButton == null)
            {
                var backGo = transform.Find("BackButton");
                if (backGo != null) _backButton = backGo.GetComponent<Button>();
                else
                {
                    _backButton = CreateHeaderButton("BackButton", "< BACK", new Vector2(80f, -37.5f), new Vector2(110f, 45f));
                    OutgameStyle.ApplyPrimaryButtonStyle(_backButton, _backButton.GetComponentInChildren<TextMeshProUGUI>());
                }
            }

            // 2. 홈 버튼
            if (_homeButton == null)
            {
                var homeGo = transform.Find("HomeButton");
                if (homeGo != null) _homeButton = homeGo.GetComponent<Button>();
                else
                {
                    _homeButton = CreateHeaderButton("HomeButton", "HOME", new Vector2(195f, -37.5f), new Vector2(90f, 45f));
                    OutgameStyle.ApplyPrimaryButtonStyle(_homeButton, _homeButton.GetComponentInChildren<TextMeshProUGUI>());
                }
            }

            // 3. 타이틀 텍스트
            if (_viewTitleText == null)
            {
                var titleGo = transform.Find("ViewTitleText");
                if (titleGo != null) _viewTitleText = titleGo.GetComponent<TextMeshProUGUI>();
                else
                {
                    _viewTitleText = CreateHeaderText("ViewTitleText", "", 24, new Vector2(400f, -37.5f), new Vector2(600f, 50f), TextAlignmentOptions.MidlineLeft);
                }
            }

            // 4. 우측 지휘관 정보
            if (_commanderInfoText == null)
            {
                var cmdGo = transform.Find("CommanderInfoText");
                if (cmdGo != null) _commanderInfoText = cmdGo.GetComponent<TextMeshProUGUI>();
                else
                {
                    _commanderInfoText = CreateHeaderText("CommanderInfoText", "COMMANDER", 20, new Vector2(-280f, -37.5f), new Vector2(300f, 50f), TextAlignmentOptions.MidlineRight);
                }
            }

            // 5. 우측 네트워크 배지
            if (_networkBadgeText == null)
            {
                var netGo = transform.Find("NetworkBadgeText");
                if (netGo != null) _networkBadgeText = netGo.GetComponent<TextMeshProUGUI>();
                else
                {
                    _networkBadgeText = CreateHeaderText("NetworkBadgeText", "PRTS // ONLINE", 16, new Vector2(-90f, -37.5f), new Vector2(160f, 50f), TextAlignmentOptions.MidlineRight);
                }
            }
        }

        private Button CreateHeaderButton(string name, string label, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = (RectTransform)txtGo.transform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.font = UiFontProvider.GetTmpFont();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = OutgameStyle.TextWhite;

            return go.GetComponent<Button>();
        }

        private TextMeshProUGUI CreateHeaderText(string name, string text, int fontSize, Vector2 pos, Vector2 size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            if (pos.x < 0)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
            }
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = UiFontProvider.GetTmpFont();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = OutgameStyle.TextWhite;
            return tmp;
        }
    }
}
