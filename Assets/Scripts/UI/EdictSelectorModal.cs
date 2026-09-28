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
        private Func<EdictData, string> selectionDescription;
        private readonly List<GameObject> activeCards = new List<GameObject>();

        private void Awake()
        {
            UiFontProvider.EnsureTmpKoreanFallback();
            if (closeButton) closeButton.onClick.AddListener(CloseModal);
            if (modalPanel) modalPanel.SetActive(false);
        }

        /// <summary>
        /// 씬/빌더에서 참조가 주입되지 않은 경우 표준 선택창을 스스로 구성한다(부분 주입도 필드별로 보수한다).
        /// SynchroBoardUI의 런타임 자기 구성 경로에서 호출된다.
        /// </summary>
        public void EnsureRuntimeUI()
        {
            if (modalPanel != null && windowTitleText != null && cardGridContainer != null && closeButton != null)
                return;

            if (modalPanel == null)
            {
                var panelGo = new GameObject("ModalPanel", typeof(RectTransform));
                panelGo.transform.SetParent(transform, false);
                var panelRt = (RectTransform)panelGo.transform;
                panelRt.anchorMin = Vector2.zero;
                panelRt.anchorMax = Vector2.one;
                panelRt.sizeDelta = Vector2.zero;
                panelRt.anchoredPosition = Vector2.zero;
                var dim = panelGo.AddComponent<Image>();
                dim.color = new Color(0f, 0f, 0f, 0.86f); // 배경 클릭 차단 + 딤
                modalPanel = panelGo;

                var windowGo = new GameObject("Window", typeof(RectTransform));
                windowGo.transform.SetParent(panelGo.transform, false);
                var windowRt = (RectTransform)windowGo.transform;
                windowRt.anchorMin = windowRt.anchorMax = new Vector2(0.5f, 0.5f);
                windowRt.sizeDelta = new Vector2(1150f, 780f);
                var windowImg = windowGo.AddComponent<Image>();
                windowImg.color = OutgameStyle.SurfaceDark;

                var titleGo = new GameObject("WindowTitle", typeof(RectTransform));
                titleGo.transform.SetParent(windowGo.transform, false);
                var titleRt = (RectTransform)titleGo.transform;
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.sizeDelta = new Vector2(-40f, 70f);
                titleRt.anchoredPosition = new Vector2(0f, -18f);
                windowTitleText = titleGo.AddComponent<TextMeshProUGUI>();
                windowTitleText.fontSize = 30;
                windowTitleText.alignment = TextAlignmentOptions.MidlineLeft;
                titleRt.offsetMin = new Vector2(32f,-88f); titleRt.offsetMax = new Vector2(-230f,-18f);
                windowTitleText.raycastTarget = false;

                var viewport = TacticalLayout.Panel(windowGo.transform,"CardViewport",OutgameStyle.SurfaceDark,.022f,.04f,.956f,.80f);
                viewport.raycastTarget = true;
                viewport.gameObject.AddComponent<RectMask2D>();
                var gridGo = new GameObject("CardGrid", typeof(RectTransform));
                gridGo.transform.SetParent(viewport.transform, false);
                var gridRt = (RectTransform)gridGo.transform;
                gridRt.anchorMin = new Vector2(0,1);
                gridRt.anchorMax = Vector2.one;
                gridRt.pivot = new Vector2(.5f,1);
                gridRt.sizeDelta = Vector2.zero;
                cardGridContainer = gridGo.transform;
                var grid = gridGo.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(202f, 286f);
                grid.spacing = new Vector2(16f, 16f);
                grid.childAlignment = TextAnchor.UpperCenter;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 5;
                gridGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport.rectTransform; scroll.content = gridRt;
                scroll.horizontal = false; scroll.scrollSensitivity = 35;
                scroll.movementType = ScrollRect.MovementType.Clamped;

                closeButton = CreateModalButton(windowGo.transform, "CloseButton", "닫기", new Vector2(0f, -24f), new Vector2(160f, 50f));
            }

            if (closeButton != null)
            {
                TacticalLayout.Place(closeButton.transform,.815f,.9f,.15f,.065f);
                OutgameStyle.ApplyLightButtonStyle(closeButton);
                // Awake 시점에는 closeButton이 없어 리스너가 붙지 못했을 수 있다 — 생성 후 확실히 연결.
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(CloseModal);
            }

            if (modalPanel != null && modalPanel.activeSelf)
                modalPanel.SetActive(false);
        }

        private static Button CreateModalButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.24f, 0.3f, 1f);
            var btn = go.AddComponent<Button>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return btn;
        }

        public void OpenSelector(EdictKind filterKind, Action<EdictData> onSelected, Func<EdictData, string> describe = null)
        {
            currentCallback = onSelected;
            selectionDescription = describe;
            if (modalPanel) modalPanel.SetActive(true);
            OutgameUIManager.Instance?.UnregisterModalCloseAction(CloseModal);
            OutgameUIManager.Instance?.RegisterModalCloseAction(CloseModal);
            if (windowTitleText) windowTitleText.text = filterKind == EdictKind.Absolute 
                ? "절대 칙령 선택 · 수용량과 효과 비교"
                : "일반 칙령 선택 · 슬롯 비용과 효과 비교";

            RefreshCardList(filterKind);
        }

        public void CloseModal()
        {
            if (modalPanel) modalPanel.SetActive(false);
            OutgameUIManager.Instance?.UnregisterModalCloseAction(CloseModal);
        }

        private void RefreshCardList(EdictKind filterKind)
        {
            foreach (var c in activeCards) { c.SetActive(false); Destroy(c); }
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
            img.color = OutgameStyle.SurfaceCard;
            OutgameStyle.ApplyPrimaryButtonStyle(btn);

            var layout = obj.AddComponent<LayoutElement>();
            layout.preferredWidth = 180;
            layout.preferredHeight = 250;

            // Top Header: Cost & Polarity
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(obj.transform, false);
            var headerTmp = headerObj.AddComponent<TextMeshProUGUI>();
            headerTmp.text = edict != null 
                ? $"<color=#D7BB79>{(edict.Kind == EdictKind.Absolute ? "수용량 +" : "기본 비용 ")}{edict.SyncCost}</color> · <color=#C9D4D0>{SynchroBoardUI.PolarityLabel(edict.Polarity)}</color>"
                : "";
            headerTmp.fontSize = 18;
            headerTmp.raycastTarget = false;
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
                bodyTmp.text = $"<color=#B0B0B0><b>장착 해제</b>\n슬롯을 비웁니다.</color>\n\n<size=70%>{selectionDescription?.Invoke(null)}</size>";
            }
            else
            {
                string bonusStr = "";
                if (edict.Bonuses != null)
                {
                    foreach (var b in edict.Bonuses)
                    {
                        if (b.FlatBonus != 0) bonusStr += $"{StatLabel(b.Stat)} {b.FlatBonus:+0.##;-0.##;0}\n";
                        if (b.PercentBonus != 0) bonusStr += $"{StatLabel(b.Stat)} {b.PercentBonus * 100f:+0.##;-0.##;0}%\n";
                    }
                }
                bodyTmp.text = $"<b>{edict.EdictName}</b>\n<size=80%>{bonusStr}</size>\n<size=65%>{selectionDescription?.Invoke(edict)}</size>";
            }

            bodyTmp.fontSize = 20;
            bodyTmp.enableAutoSizing = true;
            bodyTmp.fontSizeMin = 12;
            bodyTmp.fontSizeMax = 20;
            bodyTmp.raycastTarget = false;
            bodyTmp.alignment = TextAlignmentOptions.Center;
            bodyTmp.color = Color.white;
            var bRect = bodyTmp.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = new Vector2(1f, 0.63f);
            bRect.offsetMin = new Vector2(10, 10);
            bRect.offsetMax = new Vector2(-10, -10);

            var frame = GetCardFrameColor(edict);
            TacticalLayout.Place(headerTmp.transform,.07f,.86f,.86f,.10f);
            headerTmp.fontSize = 14;
            headerTmp.font = UiFontProvider.GetTmpFont(); bodyTmp.font = UiFontProvider.GetTmpFont();
            TacticalLayout.Panel(obj.transform,"PolarityRule",frame,.025f,.98f,.95f,.01f);
            TacticalLayout.Panel(obj.transform,"LeftFrame",frame,.025f,.025f,.006f,.96f);
            TacticalLayout.Panel(obj.transform,"RightFrame",frame,.969f,.025f,.006f,.96f);
            TacticalLayout.Panel(obj.transform,"BottomFrame",frame,.025f,.025f,.95f,.01f);
            var glyph=TacticalLayout.Label(obj.transform,"CircuitGlyph",edict==null?"−":edict.Kind==EdictKind.Absolute?"K":edict.Polarity==PolarityType.Order?"/ /":edict.Polarity==PolarityType.Chaos?"×":"−",32,frame,.1f,.64f,.8f,.19f);
            glyph.alignment=TextAlignmentOptions.Center;glyph.overflowMode=TextOverflowModes.Overflow;
            TacticalLayout.Panel(obj.transform,"EffectRule",frame,.07f,.63f,.86f,.003f);
            return obj;
        }

        private static string StatLabel(MetaStatType stat) => stat switch
        {
            MetaStatType.MaxHealth => "최대 체력", MetaStatType.AttackDamage => "공격력",
            MetaStatType.Defense => "방어력", MetaStatType.Resistance => "마법 저항",
            MetaStatType.MoveRange => "이동 범위", MetaStatType.AttackRange => "공격 범위",
            MetaStatType.MoveCostAP => "이동 AP", MetaStatType.AttackCostAP => "공격 AP",
            MetaStatType.ActionSpeed => "행동 속도", MetaStatType.MaxSP => "최대 SP", _ => stat.ToString()
        };

        private Color GetCardFrameColor(EdictData edict)
        {
            if (edict == null) return OutgameStyle.BorderSubtle;
            if (edict.Kind == EdictKind.Absolute) return OutgameStyle.AccentGold;
            return edict.Polarity == PolarityType.Chaos ? OutgameStyle.AccentRed : edict.Polarity == PolarityType.Order ? OutgameStyle.AccentCyan : OutgameStyle.TextMuted;
        }
    }
}
