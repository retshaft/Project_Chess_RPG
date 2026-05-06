using UnityEngine;
using CheckmateRPG.Core;

namespace CheckmateRPG.Testing
{
    public class APDebugLogger : MonoBehaviour
    {
        [SerializeField] private bool _logAPChanges = true;
        [SerializeField] private bool _logInsufficientAP = true;

        private APManager _apManager;

        private void Awake()
        {
            _apManager = APManager.Instance ?? GetComponent<APManager>();
        }

        private void OnEnable()
        {
            if (_apManager == null)
                return;

            _apManager.OnAPChanged += HandleAPChanged;
            _apManager.OnInsufficientAP += HandleInsufficientAP;
        }

        private void OnDisable()
        {
            if (_apManager == null)
                return;

            _apManager.OnAPChanged -= HandleAPChanged;
            _apManager.OnInsufficientAP -= HandleInsufficientAP;
        }

        private void HandleAPChanged(float current, float max, float delta, APChangeReason reason)
        {
            if (!_logAPChanges)
                return;

            Debug.Log($"[APDebugLogger] AP {current:0.0}/{max:0.0} (Δ {delta:0.0}, {reason})");
        }

        private void HandleInsufficientAP(float requested, float current, float missing, APActionReason reason)
        {
            if (!_logInsufficientAP)
                return;

            Debug.Log($"[APDebugLogger] AP 부족: 요청 {requested:0.0}, 보유 {current:0.0}, 부족 {missing:0.0} ({reason})");
        }
    }
}
