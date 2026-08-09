using UnityEngine;
using UnityEngine.UI;
using CheckmateRPG.Units;
using CheckmateRPG.Components;
using CheckmateRPG.PlayerInput;

namespace CheckmateRPG.UI
{
    public class UnitInfoPanel : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _hpText;
        [SerializeField] private Text _atkText;
        [SerializeField] private Text _weightText;
        [SerializeField] private Text _moveRangeText;

        private PlayerInputController _inputController;
        private UnitBrain _currentUnit;
        private HealthComponent _currentHealth;

        private void Start()
        {
            if (_panelRoot == null)
            {
                CreateProgrammaticUI();
            }

            _inputController = Object.FindFirstObjectByType<PlayerInputController>();
            if (_inputController != null)
            {
                _inputController.OnSelectionChanged += HandleSelectionChanged;
            }

            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        private void CreateProgrammaticUI()
        {
            var canvasGo = new GameObject("UnitInfoCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            _panelRoot = new GameObject("InfoPanel_BG");
            _panelRoot.transform.SetParent(canvasGo.transform, false);
            var bgImg = _panelRoot.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            var bgRect = _panelRoot.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(1f, 0f);
            bgRect.anchorMax = new Vector2(1f, 0f);
            bgRect.pivot = new Vector2(1f, 0f);
            bgRect.anchoredPosition = new Vector2(-20f, 20f);
            bgRect.sizeDelta = new Vector2(250f, 200f);

            var layout = _panelRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 15, 15);
            layout.spacing = 10f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            
            _nameText = CreateTextElement("NameText", _panelRoot.transform, 24, FontStyle.Bold);
            _hpText = CreateTextElement("HPText", _panelRoot.transform, 18, FontStyle.Normal);
            _hpText.color = Color.green;
            _atkText = CreateTextElement("AtkText", _panelRoot.transform, 18, FontStyle.Normal);
            _weightText = CreateTextElement("WeightText", _panelRoot.transform, 18, FontStyle.Normal);
            _moveRangeText = CreateTextElement("MoveRangeText", _panelRoot.transform, 18, FontStyle.Normal);
        }

        private Text CreateTextElement(string name, Transform parent, int fontSize, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 25f);
            return text;
        }

        private void OnDestroy()
        {
            if (_inputController != null)
            {
                _inputController.OnSelectionChanged -= HandleSelectionChanged;
            }
            UnsubscribeHealth();
        }

        private void HandleSelectionChanged(UnitBrain selectedUnit)
        {
            _currentUnit = selectedUnit;

            if (_currentUnit == null)
            {
                if (_panelRoot != null) _panelRoot.SetActive(false);
                UnsubscribeHealth();
                return;
            }

            if (_panelRoot != null) _panelRoot.SetActive(true);

            UpdateStaticInfo();
            SubscribeHealth();
            UpdateHealthUI(_currentHealth.CurrentHealth, _currentHealth.MaxHealth);
        }

        private void SubscribeHealth()
        {
            UnsubscribeHealth();
            if (_currentUnit != null)
            {
                _currentHealth = _currentUnit.GetComponent<HealthComponent>();
                if (_currentHealth != null)
                {
                    _currentHealth.OnHealthChanged += UpdateHealthUI;
                }
            }
        }

        private void UnsubscribeHealth()
        {
            if (_currentHealth != null)
            {
                _currentHealth.OnHealthChanged -= UpdateHealthUI;
                _currentHealth = null;
            }
        }

        private void UpdateStaticInfo()
        {
            if (_nameText != null) _nameText.text = _currentUnit.name;

            if (_currentUnit.UnitData != null)
            {
                if (_atkText != null) _atkText.text = $"ATK: {_currentUnit.UnitData.AttackDamage}";
            }

            var movement = _currentUnit.GetComponent<MovementComponent>();
            if (movement != null)
            {
                if (_weightText != null) _weightText.text = $"Weight: {movement.Weight}";
                // Optional: We can't easily get 'Move Range' as an int if it relies on complex pathing, but we can if it's in UnitData.
                if (_moveRangeText != null && _currentUnit.UnitData != null) 
                    _moveRangeText.text = $"Move Rng: {_currentUnit.UnitData.MoveRange}";
            }
        }

        private void UpdateHealthUI(float current, float max)
        {
            if (_hpText != null)
            {
                _hpText.text = $"HP: {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }
    }
}
