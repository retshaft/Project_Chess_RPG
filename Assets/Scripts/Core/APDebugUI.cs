using UnityEngine;
using UnityEngine.UI;

namespace CheckmateRPG.Core
{
    public class APDebugUI : MonoBehaviour, IAPUI
    {
        [Header("Layout")]
        [SerializeField] private Vector2 _apTextOffset = new Vector2(0f, 40f);
        [SerializeField] private Vector2 _warningTextOffset = new Vector2(0f, 85f);
        [SerializeField] private Vector2 _textSize = new Vector2(420f, 48f);
        [SerializeField] private Vector2 _gaugeOffset = new Vector2(0f, 10f);
        [SerializeField] private Vector2 _gaugeSize = new Vector2(320f, 20f);

        [Header("Style")]
        [SerializeField] private Font _debugFont;
        [SerializeField] private int _apFontSize = 26;
        [SerializeField] private int _warningFontSize = 24;
        [SerializeField] private Color _gaugeBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _gaugeFillColor = new Color(0.2f, 0.75f, 1f, 0.95f);
        [SerializeField] private Color _warningColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private float _warningFlashDuration = 1.25f;
        [SerializeField] private float _warningFlashSpeed = 6f;

        private Text _apText;
        private Text _warningText;
        private Image _gaugeFill;
        private float _warningTimer;
        private APManager _apManager;

        private void Awake()
        {
            BuildDebugUI();
            _apManager = APManager.Instance ?? GetComponentInParent<APManager>();
        }

        private void OnEnable()
        {
            if (_apManager != null)
            {
                _apManager.OnAPChanged += HandleAPChanged;
                UpdateAP(_apManager.CurrentAP, _apManager.MaxAP, 0f, APChangeReason.Initialization);
            }
        }

        private void OnDisable()
        {
            if (_apManager != null)
                _apManager.OnAPChanged -= HandleAPChanged;
        }

        private void Update()
        {
            UpdateWarningVisuals();
        }

        public void UpdateAP(float current, float max, float delta, APChangeReason reason)
        {
            if (_apText == null)
                return;

            int currentDisplay = Mathf.CeilToInt(current);
            int maxDisplay = Mathf.CeilToInt(max);
            _apText.text = $"AP {currentDisplay}/{maxDisplay}";

            if (_gaugeFill != null)
                _gaugeFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        public void ShowInsufficientAP(float requested, float current, float missing, APActionReason reason, string message)
        {
            if (_warningText == null)
                return;

            _warningText.text = message;
            _warningTimer = Mathf.Max(_warningTimer, _warningFlashDuration);
            _warningText.gameObject.SetActive(true);
        }

        private void HandleAPChanged(float current, float max, float delta, APChangeReason reason)
        {
            UpdateAP(current, max, delta, reason);
        }

        private void BuildDebugUI()
        {
            var canvasGO = new GameObject("APCanvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGO.AddComponent<GraphicRaycaster>();

            Font font = _debugFont != null ? _debugFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            CreateGauge(canvasGO.transform);
            _apText = CreateText("APValueText", canvasGO.transform, font, _apTextOffset, _apFontSize, Color.white);
            _warningText = CreateText("APWarningText", canvasGO.transform, font, _warningTextOffset, _warningFontSize,
                _warningColor);
            _warningText.gameObject.SetActive(false);
        }

        private void CreateGauge(Transform parent)
        {
            var background = CreateImage("APGaugeBackground", parent, _gaugeOffset, _gaugeSize, _gaugeBackgroundColor);
            _gaugeFill = CreateImage("APGaugeFill", background.transform, Vector2.zero, _gaugeSize, _gaugeFillColor);
            _gaugeFill.type = Image.Type.Filled;
            _gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            _gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _gaugeFill.fillAmount = 1f;
        }

        private void UpdateWarningVisuals()
        {
            if (_warningText == null)
                return;

            if (_warningTimer > 0f)
            {
                _warningTimer -= Time.deltaTime;
                float pulse = Mathf.PingPong(Time.time * _warningFlashSpeed, 1f);
                Color color = _warningColor;
                color.a = Mathf.Lerp(0.2f, 1f, pulse);
                _warningText.color = color;
                return;
            }

            if (_warningText.gameObject.activeSelf)
                _warningText.gameObject.SetActive(false);
        }

        private Text CreateText(string name, Transform parent, Font font, Vector2 anchoredPosition, int fontSize,
            Color color)
        {
            var textGO = new GameObject(name);
            textGO.transform.SetParent(parent, false);

            var rect = textGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = _textSize;

            var text = textGO.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Image CreateImage(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var imageGO = new GameObject(name);
            imageGO.transform.SetParent(parent, false);

            var rect = imageGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = imageGO.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
