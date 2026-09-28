using UnityEngine;
using UnityEngine.UI;

namespace CheckmateRPG.UI
{
    public class TitleUI : OutgameViewBase
    {
        [SerializeField] private Button startButton;

        private void Start()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
                OutgameStyle.ApplyCombatButtonStyle(startButton);
                TacticalLayout.Place(startButton.transform,.595f,.245f,.32f,.085f);
                var label = startButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (label) label.text = "접속 시작  /  ENTER";
            }
            
            TacticalLayout.Page(this);
            TacticalLayout.Artwork(transform);
            TacticalLayout.Panel(transform,"TitlePlate",new Color(.07f,.08f,.08f,.94f),.555f,.19f,.4f,.63f);
            if (startButton) startButton.transform.SetAsLastSibling();
            TacticalLayout.Label(transform,"Edition","TACTICAL ROLE-PLAYING GAME  /  01",16,OutgameStyle.TextMuted,.593f,.71f,.34f,.04f);
            TacticalLayout.Label(transform,"Wordmark","CHECK\nMATE",94,OutgameStyle.TextWhite,.59f,.44f,.36f,.27f).fontStyle = TMPro.FontStyles.Bold;
            TacticalLayout.Label(transform,"KoreanTitle","체크메이트  /  당신의 다음 수",22,OutgameStyle.TextWhite,.595f,.365f,.34f,.055f);
            TacticalLayout.Label(transform,"Footnote","CHECKMATE RPG  /  COMMAND INTERFACE",14,OutgameStyle.TextWhite,.04f,.03f,.8f,.04f);
            var image = GetComponent<Image>();
            if (image != null)
            {
                OutgameStyle.ApplySurfaceStyle(image);
            }
        }

        private void OnStartClicked()
        {
            // 인증, 리소스 체크(페이크) 연출 후 로비로 이동
            OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby, true);
        }
    }
}
