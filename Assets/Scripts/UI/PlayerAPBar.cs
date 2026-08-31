using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using CheckmateRPG.Core;

namespace CheckmateRPG.UI
{
    public class PlayerAPBar : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Slider _apSlider;
        [SerializeField] private Slider _ghostSlider;
        [SerializeField] private Text _apText;
        [SerializeField] private Text _warningText;

        [Header("Settings")]
        [SerializeField] private Color _normalTextColor = Color.white;
        [SerializeField] private Color _warningTextColor = new Color(1f, 0.2f, 0.29f, 1f); // Accent_Warning_Red
        [SerializeField] private Color _ghostColor = new Color(0f, 0.9f, 1f, 0.4f); // AP_Predict_Ghost
        [SerializeField] private float _warningDuration = 1.5f;

        private Coroutine _warningRoutine;

        private void Start()
        {
            if (_apText == null || _apSlider == null)
            {
                CreateProgrammaticUI();
            }

            if (APManager.Instance != null)
            {
                APManager.Instance.OnAPChanged += HandleAPChanged;
                APManager.Instance.OnInsufficientAP += HandleInsufficientAP;
                
                // Init UI
                HandleAPChanged(APManager.Instance.CurrentAP, APManager.Instance.MaxAP, 0f, APChangeReason.Initialization);
            }

            if (_warningText != null)
            {
                _warningText.gameObject.SetActive(false);
            }
        }

        private void CreateProgrammaticUI()
        {
            var canvasGo = new GameObject("PlayerAPBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var bgGo = new GameObject("AP_BG");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0f);
            bgRect.anchorMax = new Vector2(0.5f, 0f);
            bgRect.pivot = new Vector2(0.5f, 0f);
            bgRect.anchoredPosition = new Vector2(0f, 20f);
            bgRect.sizeDelta = new Vector2(400f, 40f);

            var fillGo = new GameObject("AP_Fill");
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0f, 0.9f, 1f, 1f); // Accent_Cyan
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            
            var ghostGo = new GameObject("AP_Ghost_Fill");
            ghostGo.transform.SetParent(bgGo.transform, false);
            var ghostImg = ghostGo.AddComponent<Image>();
            ghostImg.color = _ghostColor;
            var ghostRect = ghostGo.GetComponent<RectTransform>();
            ghostRect.anchorMin = new Vector2(0f, 0f);
            ghostRect.anchorMax = new Vector2(1f, 1f);
            ghostRect.offsetMin = Vector2.zero;
            ghostRect.offsetMax = Vector2.zero;
            
            var ghostSliderGo = new GameObject("GhostSlider");
            ghostSliderGo.transform.SetParent(bgGo.transform, false);
            var ghostSliderRt = ghostSliderGo.AddComponent<RectTransform>();
            ghostSliderRt.anchorMin = Vector2.zero; ghostSliderRt.anchorMax = Vector2.one;
            ghostSliderRt.offsetMin = Vector2.zero; ghostSliderRt.offsetMax = Vector2.zero;
            _ghostSlider = ghostSliderGo.AddComponent<Slider>();
            _ghostSlider.targetGraphic = ghostImg;
            _ghostSlider.fillRect = ghostRect;
            _ghostSlider.interactable = false;

            var apSliderGo = new GameObject("ApSlider");
            apSliderGo.transform.SetParent(bgGo.transform, false);
            var apSliderRt = apSliderGo.AddComponent<RectTransform>();
            apSliderRt.anchorMin = Vector2.zero; apSliderRt.anchorMax = Vector2.one;
            apSliderRt.offsetMin = Vector2.zero; apSliderRt.offsetMax = Vector2.zero;
            _apSlider = apSliderGo.AddComponent<Slider>();
            _apSlider.targetGraphic = fillImg;
            _apSlider.fillRect = fillRect;
            _apSlider.interactable = false;
            
            // Put ghost behind fill
            ghostGo.transform.SetSiblingIndex(fillGo.transform.GetSiblingIndex());

            var textGo = new GameObject("AP_Text");
            textGo.transform.SetParent(bgGo.transform, false);
            _apText = textGo.AddComponent<Text>();
            _apText.alignment = TextAnchor.MiddleCenter;
            _apText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _apText.fontSize = 20;
            _apText.color = _normalTextColor;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var warningGo = new GameObject("AP_WarningText");
            warningGo.transform.SetParent(canvasGo.transform, false);
            _warningText = warningGo.AddComponent<Text>();
            _warningText.alignment = TextAnchor.MiddleCenter;
            _warningText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _warningText.fontSize = 24;
            _warningText.color = _warningTextColor;
            var warningRect = warningGo.GetComponent<RectTransform>();
            warningRect.anchorMin = new Vector2(0.5f, 0f);
            warningRect.anchorMax = new Vector2(0.5f, 0f);
            warningRect.pivot = new Vector2(0.5f, 0f);
            warningRect.anchoredPosition = new Vector2(0f, 70f); // Above the bar
            warningRect.sizeDelta = new Vector2(400f, 40f);
        }

