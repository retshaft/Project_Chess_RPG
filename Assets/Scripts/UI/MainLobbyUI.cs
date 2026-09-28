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
            if (rosterButton) rosterButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.DeckBuilder));
            if (synchroBoardButton) synchroBoardButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.SynchroBoard));
            if (recruitButton) recruitButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.Recruit));
            if (operatorHubButton) operatorHubButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.OperatorHub));
            if (archiveButton) archiveButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.Archive));
            if (kingSuitBayButton) kingSuitBayButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.KingSuitBay));
            
            if (secretaryTouchArea) secretaryTouchArea.gameObject.SetActive(false);
            if (speechBubblePanel) speechBubblePanel.SetActive(false);
            
            ApplyStyles();
        }

        private void ApplyStyles()
        {
            TacticalLayout.Page(this);
            TacticalLayout.Artwork(transform);
            TacticalLayout.Panel(transform, "MenuBacking", new Color(.08f,.09f,.09f,.22f), .565f,.13f,.4f,.65f);
            TacticalLayout.Label(transform,"CommandTitle","사령부",54,OutgameStyle.Ink,.59f,.79f,.32f,.075f);
            TacticalLayout.Label(transform,"CommandCode","01 / COMMAND CENTER",17,OutgameStyle.Ink,.592f,.86f,.33f,.035f);
            TacticalLayout.Menu(stageSelectButton,"02","작전 출격","OPERATIONS",.585f,.565f,.365f,.195f,true,true);
            TacticalLayout.Menu(operatorHubButton,"03","대원 관리","OPERATORS",.585f,.419f,.177f,.13f,true);
            TacticalLayout.Menu(rosterButton,"05","부대 편성","FORMATION",.773f,.419f,.177f,.13f,true);
            TacticalLayout.Menu(synchroBoardButton,"04","칙령 회로","SYNCHRO",.585f,.274f,.177f,.13f);
            TacticalLayout.Menu(recruitButton,"08","인재 채용","HEADHUNTING",.773f,.274f,.177f,.13f);
            TacticalLayout.Menu(kingSuitBayButton,"09","슈트 격납고","KING'S SUIT",.585f,.145f,.177f,.113f);
            TacticalLayout.Menu(archiveButton,"10","작전 기록","ARCHIVE",.773f,.145f,.177f,.113f);

            var assistant = transform.Find("AssistantCard");
            if (assistant)
            {
                assistant.gameObject.SetActive(false);
            }
            var status = transform.Find("TerminalStatusCard");
            if (status)
            {
                TacticalLayout.Place(status,.035f,.745f,.185f,.115f);
                status.GetComponent<Image>().color = new Color(.07f,.08f,.08f,.9f);
                TacticalLayout.Place(kingLevelText.transform,.08f,.51f,.84f,.4f);
                TacticalLayout.Place(tacticalPointsText.transform,.08f,.12f,.84f,.35f);
                kingLevelText.alignment = tacticalPointsText.alignment = TextAlignmentOptions.MidlineLeft;
                kingLevelText.fontSize = 22; tacticalPointsText.fontSize = 18;
            }
            if (speechBubblePanel)
            {
                speechBubblePanel.transform.SetParent(transform,false);
                TacticalLayout.Place(speechBubblePanel.transform,.04f,.23f,.45f,.11f);
                speechBubblePanel.GetComponent<Image>().color = OutgameStyle.Paper;
                speechBubbleText.color = OutgameStyle.Ink;
                speechBubbleText.fontSize = 23;
                speechBubblePanel.transform.SetAsLastSibling();
            }
            TacticalLayout.Label(transform,"LobbyFooter","CHECKMATE  /  FIELD COMMAND DIVISION",14,OutgameStyle.TextWhite,.035f,.025f,.6f,.03f);
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
                    kingLevelText.text = $"<color=#B1B7B4>지휘관 슈트</color> <b><color=#F3F2EC>Lv.{data.KingProgression.SuitLevel}</color></b>";
                }
                if (tacticalPointsText != null)
                {
                    int clearedCount = data.ClearedStages != null ? data.ClearedStages.Count : 0;
                    tacticalPointsText.text = $"<color=#B1B7B4>작전 완수:</color> <color=#F3F2EC>{clearedCount} 구역</color>";
                }
            }
        }
    }
}
