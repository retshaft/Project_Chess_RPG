using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Progression;
using CheckmateRPG.Data;

namespace CheckmateRPG.UI
{
    /// <summary>
    /// 명일방주풍 인재 채용(Headhunting / Recruit) 화면입니다.
    /// 픽업 통신 및 상시 요원 소집 채널을 제공하며, 아웃게임 스타일 토큰을 준수합니다.
    /// </summary>
    public class RecruitHubUI : OutgameViewBase
    {
        [SerializeField] private Button _backButton;
        private TextMeshProUGUI _statusFeedbackText;
        private TextMeshProUGUI _banner1DescText;
        private TextMeshProUGUI _banner2DescText;

        private void Awake()
        {
            ViewType = OutgameViewType.Recruit;
            UiFontProvider.EnsureTmpKoreanFallback();
            EnsureUI();

            if (_backButton != null)
                _backButton.onClick.AddListener(() => OutgameUIManager.Instance.GoBack());
        }

        public override void Show()
        {
            base.Show();
            EnsureUI();
            RefreshData();
        }

        private void EnsureUI()
        {
            var panel = transform.Find("RecruitContainer");
            if (panel != null) return;

            var panelGo = new GameObject("RecruitContainer", typeof(RectTransform));
            panelGo.transform.SetParent(transform, false);
            var pRt = (RectTransform)panelGo.transform;
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.sizeDelta = new Vector2(1400f, 620f);

            // 1. Featured Pickup Banner (Left Card)
            CreateBannerCard(
                panelGo.transform,
                "PickupBannerCard",
                new Vector2(-360f, 40f),
                new Vector2(640f, 480f),
                "<color=#FFB300><b>01 // FEATURED PICKUP</b></color>",
                "<b>기동 타격대 // KNIGHT TACTICAL</b>",
                "체스판의 지형과 적 기물을 도약하여 전장을 유린하는\n나이트(Knight) 계열 오퍼레이터의 소집 확률이 대폭 상승합니다.",
                "헤드헌팅 기밀 통신 가동 중\n• 대상: 체스 기동 전술 타격 부대",
                "1회 소집 가동",
                "10회 연속 소집",
                out _banner1DescText,
                () => ExecuteRecruit(1, "나이트 기동 타격대"),
                () => ExecuteRecruit(10, "나이트 기동 타격대")
            );

            // 2. Standard Banner (Right Card)
            CreateBannerCard(
                panelGo.transform,
                "StandardBannerCard",
                new Vector2(360f, 40f),
                new Vector2(640f, 480f),
                "<color=#00E5FF><b>02 // STANDARD HEADHUNTING</b></color>",
                "<b>체스 정예 요원 상시 소집</b>",
                "폰(Pawn), 룩(Rook), 비숍(Bishop) 등 전열 돌파 및 원거리 지원\n핵심 기보를 보유한 작전 대원을 통상 채널로 영입합니다.",
                "PRTS 상시 통신망 대역폭: 정상 (99.8%)\n• 전 기물 직군 표준 확률 적용",
                "상시 채용 통신",
                "10회 상시 채용",
                out _banner2DescText,
                () => ExecuteRecruit(1, "상시 인재 풀"),
                () => ExecuteRecruit(10, "상시 인재 풀")
            );

            // 3. Bottom Status Feedback Console
            var statusGo = new GameObject("StatusConsole", typeof(RectTransform), typeof(Image));
            statusGo.transform.SetParent(panelGo.transform, false);
            var sRt = (RectTransform)statusGo.transform;
            sRt.anchorMin = new Vector2(0.5f, 0f);
            sRt.anchorMax = new Vector2(0.5f, 0f);
            sRt.pivot = new Vector2(0.5f, 0f);
            sRt.anchoredPosition = new Vector2(0f, -60f);
            sRt.sizeDelta = new Vector2(1360f, 60f);
            statusGo.GetComponent<Image>().color = OutgameStyle.SurfaceDark;

            var statusTextGo = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusTextGo.transform.SetParent(statusGo.transform, false);
            var stRt = (RectTransform)statusTextGo.transform;
            stRt.anchorMin = Vector2.zero;
            stRt.anchorMax = Vector2.one;
            stRt.offsetMin = new Vector2(24f, 0f);
            stRt.offsetMax = new Vector2(-24f, 0f);
            _statusFeedbackText = statusTextGo.GetComponent<TextMeshProUGUI>();
            _statusFeedbackText.font = UiFontProvider.GetTmpFont();
            _statusFeedbackText.fontSize = 19;
            _statusFeedbackText.alignment = TextAlignmentOptions.MidlineLeft;
            _statusFeedbackText.color = OutgameStyle.AccentCyan;
            _statusFeedbackText.text = "● PRTS HEADHUNTING PROTOCOL // 암호화 통신 대기 중. 지휘관의 채용 승인을 기다립니다.";
        }

