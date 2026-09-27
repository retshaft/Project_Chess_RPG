using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    public class MainLobbyUI : OutgameViewBase
    {
        [Header("Menu Buttons")]
        [SerializeField] private Button stageSelectButton;
        [SerializeField] private Button rosterButton;
        [SerializeField] private Button synchroBoardButton;
        [SerializeField] private Button recruitButton;
        [SerializeField] private Button operatorHubButton;
        [SerializeField] private Button archiveButton;
        [SerializeField] private Button kingSuitBayButton;

        [Header("Profile & Resources")]
        [SerializeField] private TextMeshProUGUI kingLevelText;
        [SerializeField] private TextMeshProUGUI tacticalPointsText;

        [Header("Secretary & Dialogue")]
        [SerializeField] private Button secretaryTouchArea;
        [SerializeField] private GameObject speechBubblePanel;
        [SerializeField] private TextMeshProUGUI speechBubbleText;
        [SerializeField] private string[] secretaryQuotes = new string[]
        {
            "지휘관님, 킹의 체스판 위에 모든 기물이 준비되었습니다.",
            "다음 작전 구역의 정찰 보고가 입수되었습니다. 지시를 내려 주십시오.",
            "부대 공명 융합율이 안정 범위에 도달해 있습니다. 출전 준비 완료입니다.",
            "잠시 휴식이신가요? 칙령(Edict) 회로 조정을 검토할 좋은 시간입니다.",
            "언제든 체크메이트를 고지할 각오가 되어 있습니다."
        };

        private Coroutine dialogueCoroutine;

        private void Awake()
        {
            if (stageSelectButton) stageSelectButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.StageSelect));
            if (rosterButton) rosterButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.Roster));
            if (synchroBoardButton) synchroBoardButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.SynchroBoard));
            if (recruitButton) recruitButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.Recruit));
            if (operatorHubButton) operatorHubButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.OperatorHub));
            if (archiveButton) archiveButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.Archive));
            if (kingSuitBayButton) kingSuitBayButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.KingSuitBay));
            
            if (secretaryTouchArea) secretaryTouchArea.onClick.AddListener(TriggerSecretaryQuote);
            if (speechBubblePanel) speechBubblePanel.SetActive(false);
            
            ApplyStyles();
        }

        private void ApplyStyles()
        {
            var image = GetComponent<Image>();
            if (image != null) OutgameStyle.ApplySurfaceStyle(image);

            // 핵심 작전 출격 버튼에 택티컬 오렌지 강조 적용
            OutgameStyle.ApplyCombatButtonStyle(stageSelectButton, stageSelectButton?.GetComponentInChildren<TextMeshProUGUI>());

            OutgameStyle.ApplyPrimaryButtonStyle(rosterButton, rosterButton?.GetComponentInChildren<TextMeshProUGUI>());
            OutgameStyle.ApplyPrimaryButtonStyle(synchroBoardButton, synchroBoardButton?.GetComponentInChildren<TextMeshProUGUI>());
            OutgameStyle.ApplyPrimaryButtonStyle(recruitButton, recruitButton?.GetComponentInChildren<TextMeshProUGUI>());
            OutgameStyle.ApplyPrimaryButtonStyle(operatorHubButton, operatorHubButton?.GetComponentInChildren<TextMeshProUGUI>());
            OutgameStyle.ApplyPrimaryButtonStyle(archiveButton, archiveButton?.GetComponentInChildren<TextMeshProUGUI>());
            OutgameStyle.ApplyPrimaryButtonStyle(kingSuitBayButton, kingSuitBayButton?.GetComponentInChildren<TextMeshProUGUI>());

            if (speechBubblePanel != null)
            {
                var speechImg = speechBubblePanel.GetComponent<Image>();
                if (speechImg != null) OutgameStyle.ApplyCardStyle(speechImg);
            }
        }

        public void TriggerSecretaryQuote()
        {
            if (speechBubblePanel == null || speechBubbleText == null) return;

            if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
            
            string randomQuote = secretaryQuotes != null && secretaryQuotes.Length > 0 
                ? secretaryQuotes[Random.Range(0, secretaryQuotes.Length)] 
                : "......";

            speechBubbleText.text = randomQuote;
            speechBubblePanel.SetActive(true);
            
            dialogueCoroutine = StartCoroutine(HideSpeechBubbleAfterDelay(3.5f));
        }

        private IEnumerator HideSpeechBubbleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (speechBubblePanel) speechBubblePanel.SetActive(false);
        }

        public override void Show()
        {
            base.Show();
            RefreshLobbyData();
        }

        private void RefreshLobbyData()
        {
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                var data = SaveManager.Instance.CurrentData;
                if (kingLevelText != null)
                {
                    kingLevelText.text = $"<color=#8A94A6>지휘관 슈트</color> <b><color=#00E5FF>Lv.{data.KingProgression.SuitLevel}</color></b>";
                }
                if (tacticalPointsText != null)
                {
                    int clearedCount = data.ClearedStages != null ? data.ClearedStages.Count : 0;
                    tacticalPointsText.text = $"<color=#8A94A6>작전 완수:</color> <color=#00E5FF>{clearedCount} 구역</color>";
                }
            }
        }
    }
}
