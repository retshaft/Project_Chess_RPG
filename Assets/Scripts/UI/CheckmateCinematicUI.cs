using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CheckmateRPG.UI
{
    public class CheckmateCinematicUI : MonoBehaviour
    {
        private static CheckmateCinematicUI _instance;
        public static CheckmateCinematicUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CheckmateCinematicUI_Auto");
                    _instance = go.AddComponent<CheckmateCinematicUI>();
                    _instance.BuildUI();
                }
                return _instance;
            }
        }

        private GameObject _rootPanel;
        private RectTransform _textRect;
        private TextMeshProUGUI _checkmateText;
        private TextMeshProUGUI _subText;
        private Image _bgFader;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (_rootPanel == null) BuildUI();
        }

        public void PlayCheckmate(Action onCompleted)
        {
            StartCoroutine(SequenceRoutine(onCompleted));
        }

        private IEnumerator SequenceRoutine(Action onCompleted)
        {
            _rootPanel.SetActive(true);
            float origTimeScale = Time.timeScale;
            Time.timeScale = 0.05f; // Time Stop

            Camera mainCam = Camera.main;
            float origFOV = mainCam != null ? mainCam.fieldOfView : 60f;
            float targetFOV = Mathf.Max(30f, origFOV - 20f);
            
            // 1. Initial Impact (0.15s realtime)
            float elapsed = 0f;
            float phase1Duration = 0.15f;
            
            if (_slashRect != null)
            {
                _slashRect.gameObject.SetActive(true);
                _slashRect.localScale = new Vector3(0f, 1f, 1f);
                var slashImg = _slashRect.GetComponent<Image>();
                if (slashImg != null) slashImg.color = new Color(1f, 1f, 1f, 1f);
            }
            
            if (_textRect != null)
            {
                _textRect.gameObject.SetActive(true);
                _textRect.localScale = new Vector3(3f, 3f, 1f);
                _checkmateText.color = new Color(1f, 0.2f, 0.29f, 0f);
            }

            while (elapsed < phase1Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / phase1Duration);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);
                float easeIn = 1f - Mathf.Cos(t * Mathf.PI * 0.5f);

                if (_bgFader != null) 
                    _bgFader.color = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.85f, t));
                
                if (mainCam != null && !mainCam.orthographic) 
                    mainCam.fieldOfView = Mathf.Lerp(origFOV, targetFOV, easeOut);
                
                if (_slashRect != null) 
                    _slashRect.localScale = new Vector3(Mathf.Lerp(0f, 1f, easeOut), 1f, 1f);
                
                if (_textRect != null)
                {
                    _textRect.localScale = Vector3.Lerp(new Vector3(3f, 3f, 1f), Vector3.one, easeIn);
                    _checkmateText.color = new Color(1f, 0.2f, 0.29f, Mathf.Lerp(0f, 1f, t));
                }

                yield return null;
            }
            
            // Text scale punch
            if (_textRect != null)
            {
                elapsed = 0f;
                float punchDuration = 0.3f;
                while (elapsed < punchDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / punchDuration);
                    float punch = Mathf.Sin(t * Mathf.PI * 10f) * (1f - t) * 0.2f;
                    _textRect.localScale = Vector3.one + new Vector3(punch, punch, 0f);
                    
                    if (_slashRect != null)
                    {
                        var slashImg = _slashRect.GetComponent<Image>();
                        if (slashImg != null)
                            slashImg.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t));
                    }
                    
                    yield return null;
                }
                _textRect.localScale = Vector3.one;
            }

            yield return new WaitForSecondsRealtime(0.4f);

            // Sub text fade in
            if (_subText != null)
            {
                _subText.gameObject.SetActive(true);
                _subText.color = new Color(0.9f, 0.9f, 0.9f, 0f);
                elapsed = 0f;
                while (elapsed < 0.3f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float alpha = Mathf.Lerp(0f, 1f, elapsed / 0.3f);
                    Color c = _subText.color; c.a = alpha;
                    _subText.color = c;
                    yield return null;
                }
            }

            // 여운을 주는 슬로우 정지 시간
            yield return new WaitForSecondsRealtime(1.8f);

            // 시간 척도 및 카메라 원상 복구
            Time.timeScale = origTimeScale;
            if (mainCam != null && !mainCam.orthographic)
            {
                elapsed = 0f;
                while (elapsed < 0.3f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    mainCam.fieldOfView = Mathf.Lerp(targetFOV, origFOV, elapsed / 0.3f);
                    yield return null;
                }
                mainCam.fieldOfView = origFOV;
            }

            _rootPanel.SetActive(false);
            if (_slashRect != null) _slashRect.gameObject.SetActive(false);
            if (_textRect != null) _textRect.gameObject.SetActive(false);
            if (_subText != null) _subText.gameObject.SetActive(false);

            onCompleted?.Invoke();
        }

        private RectTransform _slashRect;

        private void BuildUI()
        {
            var canvasGO = new GameObject("CheckmateCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; 
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            _rootPanel = new GameObject("CheckmatePanel");
            _rootPanel.transform.SetParent(canvasGO.transform, false);
            var panelRect = _rootPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;
            _bgFader = _rootPanel.AddComponent<Image>();
            _bgFader.color = new Color(0f, 0f, 0f, 0f);

            // 사선 연출 그래픽
            var slashGO = new GameObject("SlashLine");
            slashGO.transform.SetParent(_rootPanel.transform, false);
            _slashRect = slashGO.AddComponent<RectTransform>();
            _slashRect.anchorMin = new Vector2(0.5f, 0.5f);
            _slashRect.anchorMax = new Vector2(0.5f, 0.5f);
            _slashRect.sizeDelta = new Vector2(3000f, 150f);
            _slashRect.localRotation = Quaternion.Euler(0, 0, -15f); // 15도 사선
            var slashImg = slashGO.AddComponent<Image>();
            slashImg.color = new Color(1f, 1f, 1f, 1f); // White flash
            slashGO.SetActive(false);

            var textGO = new GameObject("MainText");
            textGO.transform.SetParent(_rootPanel.transform, false);
            _textRect = textGO.AddComponent<RectTransform>();
            _textRect.anchorMin = new Vector2(0.1f, 0.4f);
            _textRect.anchorMax = new Vector2(0.9f, 0.65f);
            _textRect.offsetMin = Vector2.zero;
            _textRect.offsetMax = Vector2.zero;
            _checkmateText = textGO.AddComponent<TextMeshProUGUI>();
            _checkmateText.text = "C H E C K M A T E";
            _checkmateText.fontSize = 96f;
            _checkmateText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _checkmateText.color = new Color(1f, 0.2f, 0.29f, 1f); // Accent_Warning_Red
            _checkmateText.alignment = TextAlignmentOptions.Center;
            _checkmateText.enableWordWrapping = false;
            textGO.SetActive(false);

            var subGO = new GameObject("SubText");
            subGO.transform.SetParent(_rootPanel.transform, false);
            var subRect = subGO.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.2f, 0.3f);
            subRect.anchorMax = new Vector2(0.8f, 0.4f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;
            _subText = subGO.AddComponent<TextMeshProUGUI>();
            _subText.text = "- TARGET COMMANDER ANNIHILATED -";
            _subText.fontSize = 32f;
            _subText.fontStyle = FontStyles.Italic;
            _subText.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
            _subText.alignment = TextAlignmentOptions.Center;
            _subText.gameObject.SetActive(false);

            _rootPanel.SetActive(false);
        }
    }
}