        private void CreateBannerCard(
            Transform parent,
            string cardName,
            Vector2 pos,
            Vector2 size,
            string tag,
            string title,
            string desc,
            string details,
            string btn1Label,
            string btn2Label,
            out TextMeshProUGUI descRef,
            UnityEngine.Events.UnityAction onBtn1,
            UnityEngine.Events.UnityAction onBtn2)
        {
            var cardGo = new GameObject(cardName, typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(parent, false);
            var cRt = (RectTransform)cardGo.transform;
            cRt.anchoredPosition = pos;
            cRt.sizeDelta = size;
            cardGo.GetComponent<Image>().color = OutgameStyle.SurfaceCard;

            // Header Banner
            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(cardGo.transform, false);
            var hRt = (RectTransform)headerGo.transform;
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.anchoredPosition = new Vector2(0f, -20f);
            hRt.sizeDelta = new Vector2(size.x - 48f, 70f);
            var hTmp = headerGo.GetComponent<TextMeshProUGUI>();
            hTmp.font = UiFontProvider.GetTmpFont();
            hTmp.text = $"{tag}\n<size=120%>{title}</size>";
            hTmp.fontSize = 20;
            hTmp.color = OutgameStyle.TextWhite;

            // Description Body
            var descGo = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGo.transform.SetParent(cardGo.transform, false);
            var dRt = (RectTransform)descGo.transform;
            dRt.anchorMin = new Vector2(0f, 0.5f);
            dRt.anchorMax = new Vector2(1f, 0.5f);
            dRt.pivot = new Vector2(0.5f, 0.5f);
            dRt.anchoredPosition = new Vector2(0f, 10f);
            dRt.sizeDelta = new Vector2(size.x - 48f, 180f);
            descRef = descGo.GetComponent<TextMeshProUGUI>();
            descRef.font = UiFontProvider.GetTmpFont();
            descRef.text = $"{desc}\n\n<color=#8A94A6><size=85%>{details}</size></color>";
            descRef.fontSize = 18;
            descRef.color = OutgameStyle.TextWhite;

            // Buttons Container
            var btnContainer = new GameObject("Buttons", typeof(RectTransform));
            btnContainer.transform.SetParent(cardGo.transform, false);
            var bRt = (RectTransform)btnContainer.transform;
            bRt.anchorMin = new Vector2(0f, 0f);
            bRt.anchorMax = new Vector2(1f, 0f);
            bRt.pivot = new Vector2(0.5f, 0f);
            bRt.anchoredPosition = new Vector2(0f, 24f);
            bRt.sizeDelta = new Vector2(size.x - 48f, 54f);

            var b1 = CreateCardButton(btnContainer.transform, "Btn1", btn1Label, new Vector2(-140f, 0f), new Vector2(260f, 50f), onBtn1, false);
            var b2 = CreateCardButton(btnContainer.transform, "Btn2", btn2Label, new Vector2(140f, 0f), new Vector2(260f, 50f), onBtn2, true);
        }

        private Button CreateCardButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick, bool isAccent)
        {
            var btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            var rt = (RectTransform)btnGo.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);
            var tRt = (RectTransform)txtGo.transform;
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.sizeDelta = Vector2.zero;
            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.font = UiFontProvider.GetTmpFont();
            tmp.text = label;
            tmp.fontSize = 19;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;

            var btn = btnGo.GetComponent<Button>();
            if (isAccent)
            {
                OutgameStyle.ApplyCombatButtonStyle(btn, tmp);
            }
            else
            {
                OutgameStyle.ApplyPrimaryButtonStyle(btn, tmp);
            }

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        private void ExecuteRecruit(int count, string bannerName)
        {
            if (_statusFeedbackText == null) return;

            string[] sampleOperators = new string[] { "Knight_A", "Pawn_Vanguard", "Rook_Defender", "Bishop_Tactician", "Queen_Executor" };
            string recruited = sampleOperators[Random.Range(0, sampleOperators.Length)];

            _statusFeedbackText.text = count == 1
                ? $"<color=#00E5FF>● [채용 통신 수신]</color> <color=#FFB300>{bannerName}</color> 채널에서 <b>[{recruited}]</b> 대원의 입사 계약서가 도착했습니다!"
                : $"<color=#00E5FF>● [10연속 채용 완료]</color> <color=#FFB300>{bannerName}</color> 정예 인재 10명의 통신 계약이 사령부 로스터에 등록되었습니다.";
        }

        private void RefreshData()
        {
            if (_statusFeedbackText != null)
            {
                _statusFeedbackText.text = "● PRTS HEADHUNTING PROTOCOL // 암호화 통신 대기 중. 지휘관의 채용 승인을 기다립니다.";
            }
        }
    }
}
