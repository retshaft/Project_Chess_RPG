using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CheckmateRPG.UI
{
    public class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Color _hoverOutlineColor = new Color(0f, 0.9f, 1f, 1f); // Accent_Cyan
        [SerializeField] private float _hoverScale = 1.02f;
        [SerializeField] private float _animationDuration = 0.15f;

        private Outline _outline;
        private Vector3 _originalScale;
        private Selectable _selectable;
        private Coroutine _scaleCoroutine;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _originalScale = transform.localScale;

            _outline = GetComponent<Outline>();
            if (_outline == null)
            {
                _outline = gameObject.AddComponent<Outline>();
                _outline.effectDistance = new Vector2(2, -2);
            }
            _outline.enabled = false;
        }

        private void ScaleTo(float targetScale, float duration)
        {
            if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleRoutine(targetScale, duration));
        }

        private IEnumerator ScaleRoutine(float targetScale, float duration)
        {
            Vector3 startScale = transform.localScale;
            Vector3 endScale = _originalScale * targetScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);
                transform.localScale = Vector3.Lerp(startScale, endScale, easeOut);
                yield return null;
            }
            transform.localScale = endScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.interactable) return;
            ScaleTo(_hoverScale, _animationDuration);
            if (_outline != null) { _outline.effectColor = _hoverOutlineColor; _outline.enabled = true; }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.interactable) return;
            ScaleTo(1f, _animationDuration);
            if (_outline != null) _outline.enabled = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.interactable) return;
            ScaleTo(0.98f, _animationDuration * 0.5f);
            if (_outline != null) _outline.effectColor = Color.white;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.interactable) return;
            ScaleTo(_hoverScale, _animationDuration * 0.5f);
            if (_outline != null) _outline.effectColor = _hoverOutlineColor;
        }
        
        private void OnDisable()
        {
            if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
            transform.localScale = _originalScale;
            if (_outline != null) _outline.enabled = false;
        }
    }
}