        private void OnDestroy()
        {
            if (APManager.Instance != null)
            {
                APManager.Instance.OnAPChanged -= HandleAPChanged;
                APManager.Instance.OnInsufficientAP -= HandleInsufficientAP;
            }
        }

        private void HandleAPChanged(float current, float max, float delta, APChangeReason reason)
        {
            if (_apSlider != null && max > 0f)
            {
                float fill = Mathf.Clamp01(current / max);
                _apSlider.value = fill;
                if (_ghostSlider != null) _ghostSlider.value = fill; // Reset ghost
            }

            if (_apText != null)
            {
                _apText.text = $"AP {Mathf.FloorToInt(current)} / {Mathf.FloorToInt(max)}"; // Arknights style
            }
        }

        public void ShowAPPreview(float predictedCost)
        {
            if (APManager.Instance == null || _ghostSlider == null || _apSlider == null) return;
            
            float max = APManager.Instance.MaxAP;
            if (max <= 0) return;

            float current = APManager.Instance.CurrentAP;
            float target = Mathf.Max(0, current - predictedCost);
            
            // The ghost bar shows where the AP will drop to.
            _ghostSlider.value = Mathf.Clamp01(current / max);
            _apSlider.value = Mathf.Clamp01(target / max); 
        }

        public void ClearAPPreview()
        {
            if (APManager.Instance == null) return;
            HandleAPChanged(APManager.Instance.CurrentAP, APManager.Instance.MaxAP, 0, APChangeReason.Initialization);
        }

        private void HandleInsufficientAP(float cost, float current, float missing, APActionReason reason)
        {
            if (_warningText == null) return;

            if (_warningRoutine != null) StopCoroutine(_warningRoutine);
            _warningRoutine = StartCoroutine(ShowWarningRoutine());
        }

        private IEnumerator ShowWarningRoutine()
        {
            _warningText.gameObject.SetActive(true);
            _warningText.text = "[INSUFFICIENT AP]";
            _warningText.color = _warningTextColor;
            _warningText.transform.localScale = Vector3.one * 1.5f;

            Color origColor = new Color(0f, 0.9f, 1f, 1f); // Accent_Cyan
            
            float elapsed = 0f;
            float duration = 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Scale down bounce
                if (t < 0.3f)
                {
                    float scaleT = t / 0.3f;
                    _warningText.transform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, scaleT);
                }

                // Flash color
                float blink = Mathf.PingPong(elapsed * 10f, 1f);
                if (_apSlider != null && _apSlider.targetGraphic != null)
                {
                    _apSlider.targetGraphic.color = Color.Lerp(origColor, _warningTextColor, blink);
                }
                
                Color c = _warningTextColor;
                c.a = Mathf.Lerp(1f, 0f, blink);
                _warningText.color = c;

                yield return null;
            }

            // Fade out
            elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / 0.3f);
                Color c = _warningTextColor; c.a = alpha;
                _warningText.color = c;
                yield return null;
            }

            if (_apSlider != null && _apSlider.targetGraphic != null)
                _apSlider.targetGraphic.color = origColor;

            _warningText.gameObject.SetActive(false);
            _warningRoutine = null;
        }
    }
}
