using UnityEngine;

namespace CheckmateRPG.Core
{
    public class APFailFeedback : MonoBehaviour
    {
        [SerializeField] private bool _logFailures = true;
        [SerializeField] private string _warningFormat = "AP 부족: {0:0.0}";
        [SerializeField] private string _consoleFormat = "[AP] AP 부족 ({0}) 요청 {1:0.0}, 보유 {2:0.0}, 부족 {3:0.0}";

        private APManager _apManager;
        private IAPUI _apUI;

        private void Awake()
        {
            _apManager = APManager.Instance ?? GetComponent<APManager>();
            _apUI = GetComponent<IAPUI>() ?? GetComponentInChildren<IAPUI>();
        }

        private void OnEnable()
        {
            if (_apManager != null)
                _apManager.OnInsufficientAP += HandleInsufficientAP;
        }

        private void OnDisable()
        {
            if (_apManager != null)
                _apManager.OnInsufficientAP -= HandleInsufficientAP;
        }

        private void HandleInsufficientAP(float requested, float current, float missing, APActionReason reason)
        {
            if (_logFailures)
                Debug.Log(string.Format(_consoleFormat, reason, requested, current, missing));

            if (_apUI != null)
                _apUI.ShowInsufficientAP(requested, current, missing, reason, string.Format(_warningFormat, missing));
        }
    }
}
