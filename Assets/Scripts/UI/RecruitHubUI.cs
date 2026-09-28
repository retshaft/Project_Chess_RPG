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
            TacticalLayout.Label(transform,"RecruitTitle","인재 채용",44,OutgameStyle.TextWhite,.05f,.77f,.7f,.08f);
            TacticalLayout.Label(transform,"RecruitCode","08 / HEADHUNTING  —  인재 통신망",16,OutgameStyle.TextMuted,.052f,.845f,.7f,.035f);
            var panel = transform.Find("RecruitContainer");
            if (panel != null) return;

            var panelGo = new GameObject("RecruitContainer", typeof(RectTransform));
            panelGo.transform.SetParent(transform, false);
            var pRt = (RectTransform)panelGo.transform;
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            TacticalLayout.Place(pRt,.05f,.2f,.9f,.5f);

            // 1. Featured Pickup Banner (Left Card)
            CreateBannerCard(
                panelGo.transform,
                "PickupBannerCard",
                new Vector2(-400f, 0f),
                new Vector2(760f, 510f),
                "<color=#D7BB79><b>01 // FEATURED PICKUP</b></color>",
                "<b>정예 오퍼레이터 픽업 소집</b>",
                "체스판의 지형과 적 기물을 도약하여 전장을 유린하는\n나이트(Knight) 계열 오퍼레이터를 소개하는 시험 통신입니다.",
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
                new Vector2(400f, 0f),
                new Vector2(760f, 510f),
                "<color=#C9D4D0><b>02 // STANDARD HEADHUNTING</b></color>",
                "<b>체스 정예 요원 상시 소집</b>",
                "폰(Pawn), 룩(Rook), 비숍(Bishop) 등 전열 돌파 및 원거리 지원\n핵심 기보를 보유한 작전 대원을 통상 채널로 영입합니다.",
                "PRTS 상시 통신망 / 채널 미리보기\n• 시험 통신 / 실제 대원 지급 미연결",
                "상시 채용 통신",
                "10회 상시 채용",
                out _banner2DescText,
                () => ExecuteRecruit(1, "상시 인재 풀"),
                () => ExecuteRecruit(10, "상시 인재 풀")
            );

            CreateCardButton(panelGo.transform,"ExchangePickup","픽업 증명서 300 · 확정 교환",new Vector2(-400,-220),new Vector2(600,42),()=>
            {
                var manager=SaveManager.EnsureInstance();
                var banner=System.Array.Find(Resources.LoadAll<RecruitBannerData>("RecruitBanners"),x=>x.IsPickup) ?? RecruitRosterCatalog.GetPickupBanner();
                bool ok=RecruitmentService.TryExchangePickup(manager.CurrentData,banner,System.DateTime.UtcNow,out string reason);
                if(ok)manager.SaveGame();
                _statusFeedbackText.text=ok?"픽업 대원을 영입했습니다.":reason;RefreshBannerBalances();
            },false);
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
            _statusFeedbackText.text = "HEADHUNTING / 40인 정규 소집 풀 준비 완료";
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
            hRt.sizeDelta = new Vector2(-48f, 80f);
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
            dRt.sizeDelta = new Vector2(-48f, 200f);
            descRef = descGo.GetComponent<TextMeshProUGUI>();
            descRef.font = UiFontProvider.GetTmpFont();
            descRef.text = $"{desc}\n\n<color=#B1B7B4><size=85%>{details}</size></color>";
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
            bRt.sizeDelta = new Vector2(-48f, 54f);

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
                OutgameStyle.ApplyLightButtonStyle(btn, tmp);
            }

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        private void ExecuteRecruit(int count, string bannerName)
        {
            if (_statusFeedbackText == null) return;

            var save=SaveManager.EnsureInstance();
            var banners=Resources.LoadAll<RecruitBannerData>("RecruitBanners");
            bool pickup=bannerName!="상시 인재 풀";
            var banner=System.Array.Find(banners,x=>x.IsPickup==pickup);
            if (banner==null) banner=pickup?RecruitRosterCatalog.GetPickupBanner():RecruitRosterCatalog.GetStandardBanner();
            foreach(var item in banners) RecruitmentService.SettleExpiredBanner(save.CurrentData,item,System.DateTime.UtcNow);
            if (!RecruitmentService.TryRecruit(save.CurrentData,banner,count,System.DateTime.UtcNow,()=> (double)Random.Range(0,int.MaxValue)/int.MaxValue,out var results,out string reason))
            { _statusFeedbackText.text=reason; return; }
            save.SaveGame();
            var names=new List<string>();foreach(var result in results)names.Add($"{result.Stars}★ {result.Unit.UnitName}");
            _statusFeedbackText.text=string.Join(" / ",names);
            RefreshBannerBalances();
        }

        private void RefreshBannerBalances()
        {
            var s=SaveManager.EnsureInstance().CurrentData;
            _banner1DescText.text=$"정예 허가증 {s.SpecialPermits} · 공명석 {s.ResonanceCrystals:N0}\n1회: 허가증 1장 또는 공명석 150\n10회: 허가증 우선 사용, 부족분 1회당 150\n5★ 2% / 4★ 8% / 3★ 90%\n51회부터 확률 증가 · 80회 5★ 확정\n천장 누적 {s.PityCounter} · 픽업 증명서 {s.HeadhuntingMileage}/300";
            _banner2DescText.text=$"일반 허가증 {s.StandardPermits} · 공명석 {s.ResonanceCrystals:N0}\n1회: 허가증 1장 또는 공명석 150\n10회: 4★ 이상 1명 보장\n중복: 3★ 증표 1 / 4★ 5 / 5★ 15\n증표는 해당 대원의 공명 돌파에 사용합니다.\n상시 증명서 {s.StandardHeadhuntingMileage:N0}";
        }
        private void RefreshData()
        {
            var manager=SaveManager.EnsureInstance();bool settled=false;
            foreach(var banner in Resources.LoadAll<RecruitBannerData>("RecruitBanners"))settled |= RecruitmentService.SettleExpiredBanner(manager.CurrentData,banner,System.DateTime.UtcNow);
            RecruitRosterCatalog.GetStandardBanner(); RecruitRosterCatalog.GetPickupBanner();
            if(settled)manager.SaveGame();
            RefreshBannerBalances();
            if (_statusFeedbackText != null)
            {
                _statusFeedbackText.text = "HEADHUNTING / 40인 정규 소집 풀 준비 완료";
            }
        }
    }
}
