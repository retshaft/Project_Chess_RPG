using UnityEngine;
using UnityEngine.UI;
using CheckmateRPG.Components;

namespace CheckmateRPG.UI
{
    public class StatusReadabilityUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Text _weightText;

        [Header("Settings")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 2.6f, 0f);
        [SerializeField] private bool _billboard = true;

        private MovementComponent _movement;
        private Camera _mainCamera;
        private Transform _target;

        public void Initialize(Transform targetUnit)
        {
            _target = targetUnit;
            _movement = _target.GetComponent<MovementComponent>();
            _mainCamera = Camera.main;
            
            if (_weightText == null)
                CreateProgrammaticUI();
            
            UpdateWeightText();
        }

        private void CreateProgrammaticUI()
        {
            var canvasGo = new GameObject("ReadabilityCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 0.5f);

            var textGo = new GameObject("WeightText");
            textGo.transform.SetParent(canvasGo.transform, false);
            _weightText = textGo.AddComponent<Text>();
            _weightText.alignment = TextAnchor.MiddleCenter;
            _weightText.fontSize = 40;
            // Scale down the text slightly for world space
            textGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            
            // Basic font fallback
            _weightText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _weightText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _weightText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void UpdateWeightText()
        {
            if (_weightText == null || _movement == null) return;

            int weight = _movement.Weight;
            _weightText.text = weight.ToString();

            // Basic color coding for temporary visual distinction
            if (weight <= 1) _weightText.color = Color.white;          // Feather
            else if (weight == 2) _weightText.color = Color.gray;      // Iron
            else if (weight == 3) _weightText.color = new Color(0.6f, 0.3f, 0.1f); // Rock
            else _weightText.color = new Color(0.8f, 0.1f, 0.8f);      // Mountain / Super Heavy
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            // Follow position
            transform.position = _target.position + _offset;

            // Billboarding
            if (_billboard && _mainCamera != null)
            {
                transform.forward = _mainCamera.transform.forward;
            }
        }
    }
}
