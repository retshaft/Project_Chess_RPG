using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using CheckmateRPG.Components;
using TMPro;

namespace CheckmateRPG.UI
{
    public class UnitStatusBar : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image _hpFillImage;
        [SerializeField] private Image _spFillImage;

        [Header("Settings")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private bool _billboard = true;

        private HealthComponent _health;
        private StatusEffectComponent _statusEffects;
        private CheckmateRPG.Units.UnitBrain _unitBrain;
        private SPComponent _sp;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _promotionText;
        private TextMeshProUGUI _exReadyText;
        private TextMeshProUGUI _weightText;
        private Camera _mainCamera;
        private Transform _target;
        private System.Collections.Generic.List<CheckmateRPG.Core.StatusEffectType> _activeStatusList = new();
        private bool _isExReady = false;
        private Coroutine _exReadyRoutine;

        public void Initialize(Transform targetUnit)
        {
            _target = targetUnit;
            _health = _target.GetComponent<HealthComponent>();
            _statusEffects = _target.GetComponent<StatusEffectComponent>();
            _unitBrain = _target.GetComponent<CheckmateRPG.Units.UnitBrain>();
            _sp = _target.GetComponent<SPComponent>();
            
            if (_hpFillImage == null)
                CreateProgrammaticUI();

            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
            }

            if (_statusEffects != null)
            {
                _statusEffects.OnStatusApplied += HandleStatusApplied;
                _statusEffects.OnStatusRemoved += HandleStatusRemoved;
            }

            if (_sp != null)
            {
                _sp.OnSPChanged += HandleSPChanged;
                HandleSPChanged(_sp.CurrentSP, _sp.MaxSP);
            }

            _mainCamera = Camera.main;
        }

        private void CreateProgrammaticUI()
        {
            var canvasGo = new GameObject("StatusBarCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 0.5f);
            
            var bgGo = new GameObject("HP_BG");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = Color.black;
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(1.5f, 0.2f);

            var fillGo = new GameObject("HP_Fill");
            fillGo.transform.SetParent(bgGo.transform, false);
            _hpFillImage = fillGo.AddComponent<Image>();
            _hpFillImage.color = Color.green;
            _hpFillImage.type = Image.Type.Filled;
            _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(1, 1);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            var whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.zero);

            var spGo = new GameObject("SP_Fill");
            spGo.transform.SetParent(canvasGo.transform, false);
            _spFillImage = spGo.AddComponent<Image>();
            _spFillImage.sprite = whiteSprite;
            _spFillImage.color = Color.yellow;
            _spFillImage.type = Image.Type.Filled;
            _spFillImage.fillMethod = Image.FillMethod.Horizontal;
            var spRect = spGo.GetComponent<RectTransform>();
            spRect.sizeDelta = new Vector2(1.5f, 0.06f);
            spRect.anchoredPosition = new Vector2(0f, -0.15f);
            spRect.anchorMin = new Vector2(0.5f, 0.5f);
            spRect.anchorMax = new Vector2(0.5f, 0.5f);
            
            _hpFillImage.sprite = whiteSprite;

            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // Status Effect Text
            var statusGo = new GameObject("StatusText");
            statusGo.transform.SetParent(canvasGo.transform, false);
            _statusText = statusGo.AddComponent<TextMeshProUGUI>();
            _statusText.fontSize = 2.5f;
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.color = new Color(1f, 0.2f, 0.3f);
            if (font != null) _statusText.font = font;
            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(3f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0f, 0.35f);

            // Promotion Text
            var promoGo = new GameObject("PromotionText");
            promoGo.transform.SetParent(canvasGo.transform, false);
            _promotionText = promoGo.AddComponent<TextMeshProUGUI>();
            _promotionText.fontSize = 3f;
            _promotionText.alignment = TextAlignmentOptions.Center;
            _promotionText.color = new Color(1f, 0.8f, 0f); // Gold
            if (font != null) _promotionText.font = font;
            _promotionText.text = "PROMOTING...";
            _promotionText.gameObject.SetActive(false);
            var promoRect = promoGo.GetComponent<RectTransform>();
            promoRect.sizeDelta = new Vector2(3f, 0.5f);
            promoRect.anchoredPosition = new Vector2(0f, 0.7f);

            // EX Ready Glow Banner
            var exGo = new GameObject("EXReadyText");
            exGo.transform.SetParent(canvasGo.transform, false);
            _exReadyText = exGo.AddComponent<TextMeshProUGUI>();
            _exReadyText.fontSize = 3.2f;
            _exReadyText.fontStyle = FontStyles.Bold;
            _exReadyText.alignment = TextAlignmentOptions.Center;
            _exReadyText.text = "★ EX READY ★";
            if (font != null) _exReadyText.font = font;
            var exRect = exGo.GetComponent<RectTransform>();
            exRect.sizeDelta = new Vector2(3f, 0.6f);
            exRect.anchoredPosition = new Vector2(0f, -0.38f);
            exGo.SetActive(false);

            // Weight Info
            var weightGo = new GameObject("WeightText");
            weightGo.transform.SetParent(canvasGo.transform, false);
            _weightText = weightGo.AddComponent<TextMeshProUGUI>();
            _weightText.fontSize = 2f;
            _weightText.alignment = TextAlignmentOptions.Right;
            _weightText.color = new Color(0.8f, 0.8f, 0.8f);
            _weightText.text = "W: ?";
            if (font != null) _weightText.font = font;
            var weightRect = weightGo.GetComponent<RectTransform>();
            weightRect.sizeDelta = new Vector2(1f, 0.5f);
            weightRect.anchoredPosition = new Vector2(0.9f, 0.35f);
        }

