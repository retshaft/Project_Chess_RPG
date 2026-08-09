#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Progression;
using CheckmateRPG.UI;
using CheckmateRPG.Data;

namespace CheckmateRPG.EditorScripts
{
    public static class M11SceneSetupMenu
    {
        [MenuItem("CheckmateRPG/Setup M11 DeckBuilder Scene", false, 10)]
        public static void SetupScene()
        {
            // 1. GameManagers 생성
            GameObject managersGO = GameObject.Find("GameManagers");
            if (managersGO == null)
            {
                managersGO = new GameObject("GameManagers");
            }
            
            if (!managersGO.TryGetComponent<SaveManager>(out _))
                managersGO.AddComponent<SaveManager>();
            
            if (!managersGO.TryGetComponent<DeckManager>(out var deckManager))
                deckManager = managersGO.AddComponent<DeckManager>();

            // UnitData 자동 할당
            string[] guids = AssetDatabase.FindAssets("t:UnitData");
            deckManager.AllUnitsDatabase.Clear();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
                if (data != null)
                {
                    deckManager.AllUnitsDatabase.Add(data);
                }
            }
            
            // 2. Canvas 생성
            Canvas canvas = GameObject.FindObjectOfType<Canvas>();
            GameObject canvasGO = null;
            if (canvas == null)
            {
                canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            else
            {
                canvasGO = canvas.gameObject;
            }

            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // 기존 DeckBuilderPanel 삭제 후 재생성 (깔끔한 업데이트를 위함)
            Transform oldPanel = canvasGO.transform.Find("DeckBuilderPanel");
            if (oldPanel != null) GameObject.DestroyImmediate(oldPanel.gameObject);

            // 3. DeckBuilderUI 배경 (Background)
            GameObject panelGO = new GameObject("DeckBuilderPanel", typeof(RectTransform), typeof(Image), typeof(DeckBuilderUI));
            panelGO.transform.SetParent(canvasGO.transform, false);
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelGO.GetComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f, 1.0f); // Arknights-like light/clean theme

            DeckBuilderUI deckBuilderUI = panelGO.GetComponent<DeckBuilderUI>();

            // --- Header Title ---
            GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(panelGO.transform, false);
            RectTransform titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.85f);
            titleRect.anchorMax = new Vector2(1, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            TextMeshProUGUI titleTmp = titleGO.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "SQUAD FORMATION";
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.fontSize = 50;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(0.2f, 0.2f, 0.2f, 1.0f);

            // --- Roster Container ---
            GameObject rosterGO = new GameObject("RosterContainer", typeof(RectTransform), typeof(GridLayoutGroup));
            rosterGO.transform.SetParent(panelGO.transform, false);
            RectTransform rosterRect = rosterGO.GetComponent<RectTransform>();
            // Centered layout taking up most of the screen
            rosterRect.anchorMin = new Vector2(0.1f, 0.2f);
            rosterRect.anchorMax = new Vector2(0.9f, 0.8f);
            rosterRect.offsetMin = Vector2.zero;
            rosterRect.offsetMax = Vector2.zero;
            
            GridLayoutGroup rosterGrid = rosterGO.GetComponent<GridLayoutGroup>();
            rosterGrid.cellSize = new Vector2(180, 260); // Arknights style tall slots
            rosterGrid.spacing = new Vector2(30, 30);
            rosterGrid.childAlignment = TextAnchor.MiddleCenter;
            rosterGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            rosterGrid.constraintCount = 4; // 2 rows of 4

            // --- Inventory Panel (Popup) ---
            GameObject invPanelGO = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
            invPanelGO.transform.SetParent(panelGO.transform, false);
            RectTransform invPanelRect = invPanelGO.GetComponent<RectTransform>();
            invPanelRect.anchorMin = Vector2.zero;
            invPanelRect.anchorMax = Vector2.one;
            invPanelRect.offsetMin = Vector2.zero;
            invPanelRect.offsetMax = Vector2.zero;
            invPanelGO.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.95f); // Dark semi-transparent background

