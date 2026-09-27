using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    /// <summary>
    /// 명일방주풍 전술 작전 기록 아카이브(Archive) 화면입니다.
    /// 완수된 작전 구역 목록 및 전술 교리 기록을 제공합니다.
    /// </summary>
    public class ArchiveUI : OutgameViewBase
    {
        [SerializeField] private Button _backButton;
        private TextMeshProUGUI _archiveContentText;

        private void Awake()
        {
            ViewType = OutgameViewType.Archive;
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
            var panel = transform.Find("ArchiveContainer");
            if (panel == null)
            {
                var panelGo = new GameObject("ArchiveContainer", typeof(RectTransform), typeof(Image));
                panelGo.transform.SetParent(transform, false);
                var pRt = (RectTransform)panelGo.transform;
                pRt.anchorMin = new Vector2(0.5f, 0.5f);
                pRt.anchorMax = new Vector2(0.5f, 0.5f);
                pRt.sizeDelta = new Vector2(900f, 500f);
                panelGo.GetComponent<Image>().color = OutgameStyle.SurfaceCard;

                var textGo = new GameObject("ArchiveText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(panelGo.transform, false);
                var tRt = (RectTransform)textGo.transform;
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = new Vector2(36f, 36f);
                tRt.offsetMax = new Vector2(-36f, -36f);
                _archiveContentText = textGo.GetComponent<TextMeshProUGUI>();
                _archiveContentText.font = UiFontProvider.GetTmpFont();
                _archiveContentText.fontSize = 21;
                _archiveContentText.color = OutgameStyle.TextWhite;
            }
            else
            {
                var t = panel.Find("ArchiveText");
                if (t != null) _archiveContentText = t.GetComponent<TextMeshProUGUI>();
            }
        }

        private void RefreshData()
        {
            if (_archiveContentText == null) return;

            int clearedCount = 0;
            int totalOperators = 0;

            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                var save = SaveManager.Instance.CurrentData;
                if (save.ClearedStages != null) clearedCount = save.ClearedStages.Count;
                if (save.UnlockedUnits != null) totalOperators = save.UnlockedUnits.Count;
            }

            _archiveContentText.text =
                $"<color=#00E5FF><b>[ARCHIVE // 전술 작전 기록 및 기보 분석 보관소]</b></color>\n\n" +
                $"• 완수된 작전 구역: <color=#00E5FF><b>{clearedCount} 개소</b></color>\n" +
                $"• 소집 및 등록된 오퍼레이터: <color=#FFB300><b>{totalOperators} 명</b></color>\n" +
                $"• 전술 분석 시스템: <color=#00E5FF>PRTS TACTICAL ARCHIVE v1.0 ONLINE</color>\n\n" +
                $"<color=#8A94A6><b>[체스 RPG 전술 교리]</b></color>\n" +
                $"1. 기물 고유의 이동 궤적과 AP 소모를 고려하여 전장을 선점하십시오.\n" +
                $"2. 아군의 체급(Weight)과 넉백(Knockback) 충돌을 활용하여 적 진형을 붕괴시키십시오.\n" +
                $"3. SP 충전 완료 시 발동 가능한 액티브 스킬은 전투의 흐름을 단숨에 전환합니다.\n" +
                $"4. 적 지휘관(킹) 처치 시 즉시 체크메이트(승리)가 선언됩니다.";
        }
    }
}
