using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CheckmateRPG.UI
{
    public class SkillCutInUI : MonoBehaviour
    {
        public static SkillCutInUI Instance { get; private set; }

        [Header("UI Elements")]
        [SerializeField] private GameObject _rootPanel;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _portraitImage;
        [SerializeField] private TextMeshProUGUI _charNameText;
        [SerializeField] private TextMeshProUGUI _skillNameText;
        [SerializeField] private RectTransform _bannerRect;

        [Header("Animation Settings")]
        [SerializeField] private float _slowdownScale = 0.25f;
        [SerializeField] private float _slowdownDurationRealtime = 0.35f;
        [SerializeField] private float _slideInDuration = 0.15f;
        [SerializeField] private float _slideOutDuration = 0.2f;

        private bool _isAnimating = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (_rootPanel != null) _rootPanel.SetActive(false);
            else BuildDefaultUI();
        }

        public void ShowCutIn(string charName, string skillName, Sprite portrait = null)
        {
            if (_isAnimating) return;
            StartCoroutine(PlayCutInRoutine(charName, skillName, portrait));
        }

        private IEnumerator PlayCutInRoutine(string charName, string skillName, Sprite portrait)
        {
            _isAnimating = true;
            if (_rootPanel != null) _rootPanel.SetActive(true);

            if (_charNameText != null) _charNameText.text = charName;
            if (_skillNameText != null) _skillNameText.text = skillName;
            if (_portraitImage != null)
            {
                _portraitImage.sprite = portrait;
                _portraitImage.enabled = portrait != null;
            }

            // 슬로우 모션 발동
            float origTimeScale = Time.timeScale;
            Time.timeScale = _slowdownScale;

            // 슬라이드 인 (Unscaled Time 사용)
            float elapsed = 0f;
            Vector2 startPos = new Vector2(-1200f, 0f);
            Vector2 targetPos = Vector2.zero;
            while (elapsed < _slideInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _slideInDuration);
                t = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease-out
                if (_bannerRect != null) _bannerRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }
            if (_bannerRect != null) _bannerRect.anchoredPosition = targetPos;

            // 슬로우 모션 지속 시간 대기 (실제 시간 기준)
            yield return new WaitForSecondsRealtime(_slowdownDurationRealtime);

            // 시간 척도 복구
            Time.timeScale = origTimeScale;

            // 슬라이드 아웃
            elapsed = 0f;
            Vector2 exitPos = new Vector2(1200f, 0f);
            while (elapsed < _slideOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _slideOutDuration);
                t = t * t; // Ease-in
                if (_bannerRect != null) _bannerRect.anchoredPosition = Vector2.Lerp(targetPos, exitPos, t);
                yield return null;
            }

            if (_rootPanel != null) _rootPanel.SetActive(false);
            _isAnimating = false;
        }

        private void BuildDefaultUI()
        {
            var canvasGO = new GameObject("SkillCutInCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            _rootPanel = new GameObject("CutInPanel");
            _rootPanel.transform.SetParent(canvasGO.transform, false);
            var panelRect = _rootPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0.35f);
            panelRect.anchorMax = new Vector2(1f, 0.65f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            _bannerRect = panelRect;

            var bgGO = new GameObject("Background Banner");
            bgGO.transform.SetParent(_rootPanel.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            _backgroundImage = bgGO.AddComponent<Image>();
            _backgroundImage.color = new Color(0.1f, 0.1f, 0.15f, 0.88f);

            // Portrait
            var portGO = new GameObject("Portrait");
            portGO.transform.SetParent(_rootPanel.transform, false);
            var portRect = portGO.AddComponent<RectTransform>();
            portRect.anchorMin = new Vector2(0.1f, 0f);
            portRect.anchorMax = new Vector2(0.35f, 1f);
            portRect.sizeDelta = Vector2.zero;
            _portraitImage = portGO.AddComponent<Image>();
            _portraitImage.preserveAspect = true;

            // Character Name Text
            var nameGO = new GameObject("CharName");
            nameGO.transform.SetParent(_rootPanel.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0.4f, 0.55f);
            nameRect.anchorMax = new Vector2(0.95f, 0.9f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            _charNameText = nameGO.AddComponent<TextMeshProUGUI>();
            _charNameText.fontSize = 40f;
            _charNameText.fontStyle = FontStyles.Bold;
            _charNameText.color = new Color(1f, 0.8f, 0.2f, 1f); // Gold/Orange
            _charNameText.alignment = TextAlignmentOptions.Left;

            // Skill Name Text
            var skillGO = new GameObject("SkillName");
            skillGO.transform.SetParent(_rootPanel.transform, false);
            var skillRect = skillGO.AddComponent<RectTransform>();
            skillRect.anchorMin = new Vector2(0.4f, 0.15f);
            skillRect.anchorMax = new Vector2(0.95f, 0.55f);
            skillRect.offsetMin = Vector2.zero;
            skillRect.offsetMax = Vector2.zero;
            _skillNameText = skillGO.AddComponent<TextMeshProUGUI>();
            _skillNameText.fontSize = 54f;
            _skillNameText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _skillNameText.color = Color.white;
            _skillNameText.alignment = TextAlignmentOptions.Left;

            _rootPanel.SetActive(false);
        }
    }
}
