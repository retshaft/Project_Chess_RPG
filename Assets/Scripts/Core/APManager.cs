// APManager.cs
// Global action-point (AP) resource controller for battle actions.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace CheckmateRPG.Core
{
    /// <summary>
    /// Manages a global AP pool that regenerates over time and powers all actions.
    /// </summary>
    public class APManager : MonoBehaviour
    {
        // ─── Singleton ───────────────────────────────────────────────────────────

        public static APManager Instance { get; private set; }

        // ─── Configuration ───────────────────────────────────────────────────────

        [Header("AP Settings")]
        [Tooltip("Maximum action points available.")]
        [SerializeField] private float _maxAP = 20f;

        [Tooltip("AP regenerated per second.")]
        [SerializeField] private float _regenPerSecond = 4f;

        [Header("Debug UI")]
        [SerializeField] private bool _createDebugUI = true;

        [SerializeField] private Vector2 _apTextOffset = new Vector2(0f, 45f);
        [SerializeField] private Vector2 _warningTextOffset = new Vector2(0f, 80f);
        [SerializeField] private Color _warningColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private float _warningFlashDuration = 1.25f;
        [SerializeField] private float _warningFlashSpeed = 6f;
        [SerializeField] private string _warningMessage = "AP 부족!";

        // ─── State ───────────────────────────────────────────────────────────────

        public float CurrentAP { get; private set; }
        public float MaxAP => _maxAP;

        public event Action<float, float> OnAPChanged;
        public event Action<float, float> OnInsufficientAP;

        private Text _apText;
        private Text _warningText;
        private float _warningTimer;

        // ─── Lifecycle ───────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[APManager] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _maxAP = Mathf.Max(0f, _maxAP);
            CurrentAP = _maxAP;

            if (_createDebugUI)
                BuildDebugUI();

            RaiseAPChanged();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            Regenerate(Time.deltaTime);
            UpdateWarningVisuals();
        }

        // ─── Public API ──────────────────────────────────────────────────────────

        public bool TrySpend(float amount)
        {
            if (amount <= 0f)
                return true;

            if (CurrentAP >= amount)
            {
                CurrentAP -= amount;
                RaiseAPChanged();
                return true;
            }

            OnInsufficientAP?.Invoke(amount, CurrentAP);
            TriggerWarning();
            return false;
        }

        // ─── Internal Logic ───────────────────────────────────────────────────────

        private void Regenerate(float deltaTime)
        {
            if (_regenPerSecond <= 0f || _maxAP <= 0f || CurrentAP >= _maxAP)
                return;

            float next = Mathf.Min(_maxAP, CurrentAP + _regenPerSecond * deltaTime);
            if (!Mathf.Approximately(next, CurrentAP))
            {
                CurrentAP = next;
                RaiseAPChanged();
            }
        }

        private void RaiseAPChanged()
        {
            OnAPChanged?.Invoke(CurrentAP, _maxAP);
            UpdateDebugUI();
        }

        private void TriggerWarning()
        {
            _warningTimer = Mathf.Max(_warningTimer, _warningFlashDuration);
            if (_warningText != null)
                _warningText.gameObject.SetActive(true);
        }

        // ─── Debug UI ────────────────────────────────────────────────────────────

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

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _apText = CreateText("APValueText", canvasGO.transform, font, _apTextOffset, 24, Color.white);
            _warningText = CreateText("APWarningText", canvasGO.transform, font, _warningTextOffset, 22, _warningColor);
            _warningText.text = _warningMessage;
            _warningText.gameObject.SetActive(false);
        }

        private void UpdateDebugUI()
        {
            if (_apText == null)
                return;

            int current = Mathf.CeilToInt(CurrentAP);
            int max = Mathf.CeilToInt(_maxAP);
            _apText.text = $"AP {current}/{max}";
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

        private static Text CreateText(string name, Transform parent, Font font, Vector2 anchoredPosition,
            int fontSize, Color color)
        {
            var textGO = new GameObject(name);
            textGO.transform.SetParent(parent, false);

            var rect = textGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(400f, 40f);

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
    }
}
