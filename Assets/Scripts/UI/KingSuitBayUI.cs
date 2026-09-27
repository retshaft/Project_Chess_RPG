using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    /// <summary>
    /// 명일방주풍 킹 슈트 격납고(King Suit Bay) 화면입니다.
    /// 플레이어 아바타(킹)의 슈트 레벨 및 장착된 칙령 상태를 요약 표시합니다.
    /// </summary>
    public class KingSuitBayUI : OutgameViewBase
    {
        [SerializeField] private Button _backButton;
        private TextMeshProUGUI _suitSpecText;

        private void Awake()
        {
            ViewType = OutgameViewType.KingSuitBay;
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
            var panel = transform.Find("BayContainer");
            if (panel == null)
            {
                var panelGo = new GameObject("BayContainer", typeof(RectTransform), typeof(Image));
                panelGo.transform.SetParent(transform, false);
                var pRt = (RectTransform)panelGo.transform;
                pRt.anchorMin = new Vector2(0.5f, 0.5f);
                pRt.anchorMax = new Vector2(0.5f, 0.5f);
                pRt.sizeDelta = new Vector2(900f, 500f);
                panelGo.GetComponent<Image>().color = OutgameStyle.SurfaceCard;

                var textGo = new GameObject("SpecText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(panelGo.transform, false);
                var tRt = (RectTransform)textGo.transform;
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = new Vector2(32f, 90f);
                tRt.offsetMax = new Vector2(-32f, -32f);
                _suitSpecText = textGo.GetComponent<TextMeshProUGUI>();
                _suitSpecText.font = UiFontProvider.GetTmpFont();
                _suitSpecText.fontSize = 22;
                _suitSpecText.color = OutgameStyle.TextWhite;

                var btnGo = new GameObject("GoToSynchroButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(panelGo.transform, false);
                var bRt = (RectTransform)btnGo.transform;
                bRt.anchorMin = new Vector2(0.5f, 0f);
                bRt.anchorMax = new Vector2(0.5f, 0f);
                bRt.pivot = new Vector2(0.5f, 0f);
                bRt.anchoredPosition = new Vector2(0f, 24f);
                bRt.sizeDelta = new Vector2(360f, 60f);

                var btnTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                btnTextGo.transform.SetParent(btnGo.transform, false);
                var btRt = (RectTransform)btnTextGo.transform;
                btRt.anchorMin = Vector2.zero;
                btRt.anchorMax = Vector2.one;
                btRt.sizeDelta = Vector2.zero;
                var btTmp = btnTextGo.GetComponent<TextMeshProUGUI>();
                btTmp.font = UiFontProvider.GetTmpFont();
                btTmp.text = "칙령 회로(Synchro) 모딩 열기";
                btTmp.fontSize = 20;
                btTmp.fontStyle = FontStyles.Bold;
                btTmp.alignment = TextAlignmentOptions.Center;

                var btn = btnGo.GetComponent<Button>();
                OutgameStyle.ApplyPrimaryButtonStyle(btn, btTmp);
                btn.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.SynchroBoard));
            }
            else
            {
                var t = panel.Find("SpecText");
                if (t != null) _suitSpecText = t.GetComponent<TextMeshProUGUI>();
            }
        }

        private void RefreshData()
        {
            if (_suitSpecText == null) return;

            int suitLv = 1;
            string edictAura = "미장착";
            int generalCount = 0;

            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                var kProg = SaveManager.Instance.CurrentData.KingProgression;
                suitLv = Mathf.Max(1, kProg.SuitLevel);
                if (!string.IsNullOrEmpty(kProg.EquippedAbsoluteEdictId)) edictAura = kProg.EquippedAbsoluteEdictId;
                if (kProg.EquippedGeneralEdictIds != null) generalCount = kProg.EquippedGeneralEdictIds.Count;
            }

            _suitSpecText.text =
                $"<color=#00E5FF><b>[KING'S SUIT // 전술 프레임 제원]</b></color>\n\n" +
                $"• 프레임 모델: <b>S-01 EXCALIBUR TACTICAL FRAME</b>\n" +
                $"• 슈트 레벨: <color=#00E5FF><b>Lv.{suitLv}</b></color>\n" +
                $"• 장착 절대 칙령(오라): <color=#FFB300>{edictAura}</color>\n" +
                $"• 가동 일반 칙령 회로: <color=#00E5FF>{generalCount} / 3 개 슬롯</color>\n\n" +
                $"<size=85%><color=#8A94A6>지휘관의 전술 슈트는 부대 전체의 작전 능력과 칙령 회로 용량을 결정합니다.\n싱크로 보드에서 칙령을 교체하여 전투 능력을 커스터마이즈할 수 있습니다.</color></size>";
        }
    }
}
