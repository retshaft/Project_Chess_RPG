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
        [SerializeField] private Text _apText;
        [SerializeField] private Text _warningText;

        [Header("Settings")]
        [SerializeField] private Color _normalTextColor = Color.white;
        [SerializeField] private Color _warningTextColor = Color.red;
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
            fillImg.color = new Color(0.2f, 0.6f, 1f, 1f); // Blue AP
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            
            _apSlider = bgGo.AddComponent<Slider>();
            _apSlider.targetGraphic = fillImg;
            _apSlider.fillRect = fillRect;
            _apSlider.interactable = false;

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
                _apSlider.value = Mathf.Clamp01(current / max);
            }

            if (_apText != null)
            {
                _apText.text = $"AP: {Mathf.FloorToInt(current)} / {Mathf.FloorToInt(max)}";
            }
        }

        private void HandleInsufficientAP(float cost, float current, float missing, APActionReason reason)
        {
            if (_warningText == null) return;

            if (_warningRoutine != null)
            {
                StopCoroutine(_warningRoutine);
            }
            _warningRoutine = StartCoroutine(ShowWarningRoutine(missing));
        }

        private IEnumerator ShowWarningRoutine(float missingAP)
        {
            _warningText.gameObject.SetActive(true);
            _warningText.text = $"Need {Mathf.CeilToInt(missingAP)} more AP!";
            _warningText.color = _warningTextColor;

            // Flash effect
            float elapsed = 0f;
            while (elapsed < _warningDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.PingPong(elapsed * 4f, 1f); // Blink speed
                Color c = _warningText.color;
                c.a = alpha;
                _warningText.color = c;
                yield return null;
            }

            _warningText.gameObject.SetActive(false);
            _warningRoutine = null;
        }
    }
}