            // Inventory Title
            GameObject invTitleGO = new GameObject("InvTitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            invTitleGO.transform.SetParent(invPanelGO.transform, false);
            RectTransform invTitleRect = invTitleGO.GetComponent<RectTransform>();
            invTitleRect.anchorMin = new Vector2(0, 0.85f);
            invTitleRect.anchorMax = new Vector2(1, 0.95f);
            invTitleRect.offsetMin = Vector2.zero;
            invTitleRect.offsetMax = Vector2.zero;
            TextMeshProUGUI invTitleTmp = invTitleGO.GetComponent<TextMeshProUGUI>();
            invTitleTmp.text = "SELECT OPERATOR";
            invTitleTmp.alignment = TextAlignmentOptions.Center;
            invTitleTmp.fontSize = 50;
            invTitleTmp.fontStyle = FontStyles.Bold;
            invTitleTmp.color = Color.white;

            // --- Inventory Scroll View ---
            GameObject scrollGO = new GameObject("InventoryScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGO.transform.SetParent(invPanelGO.transform, false);
            RectTransform scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.05f, 0.1f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.8f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            scrollGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.2f);
            
            // Scroll View Viewport & Mask
            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            RectTransform vpRect = viewportGO.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = new Vector2(10, 10);
            vpRect.offsetMax = new Vector2(-10, -10);
            viewportGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            viewportGO.GetComponent<Mask>().showMaskGraphic = false;

            // --- Inventory Container ---
            GameObject inventoryGO = new GameObject("InventoryContainer", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            inventoryGO.transform.SetParent(viewportGO.transform, false);
            RectTransform inventoryRect = inventoryGO.GetComponent<RectTransform>();
            inventoryRect.anchorMin = new Vector2(0, 1);
            inventoryRect.anchorMax = new Vector2(1, 1);
            inventoryRect.pivot = new Vector2(0, 1);
            inventoryRect.offsetMin = Vector2.zero;
            inventoryRect.offsetMax = Vector2.zero;
            
            GridLayoutGroup inventoryGrid = inventoryGO.GetComponent<GridLayoutGroup>();
            inventoryGrid.cellSize = new Vector2(160, 240);
            inventoryGrid.spacing = new Vector2(20, 20);
            inventoryGrid.childAlignment = TextAnchor.UpperLeft;
            
            ContentSizeFitter fitter = inventoryGO.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.MinSize;

            ScrollRect sr = scrollGO.GetComponent<ScrollRect>();
            sr.content = inventoryRect;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.viewport = vpRect;

            // Close Inventory Button
            GameObject closeInvBtnGO = new GameObject("CloseInventoryButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeInvBtnGO.transform.SetParent(invPanelGO.transform, false);
            RectTransform closeInvRect = closeInvBtnGO.GetComponent<RectTransform>();
            closeInvRect.anchorMin = new Vector2(0.85f, 0.88f);
            closeInvRect.anchorMax = new Vector2(0.95f, 0.95f);
            closeInvRect.offsetMin = Vector2.zero;
            closeInvRect.offsetMax = Vector2.zero;
            closeInvBtnGO.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f);

            GameObject closeTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            closeTextGO.transform.SetParent(closeInvBtnGO.transform, false);
            RectTransform closeTextRect = closeTextGO.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero; closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero; closeTextRect.offsetMax = Vector2.zero;
            TextMeshProUGUI closeTmp = closeTextGO.GetComponent<TextMeshProUGUI>();
            closeTmp.text = "CLOSE"; closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.fontSize = 24; closeTmp.fontStyle = FontStyles.Bold; closeTmp.color = Color.white;

            // --- Start Battle Button (on main panel) ---
            GameObject startBtnGO = new GameObject("StartBattleButton", typeof(RectTransform), typeof(Image), typeof(Button));
            startBtnGO.transform.SetParent(panelGO.transform, false);
            RectTransform btnRect = startBtnGO.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.75f, 0.05f);
            btnRect.anchorMax = new Vector2(0.95f, 0.15f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;
            startBtnGO.GetComponent<Image>().color = new Color(0.95f, 0.45f, 0.1f, 1f); // Arknights orange button

            GameObject btnTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTextGO.transform.SetParent(startBtnGO.transform, false);
            RectTransform btnTextRect = btnTextGO.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            TextMeshProUGUI btnTmp = btnTextGO.GetComponent<TextMeshProUGUI>();
            btnTmp.text = "OPERATION START";
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.fontSize = 26;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.color = Color.white;

            // 4. Prefab 생성 (템플릿용)
            GameObject prefabSlot = new GameObject("RosterSlotPrefab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(RosterSlotUI));
            prefabSlot.transform.SetParent(panelGO.transform, false);
            prefabSlot.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.3f, 1.0f); // 어두운 슬롯 배경
            prefabSlot.SetActive(false); 
            
            // Image (Icon)
            GameObject iconGO = new GameObject("UnitIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(prefabSlot.transform, false);
            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(5, 30);
            iconRect.offsetMax = new Vector2(-5, -5);
            iconGO.GetComponent<Image>().preserveAspect = true;

            // TMP (Text)
            GameObject textGO = new GameObject("UnitNameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(prefabSlot.transform, false);
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0);
            textRect.offsetMin = new Vector2(0, 0);
            textRect.offsetMax = new Vector2(0, 30);
            TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "Unit";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 16;
            tmp.color = Color.white;

            // RosterSlotUI 맵핑
            SerializedObject so = new SerializedObject(prefabSlot.GetComponent<RosterSlotUI>());
            so.FindProperty("_unitIcon").objectReferenceValue = iconGO.GetComponent<Image>();
            so.FindProperty("_unitNameText").objectReferenceValue = tmp;
            so.FindProperty("_actionButton").objectReferenceValue = prefabSlot.GetComponent<Button>();
            so.ApplyModifiedProperties();

            // DeckBuilderUI 맵핑
            SerializedObject dbSO = new SerializedObject(deckBuilderUI);
            dbSO.FindProperty("_rosterContainer").objectReferenceValue = rosterGO.transform;
            dbSO.FindProperty("_inventoryContainer").objectReferenceValue = inventoryGO.transform;
            dbSO.FindProperty("_inventoryPanel").objectReferenceValue = invPanelGO;
            dbSO.FindProperty("_rosterSlotPrefab").objectReferenceValue = prefabSlot.GetComponent<RosterSlotUI>();
            dbSO.FindProperty("_inventorySlotPrefab").objectReferenceValue = prefabSlot.GetComponent<RosterSlotUI>();
            dbSO.FindProperty("_startBattleButton").objectReferenceValue = startBtnGO.GetComponent<Button>();
            dbSO.FindProperty("_closeInventoryButton").objectReferenceValue = closeInvBtnGO.GetComponent<Button>();
            dbSO.ApplyModifiedProperties();

            // Make sure Inventory Panel is hidden by default in editor
            invPanelGO.SetActive(false);

            EditorGUIUtility.PingObject(panelGO);
            Debug.Log("[M11] 명일방주 스타일 덱 빌더 씬 셋업이 완료되었습니다. (팝업 인벤토리 적용)");
        }
    }
}
#endif
