using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CheckmateRPG.UI
{
    public class KingSuitBayUI : OutgameViewBase
    {
        [SerializeField] private Button _backButton;
        
        private void Awake()
        {
            if (_backButton != null)
                _backButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby));
        }
    }
}
