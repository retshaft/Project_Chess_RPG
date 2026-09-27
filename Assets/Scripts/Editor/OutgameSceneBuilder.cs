#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using CheckmateRPG.UI;
using CheckmateRPG.Progression;

namespace CheckmateRPG.Editor
{
    public class OutgameSceneBuilder
    {
        [MenuItem("CheckmateRPG/Build Premium Outgame Scene (M6, M13 허브 셋업)")]
        [MenuItem("Tools/CheckmateRPG/Build Premium Outgame Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Setup Camera
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f); // Deep dark background
            cam.orthographic = true;
            camObj.tag = "MainCamera";

            // 2. Managers Setup (SaveManager, StageManager, DeckManager)
            if (Object.FindFirstObjectByType<SaveManager>() == null)
            {
                new GameObject("SaveManager").AddComponent<SaveManager>();
            }
            if (Object.FindFirstObjectByType<StageManager>() == null)
            {
                new GameObject("StageManager").AddComponent<StageManager>();
            }
            if (Object.FindFirstObjectByType<CheckmateRPG.Progression.DeckManager>() == null)
            {
                new GameObject("DeckManager").AddComponent<CheckmateRPG.Progression.DeckManager>();
            }

            // 3. Setup Canvas
            GameObject canvasObj = new GameObject("OutgameCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Event System
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 4. Create UIManager
            OutgameUIManager uiManager = canvasObj.AddComponent<OutgameUIManager>();
            var viewsField = typeof(OutgameUIManager).GetField("views", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var viewsList = new System.Collections.Generic.List<OutgameViewBase>();

            // 5. Build Views
            // --- Title View ---
            var titleView = CreatePanel<TitleUI>(canvasObj, "TitleView", OutgameViewType.Title);
            CreateDarkGlassBackground(titleView.gameObject, "CHECKMATE RPG", 120);
            var startBtn = CreateButton(titleView.gameObject, "StartButton", "SYSTEM START", new Vector2(0, -200), new Vector2(400, 100));
            typeof(TitleUI).GetField("startButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(titleView, startBtn);
            viewsList.Add(titleView);

            // --- Main Lobby View ---
            var lobbyView = CreatePanel<MainLobbyUI>(canvasObj, "MainLobbyView", OutgameViewType.MainLobby);
            CreateDarkGlassBackground(lobbyView.gameObject, "A R K : C H E C K M A T E", 80);

            // Lobby Menu Buttons (Arknights Cybernetic Layout)
            var stageSelectBtn = CreateButton(lobbyView.gameObject, "Btn_Combat", "02 // OPERATIONS\n<size=70%>작전 출격</size>", new Vector2(620, 100), new Vector2(420, 130), new Color(1f, 0.4f, 0.2f));
            var operatorHubBtn = CreateButton(lobbyView.gameObject, "Btn_OperatorHub", "03 // OPERATOR HUB\n<size=70%>오퍼레이터 관리</size>", new Vector2(620, -20), new Vector2(420, 85));
            var synchroBtn = CreateButton(lobbyView.gameObject, "Btn_Synchro", "04 // SYNCHRO MODDING\n<size=70%>킹 칙령 보드</size>", new Vector2(620, -120), new Vector2(420, 85));
            var rosterBtn = CreateButton(lobbyView.gameObject, "Btn_Roster", "05 // FORMATION\n<size=70%>부대 편성 현황</size>", new Vector2(620, -220), new Vector2(420, 85));
            var recruitBtn = CreateButton(lobbyView.gameObject, "Btn_Recruit", "08 // HEADHUNTING\n<size=70%>인재 채용</size>", new Vector2(620, -320), new Vector2(420, 85));
            var kingSuitBayBtn = CreateButton(lobbyView.gameObject, "Btn_KingSuitBay", "09 // KING'S SUIT\n<size=70%>슈트 격납고</size>", new Vector2(170, -220), new Vector2(380, 85));
            var archiveBtn = CreateButton(lobbyView.gameObject, "Btn_Archive", "10 // ARCHIVE\n<size=70%>작전 기록소</size>", new Vector2(170, -320), new Vector2(380, 85));

            // Assistant Operator Visual Frame & Dialogue Bubble
            var assistantCard = new GameObject("AssistantCard", typeof(RectTransform), typeof(Image));
            assistantCard.transform.SetParent(lobbyView.transform, false);
            var aRt = (RectTransform)assistantCard.transform;
            aRt.anchoredPosition = new Vector2(-280, -60);
            aRt.sizeDelta = new Vector2(460, 720);
            assistantCard.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.5f);

            var aBadge = CreateText(assistantCard, "AssistantBadge", "<color=#8A94A6>ASSISTANT OPERATOR</color>\n<b><color=#00E5FF>PRTS TACTICAL LIAISON</color></b>", 20, new Vector2(0, 310), TextAlignmentOptions.Center);
            aBadge.rectTransform.sizeDelta = new Vector2(420, 60);

            var secretaryArea = CreateButton(assistantCard, "SecretaryTouchArea", "<color=#4A5262>[ TOUCH TO COMMUNICATE ]</color>", new Vector2(0, -280), new Vector2(400, 50));
            secretaryArea.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            var speechPanel = new GameObject("SpeechBubblePanel", typeof(RectTransform), typeof(Image));
            speechPanel.transform.SetParent(lobbyView.transform, false);
            var spRt = (RectTransform)speechPanel.transform;
            spRt.sizeDelta = new Vector2(640, 140);
            spRt.anchoredPosition = new Vector2(-280, 220);
            speechPanel.GetComponent<Image>().color = OutgameStyle.SurfaceCard;

            var speechText = CreateText(speechPanel, "SpeechText", "지휘관님, 킹의 체스판 위에 모든 기물이 준비되었습니다.", 22, Vector2.zero, TextAlignmentOptions.Center);
            speechText.rectTransform.sizeDelta = new Vector2(600, 100);

            // Terminal Status Card (Left-side Profile Summary)
            var statusCard = new GameObject("TerminalStatusCard", typeof(RectTransform), typeof(Image));
            statusCard.transform.SetParent(lobbyView.transform, false);
            var scRt = (RectTransform)statusCard.transform;
            scRt.anchoredPosition = new Vector2(-680, 360);
            scRt.sizeDelta = new Vector2(380, 130);
            statusCard.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.13f, 0.85f);

            var kingLv = CreateText(statusCard, "KingLevelText", "지휘관 슈트 Lv.1", 22, new Vector2(0, 24), TextAlignmentOptions.Center);
            kingLv.rectTransform.sizeDelta = new Vector2(350, 40);

            var apText = CreateText(statusCard, "ClearedStagesText", "작전 완수: 0 구역", 20, new Vector2(0, -24), TextAlignmentOptions.Center);
            apText.rectTransform.sizeDelta = new Vector2(350, 40);

            var lobbyType = typeof(MainLobbyUI);
            lobbyType.GetField("stageSelectButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, stageSelectBtn);
            lobbyType.GetField("rosterButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, rosterBtn);
            lobbyType.GetField("synchroBoardButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, synchroBtn);
            lobbyType.GetField("recruitButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, recruitBtn);
            lobbyType.GetField("operatorHubButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, operatorHubBtn);
            lobbyType.GetField("kingSuitBayButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, kingSuitBayBtn);
            lobbyType.GetField("archiveButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, archiveBtn);
            lobbyType.GetField("secretaryTouchArea", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, secretaryArea);
            lobbyType.GetField("speechBubblePanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, speechPanel);
            lobbyType.GetField("speechBubbleText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, speechText);
            lobbyType.GetField("kingLevelText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, kingLv);
            lobbyType.GetField("tacticalPointsText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(lobbyView, apText);
            viewsList.Add(lobbyView);

            // --- Roster View ---
            var rosterView = CreatePanel<RosterUI>(canvasObj, "RosterView", OutgameViewType.Roster);
            CreateDarkGlassBackground(rosterView.gameObject, "R O S T E R", 60);

            var unitContainer = new GameObject("UnitListContainer");
            unitContainer.transform.SetParent(rosterView.transform, false);
            var unitRt = unitContainer.AddComponent<RectTransform>();
            unitRt.anchoredPosition = new Vector2(0, -60);
            unitRt.sizeDelta = new Vector2(1400, 640);
            var unitGrid = unitContainer.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            unitGrid.cellSize = new Vector2(300, 60);
            unitGrid.spacing = new Vector2(20, 20);
            unitGrid.childAlignment = TextAnchor.UpperCenter;
            
            var dummyUnitPrefab = CreateButton(rosterView.gameObject, "DummyUnitPrefab", "UnitSlot", Vector2.zero, new Vector2(300, 60));
            dummyUnitPrefab.gameObject.SetActive(false);
            typeof(RosterUI).GetField("unitListContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(rosterView, unitContainer.transform);
            typeof(RosterUI).GetField("unitSlotButtonPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(rosterView, dummyUnitPrefab);
            viewsList.Add(rosterView);

            // --- Synchro Board View ---
            var synchroView = CreatePanel<SynchroBoardUI>(canvasObj, "SynchroBoardView", OutgameViewType.SynchroBoard);
            CreateDarkGlassBackground(synchroView.gameObject, "S Y N C H R O  M O D D I N G", 60);
            viewsList.Add(synchroView);

            // --- Deck Builder View ---
            var deckView = CreatePanel<DeckBuilderUI>(canvasObj, "DeckBuilderView", OutgameViewType.DeckBuilder);
            CreateDarkGlassBackground(deckView.gameObject, "D E C K   B U I L D I N G", 60);
            
            var rosterGrid = new GameObject("RosterGrid");
            rosterGrid.transform.SetParent(deckView.transform, false);
            var rosterGridRt = rosterGrid.AddComponent<RectTransform>();
            rosterGridRt.anchoredPosition = new Vector2(0, -100); rosterGridRt.sizeDelta = new Vector2(1600, 300);
            var hlg = rosterGrid.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 20; hlg.childControlHeight = false; hlg.childControlWidth = false;
            typeof(DeckBuilderUI).GetField("_rosterContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(deckView, rosterGrid.transform);
            
            var dummyRosterSlot = CreateButton(deckView.gameObject, "RosterSlotPrefab", "+", Vector2.zero, new Vector2(150, 200));
            var slotComp = dummyRosterSlot.gameObject.AddComponent<RosterSlotUI>();
            
            var slotBtn = dummyRosterSlot.gameObject.GetComponent<Button>();
            var slotTxt = dummyRosterSlot.gameObject.GetComponentInChildren<TextMeshProUGUI>();
            slotTxt.fontSize = 40;
            
            // Icon
            var slotIconGo = new GameObject("Icon");
            slotIconGo.transform.SetParent(dummyRosterSlot.transform, false);
            var slotIconRt = slotIconGo.AddComponent<RectTransform>();
            slotIconRt.anchorMin = Vector2.zero; slotIconRt.anchorMax = Vector2.one; slotIconRt.sizeDelta = Vector2.zero;
            var slotIconImg = slotIconGo.AddComponent<Image>();
            slotIconGo.SetActive(false);

            typeof(RosterSlotUI).GetField("_unitNameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(slotComp, slotTxt);
            typeof(RosterSlotUI).GetField("_actionButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(slotComp, slotBtn);
            typeof(RosterSlotUI).GetField("_unitIcon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(slotComp, slotIconImg);

            dummyRosterSlot.gameObject.SetActive(false);
            typeof(DeckBuilderUI).GetField("_rosterSlotPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(deckView, slotComp);
            
            var startBattleBtn = CreateButton(deckView.gameObject, "StartBattleBtn", "START BATTLE", new Vector2(700, -400), new Vector2(300, 80), new Color(1f, 0.4f, 0.1f, 0.95f));
            typeof(DeckBuilderUI).GetField("_startBattleButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(deckView, startBattleBtn);

            var inventoryPanel = new GameObject("InventoryPanel");
            inventoryPanel.transform.SetParent(deckView.transform, false);
            var invRt = inventoryPanel.AddComponent<RectTransform>();
            invRt.anchorMin = Vector2.zero; invRt.anchorMax = Vector2.one; invRt.sizeDelta = Vector2.zero;
            var invImg = inventoryPanel.AddComponent<Image>(); invImg.color = new Color(0,0,0,0.92f);
            inventoryPanel.SetActive(false);
            typeof(DeckBuilderUI).GetField("_inventoryPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(deckView, inventoryPanel);

            var closeInvBtn = CreateButton(inventoryPanel, "CloseInvBtn", "CLOSE", new Vector2(0, 450), new Vector2(200, 80));
            typeof(DeckBuilderUI).GetField("_closeInventoryButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(deckView, closeInvBtn);

            var invGrid = new GameObject("InventoryGrid");
            invGrid.transform.SetParent(inventoryPanel.transform, false);
            var invGridRt = invGrid.AddComponent<RectTransform>();
            invGridRt.anchoredPosition = Vector2.zero; invGridRt.sizeDelta = new Vector2(1600, 800);
            var glg = invGrid.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            glg.cellSize = new Vector2(150, 200); glg.spacing = new Vector2(20, 20); glg.childAlignment = TextAnchor.MiddleCenter;
            typeof(DeckBuilderUI).GetField("_inventoryContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(deckView, invGrid.transform);
            typeof(DeckBuilderUI).GetField("_inventorySlotPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(deckView, slotComp);

            viewsList.Add(deckView);

            // --- Stage Select View ---
            var stageView = CreatePanel<StageSelectUI>(canvasObj, "StageSelectView", OutgameViewType.StageSelect);
            CreateDarkGlassBackground(stageView.gameObject, "O P E R A T I O N S", 60);
            
            var stageContainer = new GameObject("StageListContainer");
            stageContainer.transform.SetParent(stageView.transform, false);
            var stageRt = stageContainer.AddComponent<RectTransform>();
            stageRt.anchoredPosition = new Vector2(0, -60);
            stageRt.sizeDelta = new Vector2(1000, 680);
            var stageVl = stageContainer.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            stageVl.spacing = 14f;
            stageVl.childAlignment = TextAnchor.UpperCenter;
            stageVl.childControlHeight = true;
            stageVl.childControlWidth = true;
            stageVl.childForceExpandHeight = false;
            
            var dummyStagePrefab = CreateButton(stageView.gameObject, "DummyStagePrefab", "StageSlot", Vector2.zero, new Vector2(880, 84));
            dummyStagePrefab.gameObject.SetActive(false);
            var dummyLe = dummyStagePrefab.gameObject.AddComponent<LayoutElement>();
            dummyLe.preferredHeight = 84f;
            dummyLe.minHeight = 80f;
            dummyLe.flexibleWidth = 1f;

            typeof(StageSelectUI).GetField("_stageListContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(stageView, stageContainer.transform);
            typeof(StageSelectUI).GetField("_stageButtonPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(stageView, dummyStagePrefab.gameObject);
            viewsList.Add(stageView);

            // --- Operator Hub View ---
            var operatorView = CreatePanel<OperatorHubUI>(canvasObj, "OperatorHubView", OutgameViewType.OperatorHub);
            CreateDarkGlassBackground(operatorView.gameObject, "O P E R A T O R   H U B", 60);
            viewsList.Add(operatorView);

            // --- Recruit Hub View ---
            var recruitView = CreatePanel<RecruitHubUI>(canvasObj, "RecruitHubView", OutgameViewType.Recruit);
            CreateDarkGlassBackground(recruitView.gameObject, "R E C R U I T M E N T", 60);
            viewsList.Add(recruitView);

            // --- Archive View ---
            var archiveView = CreatePanel<ArchiveUI>(canvasObj, "ArchiveView", OutgameViewType.Archive);
            CreateDarkGlassBackground(archiveView.gameObject, "A R C H I V E", 60);
            viewsList.Add(archiveView);

            // --- King Suit Bay View ---
            var kingSuitView = CreatePanel<KingSuitBayUI>(canvasObj, "KingSuitBayView", OutgameViewType.KingSuitBay);
            CreateDarkGlassBackground(kingSuitView.gameObject, "K I N G ' S   S U I T", 60);
            viewsList.Add(kingSuitView);

            // Inject views list to manager
            viewsField.SetValue(uiManager, viewsList);

            // --- Unified Top Navigation Header (Arknights Style) ---
            var headerObj = new GameObject("HeaderPanel");
            headerObj.transform.SetParent(canvasObj.transform, false);
            var headerUI = headerObj.AddComponent<OutgameHeaderUI>();
            headerUI.EnsureHeaderUI();
            headerObj.transform.SetAsLastSibling();

            // Save Scene
            string targetPath = "Assets/Scenes/OutgameScene.unity";
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, targetPath);
            
            Debug.Log($"✅ [Phase 13] Premium Arknights Outgame Hub Scene baked successfully at {targetPath}!");
        }

        private static T CreatePanel<T>(GameObject parent, string name, OutgameViewType type) where T : OutgameViewBase
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            T comp = go.AddComponent<T>();
            comp.ViewType = type;
            
            go.AddComponent<CanvasGroup>();
            return comp;
        }

        private static void CreateDarkGlassBackground(GameObject parent, string titleText, int fontSize)
        {
            var bg = new GameObject("Background");
            bg.transform.SetParent(parent.transform, false);
            var rt = bg.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var img = bg.AddComponent<Image>();
            img.color = new Color(0.07f, 0.08f, 0.09f, 1f); // BG_Dark_Base (#121417)

            var textGo = new GameObject("TitleText");
            textGo.transform.SetParent(parent.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.text = titleText;
            txt.fontSize = fontSize;
            txt.color = new Color(1, 1, 1, 0.15f); // Subtle watermark title
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            var trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;
            bg.transform.SetAsFirstSibling();
            textGo.transform.SetSiblingIndex(1);
        }

        private static Button CreateButton(GameObject parent, string name, string text, Vector2 pos, Vector2 size, Color? col = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.color = col ?? new Color(0.12f, 0.13f, 0.17f, 0.9f); // BG_Dark_Surface
            
            Button btn = go.AddComponent<Button>();
            go.AddComponent<ButtonFeedback>();

            GameObject txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            RectTransform txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one; txtRt.sizeDelta = Vector2.zero;
            
            TextMeshProUGUI tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;

            return btn;
        }

        private static TextMeshProUGUI CreateText(GameObject parent, string name, string text, int size, Vector2 pos = default, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(800, 100);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.alignment = align;
            return tmp;
        }
    }
}
#endif