        private void Start()
        {
            if (_unitBrain != null && _unitBrain.UnitData != null)
            {
                if (_weightText != null)
                {
                    _weightText.text = $"W: {_unitBrain.UnitData.Weight}";
                }
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
            }
            if (_statusEffects != null)
            {
                _statusEffects.OnStatusApplied -= HandleStatusApplied;
                _statusEffects.OnStatusRemoved -= HandleStatusRemoved;
            }
            if (_sp != null)
            {
                _sp.OnSPChanged -= HandleSPChanged;
            }
        }

        private void HandleStatusApplied(CheckmateRPG.Core.StatusEffectType type)
        {
            if (!_activeStatusList.Contains(type))
            {
                _activeStatusList.Add(type);
                UpdateStatusText();
            }
        }

        private void HandleStatusRemoved(CheckmateRPG.Core.StatusEffectType type)
        {
            if (_activeStatusList.Remove(type))
            {
                UpdateStatusText();
            }
        }

        private void UpdateStatusText()
        {
            if (_statusText == null) return;
            if (_activeStatusList.Count == 0)
            {
                _statusText.text = "";
                return;
            }
            
            int displayCount = Mathf.Min(3, _activeStatusList.Count);
            string result = "";
            for (int i = 0; i < displayCount; i++)
            {
                result += $"[{_activeStatusList[i].ToString()}] ";
            }
            if (_activeStatusList.Count > 3) result += "...";
            _statusText.text = result;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_hpFillImage != null && max > 0f)
            {
                float ratio = Mathf.Clamp01(current / max);
                _hpFillImage.fillAmount = ratio;
                // Color transition from Green -> Yellow -> Red as HP drops
                _hpFillImage.color = ratio > 0.5f ? Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f) : Color.Lerp(Color.red, Color.yellow, ratio * 2f);
            }
        }

        private IEnumerator ExReadyPulseRoutine()
        {
            while (true)
            {
                float alpha = Mathf.Lerp(0.3f, 1f, Mathf.PingPong(Time.time * 2f, 1f));
                if (_exReadyText != null)
                {
                    Color c = _exReadyText.color;
                    c.a = alpha;
                    _exReadyText.color = c;
                }
                yield return null;
            }
        }

        private void HandleSPChanged(float current, float max)
        {
            if (_spFillImage != null && max > 0f)
            {
                _spFillImage.fillAmount = Mathf.Clamp01(current / max);
                _isExReady = current >= max;

                if (_isExReady)
                {
                    _spFillImage.color = new Color(0f, 0.9f, 1f, 1f); // Accent_Cyan
                    if (_exReadyText != null) 
                    {
                        if (!_exReadyText.gameObject.activeSelf)
                        {
                            _exReadyText.gameObject.SetActive(true);
                            _exReadyText.color = new Color(1f, 0.7f, 0f, 1f); // Accent_Gold
                            if (_exReadyRoutine == null)
                            {
                                _exReadyRoutine = StartCoroutine(ExReadyPulseRoutine());
                            }
                        }
                    }
                }
                else
                {
                    _spFillImage.color = Color.yellow;
                    if (_exReadyText != null) 
                    {
                        _exReadyText.gameObject.SetActive(false);
                        if (_exReadyRoutine != null)
                        {
                            StopCoroutine(_exReadyRoutine);
                            _exReadyRoutine = null;
                        }
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = _target.position + _offset;

            if (_billboard && _mainCamera != null)
            {
                transform.forward = _mainCamera.transform.forward;
            }

            if (_unitBrain != null && _promotionText != null)
            {
                bool isPromoting = _unitBrain.IsPromoting;
                if (_promotionText.gameObject.activeSelf != isPromoting)
                {
                    _promotionText.gameObject.SetActive(isPromoting);
                }
            }

            // Animate EX READY glow text
            if (_isExReady && _exReadyText != null && _exReadyText.gameObject.activeInHierarchy)
            {
                float pulse = (Mathf.Sin(Time.time * 9f) + 1f) * 0.5f;
                _exReadyText.color = Color.Lerp(new Color(1f, 0.85f, 0f), new Color(0f, 1f, 1f), pulse);
                float scale = Mathf.Lerp(0.95f, 1.15f, pulse);
                _exReadyText.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
