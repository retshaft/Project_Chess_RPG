using System.Collections;
using UnityEngine;
using TMPro;

namespace CheckmateRPG.UI
{
    public class FloatingText : MonoBehaviour
    {
        private TextMeshPro _tmpro;
        private Camera _mainCamera;

        public void Initialize(string text, Color color, float duration = 0.8f, float maxScale = 1.5f)
        {
            if (_tmpro == null)
            {
                _tmpro = GetComponent<TextMeshPro>();
                if (_tmpro == null)
                {
                    _tmpro = gameObject.AddComponent<TextMeshPro>();
                    _tmpro.fontSize = 6f;
                    _tmpro.alignment = TextAlignmentOptions.Center;
                    var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                    if (font != null) _tmpro.font = font;
                }
            }

            _mainCamera = Camera.main;
            _tmpro.text = text;
            _tmpro.color = color;
            _tmpro.alpha = 1f;
            
            StartCoroutine(Animate(duration, maxScale));
        }

        private IEnumerator Animate(float duration, float maxScale)
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Color startColor = _tmpro.color;

            transform.localScale = Vector3.one * maxScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Pop & Settle scaling
                float scaleT = Mathf.Clamp01(elapsed / 0.15f);
                float currentScale = Mathf.Lerp(maxScale, 1.0f, Mathf.Sin(scaleT * Mathf.PI * 0.5f));
                transform.localScale = Vector3.one * currentScale;

                // Smooth upward curve
                float moveT = Mathf.Sin(t * Mathf.PI * 0.5f);
                transform.position = startPos + Vector3.up * (moveT * 1.8f);
                
                if (_mainCamera != null)
                {
                    transform.forward = _mainCamera.transform.forward;
                }

                // Smooth alpha fade out in latter half
                Color c = startColor;
                c.a = t > 0.5f ? 1f - ((t - 0.5f) * 2f) : 1f;
                _tmpro.color = c;

                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}
