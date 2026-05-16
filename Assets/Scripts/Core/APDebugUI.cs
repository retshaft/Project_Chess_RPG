using System;
using System.Collections.Generic;
using CheckmateRPG.Testing;
using CheckmateRPG.Units;
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
        [SerializeField] private Vector2 _detailTextOffset = new Vector2(20f, 115f);
        [SerializeField] private Vector2 _detailTextSize = new Vector2(860f, 180f);
        [SerializeField] private Vector2 _gaugeOffset = new Vector2(0f, 10f);
        [SerializeField] private Vector2 _gaugeSize = new Vector2(320f, 20f);

        [Header("Style")]
        [SerializeField] private Font _debugFont;
        [SerializeField] private int _apFontSize = 26;
        [SerializeField] private int _warningFontSize = 24;
        [SerializeField] private int _detailFontSize = 18;
        [SerializeField] private Color _gaugeBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _gaugeFillColor = new Color(0.2f, 0.75f, 1f, 0.95f);
        [SerializeField] private Color _warningColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private float _warningFlashDuration = 1.25f;
        [SerializeField] private float _warningFlashSpeed = 6f;

        private Text _apText;
        private Text _warningText;
        private Text _detailText;
        private Image _gaugeFill;
        private float _warningTimer;
        private APManager _apManager;
        private float _lastDelta;
        private APChangeReason _lastReason = APChangeReason.Initialization;

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

            APDebugLogger.SnapshotChanged += HandleSnapshotChanged;
            UpdateRuntimeDebugText();
        }

        private void OnDisable()
        {
            APDebugLogger.SnapshotChanged -= HandleSnapshotChanged;

            if (_apManager != null)
                _apManager.OnAPChanged -= HandleAPChanged;
        }

        private void Update()
        {
            UpdateWarningVisuals();
            UpdateRuntimeDebugText();
        }

        public void UpdateAP(float current, float max, float delta, APChangeReason reason)
        {
            if (_apText == null)
                return;

            int currentDisplay = Mathf.CeilToInt(current);
            int maxDisplay = Mathf.CeilToInt(max);
            _apText.text = $"AP {currentDisplay}/{maxDisplay}";

            _lastDelta = delta;
            _lastReason = reason;

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

        private void HandleSnapshotChanged()
        {
            UpdateRuntimeDebugText();
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
            _warningText = CreateText("APWarningText", canvasGO.transform, font, _warningTextOffset, _warningFontSize, _warningColor);
            _warningText.gameObject.SetActive(false);

            _detailText = CreateText("APRuntimeText", canvasGO.transform, font, _detailTextOffset, _detailFontSize, Color.white);
            _detailText.alignment = TextAnchor.UpperLeft;
            _detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailText.verticalOverflow = VerticalWrapMode.Overflow;
            _detailText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _detailText.rectTransform.anchorMax = new Vector2(0f, 0f);
            _detailText.rectTransform.pivot = new Vector2(0f, 0f);
            _detailText.rectTransform.sizeDelta = _detailTextSize;
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

        private void UpdateRuntimeDebugText()
        {
            if (_detailText == null)
                return;

            UnitBrain turnUnit = APDebugLogger.CurrentTurnUnit;
            string turnUnitName = turnUnit != null ? turnUnit.name : "None";
            string regenState = _apManager == null
                ? "Unknown"
                : (_apManager.RegenEnabled ? "Active" : $"Paused ({_apManager.RegenPauseReason})");

            float delta = APDebugLogger.LastAPChangeFrame >= 0 ? APDebugLogger.LastAPDelta : _lastDelta;
            APChangeReason reason = APDebugLogger.LastAPChangeFrame >= 0
                ? APDebugLogger.LastAPChangeReason
                : _lastReason;

            string deltaText = delta >= 0f ? $"+{delta:0.0}" : delta.ToString("0.0");
            string statusText = BuildStatusText(turnUnit);
            string queuedCommand = string.IsNullOrWhiteSpace(APDebugLogger.LastQueuedCommandSummary)
                ? "None"
                : APDebugLogger.LastQueuedCommandSummary;

            _detailText.text =
                $"Turn Unit: {turnUnitName}\n" +
                $"AP Δ: {deltaText} ({reason})  Regen: {regenState}\n" +
                $"Status: {statusText}\n" +
                $"Last Input: {queuedCommand}";
        }

        private static string BuildStatusText(UnitBrain unit)
        {
            if (unit == null || unit.StatusEffects == null)
                return "None";

            var activeStatuses = new List<string>();
            Array statusValues = Enum.GetValues(typeof(StatusEffectType));
            for (int i = 0; i < statusValues.Length; i++)
            {
                var status = (StatusEffectType)statusValues.GetValue(i);
                if (unit.StatusEffects.HasStatus(status))
                    activeStatuses.Add(status.ToString());
            }

            return activeStatuses.Count == 0
                ? "None"
                : string.Join(", ", activeStatuses);
        }

        private Text CreateText(string name, Transform parent, Font font, Vector2 anchoredPosition, int fontSize, Color color)
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
