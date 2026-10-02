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
            var cost = TacticalLayout.Label(transform, "FormationUnitCost",
                unit != null ? $"COST  {unit.FormationCost}" : "", 18,
                OutgameStyle.TextWhite, .08f, .89f, .84f, .09f);
            cost.alignment = TextAlignmentOptions.MidlineRight;
            cost.overflowMode = TextOverflowModes.Overflow;
            if (unit != null)
            {
                if (_unitNameText != null) 
                {
                    string roleTag = unit.PieceType == ChessPieceType.King ? "<color=#D7BB79>[KING]</color>" : $"<color=#C9D4D0>[{unit.PieceType}]</color>";
                    _unitNameText.text = $"{roleTag}\n<b>{unit.UnitName}</b>";
                    _unitNameText.fontSize = 23;
                    TacticalLayout.Place(_unitNameText.transform,.06f,.05f,.88f,.28f);
                    _unitNameText.alignment = TextAlignmentOptions.Center;
                }
                if (_unitIcon != null) 
                {
                    _unitIcon.gameObject.SetActive(unit.Icon != null);
                    _unitIcon.sprite = unit.Icon;
                    TacticalLayout.Place(_unitIcon.transform,.08f,.36f,.84f,.52f);
                    _unitIcon.preserveAspect = true;
                    _unitIcon.raycastTarget = false;
                    if (unit.Icon != null)
                        _unitIcon.sprite = unit.Icon;
                }
            }
            else
            {
                if (_unitNameText != null) 
                {
                    _unitNameText.text = "미배치";
                    _unitNameText.fontSize = 20;
                    TacticalLayout.Place(_unitNameText.transform,.06f,.05f,.88f,.28f);
                    _unitNameText.alignment = TextAlignmentOptions.Center;
                }
                if (_unitIcon != null) 
                {
                    _unitIcon.gameObject.SetActive(false);
                    _unitIcon.sprite = null;
                }
            }
            
            var symbol = TacticalLayout.Label(transform,"PieceMark",unit != null ? (unit.PieceType == ChessPieceType.Knight ? "N" : unit.PieceType.ToString().Substring(0,1)) : "+",76,OutgameStyle.TextDim,.08f,.38f,.84f,.46f);
            symbol.alignment = TextAlignmentOptions.Center;
            symbol.gameObject.SetActive(unit == null || unit.Icon == null);
            TacticalLayout.Panel(transform,"RoleRule",unit != null && unit.PieceType == ChessPieceType.King ? OutgameStyle.AccentGold : OutgameStyle.BorderSubtle,.06f,.32f,.88f,.009f);
            // Action button must always be active to allow clicking empty slots
            if (_actionButton != null)
            {
                _actionButton.gameObject.SetActive(true);
                OutgameStyle.ApplyPrimaryButtonStyle(_actionButton,_unitNameText);
                var btnImg = _actionButton.GetComponent<Image>();
                if (btnImg != null)
                {
                    btnImg.color = Color.white;
                }
            }
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
