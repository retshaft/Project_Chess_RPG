using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Data;

namespace CheckmateRPG.UI
{
    public class RecruitResultUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Transform gridContainer;
        [SerializeField] private Button confirmButton;

        private readonly List<GameObject> createdCards = new List<GameObject>();

        private void Awake()
        {
            if (confirmButton) confirmButton.onClick.AddListener(HideResults);
            if (resultPanel) resultPanel.SetActive(false);
        }

        public void ShowResults(List<UnitData> pulledUnits)
        {
            if (resultPanel) resultPanel.SetActive(true);

            // Clear previous cards
            foreach (var card in createdCards)
            {
                Destroy(card);
            }
            createdCards.Clear();

            if (gridContainer == null || pulledUnits == null) return;

            // Generate UI Cards dynamically
            foreach (var unit in pulledUnits)
            {
                GameObject card = CreateUnitCard(unit);
                if (card != null)
                {
                    card.transform.SetParent(gridContainer, false);
                    createdCards.Add(card);
                }
            }
        }

        public void HideResults()
        {
            if (resultPanel) resultPanel.SetActive(false);
        }

        private GameObject CreateUnitCard(UnitData unit)
        {
            GameObject obj = new GameObject(unit != null ? unit.UnitName : "UnknownUnit");
            var img = obj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.35f, 1f); // Stylish dark blue-grey card background

            // Layout scaling constraints
            var layoutElement = obj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 160;
            layoutElement.preferredHeight = 220;

            // Name & Role Text
            GameObject textObj = new GameObject("UnitText");
            textObj.transform.SetParent(obj.transform, false);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = unit != null ? $"<b>{unit.UnitName}</b>\n<size=75%>{unit.BaseRole}</size>" : "???";
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            var rect = tmp.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(5, 5);
            rect.offsetMax = new Vector2(-5, -5);

            return obj;
        }
    }
}
