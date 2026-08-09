using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CheckmateRPG.Data;

namespace CheckmateRPG.Testing.UI
{
    [RequireComponent(typeof(Button))]
    public class DraggableDeploymentButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private UnitData _unitData;
        private CheckmateRPG.Core.BattleManager _battleManager;
        private Button _button;
        
        private GameObject _ghostUI;
        private RectTransform _ghostRect;

        public void Init(UnitData unit, CheckmateRPG.Core.BattleManager battleManager)
        {
            _unitData = unit;
            _battleManager = battleManager;
            _button = GetComponent<Button>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_button.interactable || _unitData == null || _battleManager == null)
                return;

            if (!_battleManager.CanAffordDeployment(_unitData))
            {
                Debug.Log($"[Deployment] Cannot afford {_unitData.UnitName}. Need {_unitData.DeploymentCost}.");
                return;
            }

            // Create Ghost UI
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _ghostUI = new GameObject("DeploymentGhostUI");
                _ghostUI.transform.SetParent(canvas.transform, false);
                _ghostUI.transform.SetAsLastSibling();

                _ghostRect = _ghostUI.AddComponent<RectTransform>();
                _ghostRect.sizeDelta = new Vector2(100f, 48f);
                
                Image img = _ghostUI.AddComponent<Image>();
                img.color = new Color(0.3f, 0.3f, 0.4f, 0.7f);
                img.raycastTarget = false;

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(_ghostUI.transform, false);
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                Text text = textObj.AddComponent<Text>();
                text.text = _unitData.UnitName;
                text.alignment = TextAnchor.MiddleCenter;
                text.fontSize = 20;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.color = new Color(1f, 1f, 1f, 0.7f);
                text.raycastTarget = false;

                UpdateGhostPosition(eventData);
            }

            _battleManager.OnDeployDragStart(_unitData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghostUI == null || _battleManager == null) return;

            RectTransform panelRect = _battleManager.DeploymentPanelRect;
            bool isOutsideUI = true;

            if (panelRect != null)
            {
                isOutsideUI = !RectTransformUtility.RectangleContainsScreenPoint(panelRect, eventData.position, eventData.pressEventCamera);
            }

            _ghostUI.SetActive(!isOutsideUI);

            if (!isOutsideUI)
            {
                UpdateGhostPosition(eventData);
            }

            _battleManager.OnDeployDragUpdate(_unitData, eventData.position, isOutsideUI);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghostUI != null)
            {
                Destroy(_ghostUI);
                _ghostUI = null;
            }

            if (!_button.interactable || _unitData == null || _battleManager == null)
                return;

            _battleManager.OnDeployDragEnd(_unitData, eventData.position, _button);
        }

        private void UpdateGhostPosition(PointerEventData eventData)
        {
            if (_ghostRect != null)
            {
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _ghostRect.parent as RectTransform, 
                    eventData.position, 
                    eventData.pressEventCamera, 
                    out localPos);
                _ghostRect.anchoredPosition = localPos;
            }
        }
    }
}

