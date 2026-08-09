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
            Time.timeScale = 0.15f; // 극적 슬로우 모션

            // 메인 카메라 줌인 효과
            Camera mainCam = Camera.main;
            float origFOV = mainCam != null ? mainCam.fieldOfView : 60f;
            float targetFOV = Mathf.Max(30f, origFOV - 15f);

            float elapsed = 0f;
            float duration = 0.45f; // Realtime
            Vector3 origTextScale = new Vector3(2.5f, 2.5f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                // 배경 암전
                if (_bgFader != null) _bgFader.color = new Color(0f, 0f, 0f, t * 0.75f);
                
                // 텍스트 강렬한 스턴 줌 (Scale down with impact)
                float scaleT = Mathf.Sin(t * Mathf.PI * 0.5f);
                if (_textRect != null) _textRect.localScale = Vector3.Lerp(origTextScale, Vector3.one, scaleT);
                
                // 카메라 줌
                if (mainCam != null && mainCam.orthographic == false)
                {
                    mainCam.fieldOfView = Mathf.Lerp(origFOV, targetFOV, scaleT);
                }

                yield return null;
            }

            if (_textRect != null) _textRect.localScale = Vector3.one;
            if (_subText != null) _subText.gameObject.SetActive(true);

            // 여운을 주는 슬로우 정지 시간 (1.35초)
            yield return new WaitForSecondsRealtime(1.35f);

            // 시간 척도 및 카메라 원상 복구 (결산 창 이동을 위해)
            Time.timeScale = origTimeScale;
            if (mainCam != null) mainCam.fieldOfView = origFOV;

            _rootPanel.SetActive(false);
            onCompleted?.Invoke();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("CheckmateCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; // 결산 창보다 위쪽 혹은 동일 층위에서 선표시
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

            var textGO = new GameObject("MainText");
            textGO.transform.SetParent(_rootPanel.transform, false);
            _textRect = textGO.AddComponent<RectTransform>();
            _textRect.anchorMin = new Vector2(0.1f, 0.4f);
            _textRect.anchorMax = new Vector2(0.9f, 0.65f);
            _textRect.offsetMin = Vector2.zero;
            _textRect.offsetMax = Vector2.zero;
            _checkmateText = textGO.AddComponent<TextMeshProUGUI>();
            _checkmateText.text = "C H E C K M A T E";
            _checkmateText.fontSize = 84f;
            _checkmateText.fontStyle = FontStyles.Bold;
            _checkmateText.color = new Color(1f, 0.15f, 0.15f, 1f); // Red alert tone
            _checkmateText.alignment = TextAlignmentOptions.Center;
            _checkmateText.enableWordWrapping = false;

            var subGO = new GameObject("SubText");
            subGO.transform.SetParent(_rootPanel.transform, false);
            var subRect = subGO.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.2f, 0.3f);
            subRect.anchorMax = new Vector2(0.8f, 0.4f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;
            _subText = subGO.AddComponent<TextMeshProUGUI>();
            _subText.text = "- TARGET COMMANDER ANNIHILATED -";
            _subText.fontSize = 28f;
            _subText.fontStyle = FontStyles.Italic;
            _subText.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
            _subText.alignment = TextAlignmentOptions.Center;
            _subText.gameObject.SetActive(false);

            _rootPanel.SetActive(false);
        }
    }
}
