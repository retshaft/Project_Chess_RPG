using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    /// <summary>
    /// Warframe-style Edict Mod Inventory & Selector Window.
    /// Renders possessed Edicts as distinct mod cards with Drain Cost and Polarity badges.
    /// </summary>
    public class EdictSelectorModal : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject modalPanel;
        [SerializeField] private TextMeshProUGUI windowTitleText;
        [SerializeField] private Transform cardGridContainer;
        [SerializeField] private Button closeButton;

        private Action<EdictData> currentCallback;
        private readonly List<GameObject> activeCards = new List<GameObject>();

        private void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(CloseModal);
            if (modalPanel) modalPanel.SetActive(false);
        }

        public void OpenSelector(EdictKind filterKind, Action<EdictData> onSelected)
        {
            currentCallback = onSelected;
            if (modalPanel) modalPanel.SetActive(true);
            if (windowTitleText) windowTitleText.text = filterKind == EdictKind.Absolute 
                ? "SELECT ABSOLUTE EDICT (AURA/COMMAND)" 
                : "SELECT GENERAL EDICT (MOD CIRCUIT)";

            RefreshCardList(filterKind);
        }

        public void CloseModal()
        {
            if (modalPanel) modalPanel.SetActive(false);
        }

        private void RefreshCardList(EdictKind filterKind)
        {
            foreach (var c in activeCards) Destroy(c);
            activeCards.Clear();

            if (cardGridContainer == null) return;

            // 1. First item is always "UNEQUIP / EMPTY"
            GameObject emptyCard = CreateModCard(null, () => OnCardClicked(null));
            emptyCard.transform.SetParent(cardGridContainer, false);
            activeCards.Add(emptyCard);

            // 2. Load matching Edicts from DeckManager Database
            if (DeckManager.Instance == null || DeckManager.Instance.AllEdicts == null) return;

            foreach (var edict in DeckManager.Instance.AllEdicts)
            {
                if (edict != null && edict.Kind == filterKind)
                {
                    GameObject card = CreateModCard(edict, () => OnCardClicked(edict));
                    card.transform.SetParent(cardGridContainer, false);
                    activeCards.Add(card);
                }
            }
        }

        private void OnCardClicked(EdictData selectedEdict)
        {
            currentCallback?.Invoke(selectedEdict);
            CloseModal();
        }

        /// <summary>
        /// Generates a Warframe-styled Mod Card UI object.
        /// </summary>
        private GameObject CreateModCard(EdictData edict, Action onClick)
        {
            GameObject obj = new GameObject(edict != null ? edict.EdictName : "EmptyCard");
            var btn = obj.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var img = obj.AddComponent<Image>();
            img.color = GetCardFrameColor(edict);

            var layout = obj.AddComponent<LayoutElement>();
            layout.preferredWidth = 180;
            layout.preferredHeight = 250;

            // Top Header: Cost & Polarity
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(obj.transform, false);
            var headerTmp = headerObj.AddComponent<TextMeshProUGUI>();
            headerTmp.text = edict != null 
                ? $"<color=yellow>[{edict.SyncCost}]</color>                 <b><color=#00E5FF>{edict.Polarity}</color></b>" 
                : "";
            headerTmp.fontSize = 18;
            headerTmp.alignment = TextAlignmentOptions.TopLeft;
            var hRect = headerTmp.GetComponent<RectTransform>();
            hRect.anchorMin = Vector2.zero;
            hRect.anchorMax = Vector2.one;
            hRect.offsetMin = new Vector2(10, 10);
            hRect.offsetMax = new Vector2(-10, -10);

            // Body: Title & Stat Bonuses
            GameObject bodyObj = new GameObject("BodyText");
            bodyObj.transform.SetParent(obj.transform, false);
            var bodyTmp = bodyObj.AddComponent<TextMeshProUGUI>();
            
            if (edict == null)
            {
                bodyTmp.text = "\n\n\n<color=#B0B0B0><b>[ UNEQUIP ]</b>\nClear Slot</color>";
            }
            else
            {
                string bonusStr = "";
                if (edict.Bonuses != null)
                {
                    foreach (var b in edict.Bonuses)
                    {
                        if (b.FlatBonus != 0) bonusStr += $"• {b.Stat} +{b.FlatBonus}\n";
                        if (b.PercentBonus != 0) bonusStr += $"• {b.Stat} +{b.PercentBonus * 100f}%\n";
                    }
                }
                bodyTmp.text = $"\n<b><size=115%>{edict.EdictName}</size></b>\n<color=#D7D7D7><size=75%>{edict.Kind} Circuit</size></color>\n\n<size=85%>{bonusStr}</size>";
            }

            bodyTmp.fontSize = 20;
            bodyTmp.alignment = TextAlignmentOptions.Center;
            bodyTmp.color = Color.white;
            var bRect = bodyTmp.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.offsetMin = new Vector2(10, 10);
            bRect.offsetMax = new Vector2(-10, -10);

            return obj;
        }

        private Color GetCardFrameColor(EdictData edict)
        {
            if (edict == null) return new Color(0.2f, 0.2f, 0.22f, 1f); // Muted grey for unequip
            if (edict.Kind == EdictKind.Absolute) return new Color(0.35f, 0.28f, 0.1f, 1f); // Gold/Bronze Aura style
            
            return edict.Polarity switch
            {
                PolarityType.Order => new Color(0.15f, 0.25f, 0.45f, 1f),  // Deep blue order circuit
                PolarityType.Chaos => new Color(0.45f, 0.15f, 0.18f, 1f),  // Crimson chaos circuit
                _ => new Color(0.25f, 0.25f, 0.28f, 1f)                    // Neutral metallic
            };
        }
    }
}
