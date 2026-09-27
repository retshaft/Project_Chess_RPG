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
                OutgameStyle.ApplyPrimaryButtonStyle(startButton);
            }
            
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
