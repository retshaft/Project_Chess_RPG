using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using CheckmateRPG.Data;
using System;

namespace CheckmateRPG.UI
{
    public class RosterSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _unitIcon;
        [SerializeField] private TextMeshProUGUI _unitNameText;
        [SerializeField] private Button _actionButton;
        
        public UnitData CurrentUnit { get; private set; }
        public int SlotIndex { get; private set; }
        
        // Changed signature to pass the slot itself for context
        public event Action<RosterSlotUI> OnActionClicked;

        private Vector3 _originalScale = Vector3.one;

        private void Awake()
        {
            if (_actionButton != null)
                _actionButton.onClick.AddListener(() => OnActionClicked?.Invoke(this));
            
            _originalScale = transform.localScale;
        }

        public void SetSlotIndex(int index)
        {
            SlotIndex = index;
        }

        public void SetUnit(UnitData unit)
        {
            CurrentUnit = unit;
            if (unit != null)
            {
                if (_unitNameText != null) 
                {
                    _unitNameText.text = unit.UnitName;
                    _unitNameText.fontSize = 16;
                }
                if (_unitIcon != null) 
                {
                    _unitIcon.gameObject.SetActive(true);
                    if (unit.Icon != null)
                        _unitIcon.sprite = unit.Icon;
                }
            }
            else
            {
                if (_unitNameText != null) 
                {
                    _unitNameText.text = "+"; // Arknights style empty indicator
                    _unitNameText.fontSize = 40; // Make + larger
                }
                if (_unitIcon != null) 
                {
                    _unitIcon.gameObject.SetActive(false);
                    _unitIcon.sprite = null;
                }
            }
            
            // Action button must always be active to allow clicking empty slots
            if (_actionButton != null) _actionButton.gameObject.SetActive(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = _originalScale * 1.05f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = _originalScale;
        }
    }
}
