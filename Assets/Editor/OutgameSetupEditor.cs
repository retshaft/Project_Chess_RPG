using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.UI;
using CheckmateRPG.Progression;

public class OutgameSetupEditor
{
    [MenuItem("Tools/Setup Outgame Scene")]
    public static void Setup()
    {
        string scenePath = "Assets/Scenes/OutgameScene.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        GameObject uiManagerObj = GameObject.Find("OutgameUIManager");
        if (uiManagerObj == null) uiManagerObj = new GameObject("OutgameUIManager");
        var uiManager = uiManagerObj.GetComponent<OutgameUIManager>();
        if (uiManager == null) uiManager = uiManagerObj.AddComponent<OutgameUIManager>();

        GameObject canvasObj = GameObject.Find("MainCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("MainCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        var views = new System.Collections.Generic.List<OutgameViewBase>();

        views.Add(CreateOrGetView<TitleUI>(canvasObj.transform, "TitlePanel", OutgameViewType.Title));
        views.Add(CreateOrGetView<MainLobbyUI>(canvasObj.transform, "MainLobbyPanel", OutgameViewType.MainLobby));
        views.Add(CreateOrGetView<StageSelectUI>(canvasObj.transform, "StageSelectPanel", OutgameViewType.StageSelect));
        views.Add(CreateOrGetView<DeckBuilderUI>(canvasObj.transform, "DeckBuilderPanel", OutgameViewType.DeckBuilder));
        views.Add(CreateOrGetView<RosterUI>(canvasObj.transform, "RosterPanel", OutgameViewType.Roster));
        views.Add(CreateOrGetView<SynchroBoardUI>(canvasObj.transform, "SynchroBoardPanel", OutgameViewType.SynchroBoard));
        views.Add(CreateOrGetView<RecruitUI>(canvasObj.transform, "RecruitPanel", OutgameViewType.Recruit));

        SerializedObject so = new SerializedObject(uiManager);
        SerializedProperty viewsProp = so.FindProperty("views");
        viewsProp.arraySize = views.Count;
        for (int i = 0; i < views.Count; i++)
        {
            viewsProp.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
        }
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("OutgameScene setup complete!");
    }

    [MenuItem("Tools/Generate Sample Edict Assets")]
    public static void GenerateSampleEdicts()
    {
        string resourcesPath = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        string edictsPath = "Assets/Resources/Edicts";
        if (!AssetDatabase.IsValidFolder(edictsPath))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Edicts");
        }

        CreateOrUpdateEdict("Aura_Order_Vanguard", "Order's Vanguard", EdictKind.Absolute, PolarityType.Order, 10,
            new StatModifierEntry { Stat = MetaStatType.AttackDamage, PercentBonus = 0.2f },
            new StatModifierEntry { Stat = MetaStatType.MaxHealth, FlatBonus = 100f });

        CreateOrUpdateEdict("Aura_Chaos_Overdrive", "Chaotic Overdrive", EdictKind.Absolute, PolarityType.Chaos, 12, true, "카오틱 오버드라이브", 30f, 35f, true,
            new StatModifierEntry { Stat = MetaStatType.ActionSpeed, PercentBonus = 0.3f },
            new StatModifierEntry { Stat = MetaStatType.MoveRange, FlatBonus = 1f });

        CreateOrUpdateEdict("Mod_Order_Vitality", "Royal Vitality", EdictKind.General, PolarityType.Order, 5,
            new StatModifierEntry { Stat = MetaStatType.MaxHealth, FlatBonus = 250f, PercentBonus = 0.1f });

        CreateOrUpdateEdict("Mod_Chaos_Striker", "Sovereign Striker", EdictKind.General, PolarityType.Chaos, 6,
            new StatModifierEntry { Stat = MetaStatType.AttackDamage, FlatBonus = 30f, PercentBonus = 0.15f });

        CreateOrUpdateEdict("Mod_Neutral_Swiftness", "Knight's Swiftness", EdictKind.General, PolarityType.Neutral, 4,
            new StatModifierEntry { Stat = MetaStatType.MoveRange, FlatBonus = 2f },
            new StatModifierEntry { Stat = MetaStatType.MoveCostAP, FlatBonus = -1f });

        CreateOrUpdateEdict("Mod_Order_Bulwark", "Fortress Bulwark", EdictKind.General, PolarityType.Order, 7,
            new StatModifierEntry { Stat = MetaStatType.Defense, FlatBonus = 45f },
            new StatModifierEntry { Stat = MetaStatType.Resistance, FlatBonus = 45f });

        string notationsPath = "Assets/Resources/Notations";
        if (!AssetDatabase.IsValidFolder(notationsPath))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Notations");
        }

        CreateOrUpdateNotation("Notation_01_Foundation", "N-01_Foundation", 1, null,
            new StatModifierEntry { Stat = MetaStatType.MaxHealth, FlatBonus = 150f });
        CreateOrUpdateNotation("Notation_02_Vanguard", "N-02_Vanguard", 2, "Notation_01_Foundation",
            new StatModifierEntry { Stat = MetaStatType.Defense, FlatBonus = 35f });
        CreateOrUpdateNotation("Notation_03_Assault", "N-03_Assault", 2, "Notation_01_Foundation",
            new StatModifierEntry { Stat = MetaStatType.AttackDamage, PercentBonus = 0.2f });
        CreateOrUpdateNotation("Notation_04_Grandmaster", "N-04_Grandmaster", 4, "Notation_03_Assault",
            new StatModifierEntry { Stat = MetaStatType.MoveRange, FlatBonus = 1f },
            new StatModifierEntry { Stat = MetaStatType.ActionSpeed, PercentBonus = 0.25f });

        string stagesPath = "Assets/Resources/Stages";
        if (!AssetDatabase.IsValidFolder(stagesPath))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Stages");
        }

        CreateOrUpdateStage("Stage_01_Skirmish", "OP-01", "Frontier Recon (정찰 훈련)", ObjectiveType.Annihilation, 50, "Initial border reconnaissance operation. Eliminate hostile pawns along the perimeter.");
        CreateOrUpdateStage("Stage_02_Ambush", "OP-02", "Knight's Ambush (매복 돌파)", ObjectiveType.Annihilation, 80, "Hostile armored cavalry detected in Sector 4. Break through their blockade.");
        CreateOrUpdateStage("Stage_03_Checkmate", "OP-03", "King's Gambit (체크메이트)", ObjectiveType.KingDefeat, 150, "Decisive battle against enemy commander. Bypass enemy defenders and execute Checkmate!");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Successfully generated sample Edicts, Notation, and Stage assets in Resources!");
    }

    private static void CreateOrUpdateStage(string fileName, string id, string name, ObjectiveType obj, int tpReward, string desc)
    {
        string assetPath = $"Assets/Resources/Stages/{fileName}.asset";
        StageData asset = AssetDatabase.LoadAssetAtPath<StageData>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<StageData>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }
        asset.StageID = id;
        asset.StageName = name;
        asset.Objective = obj;
        asset.RewardTP = tpReward;
        asset.Description = desc;
        EditorUtility.SetDirty(asset);
    }

    private static void CreateOrUpdateNotation(string fileName, string nodeId, int tpCost, string prereqFileName, params StatModifierEntry[] stats)
    {
        string assetPath = $"Assets/Resources/Notations/{fileName}.asset";
        NotationNodeData node = AssetDatabase.LoadAssetAtPath<NotationNodeData>(assetPath);
        if (node == null)
        {
            node = ScriptableObject.CreateInstance<NotationNodeData>();
            AssetDatabase.CreateAsset(node, assetPath);
        }

        node.NodeId = nodeId;
        node.TPCost = tpCost;
        node.Prerequisites.Clear();
        if (!string.IsNullOrEmpty(prereqFileName))
        {
            var prereq = AssetDatabase.LoadAssetAtPath<NotationNodeData>($"Assets/Resources/Notations/{prereqFileName}.asset");
            if (prereq != null) node.Prerequisites.Add(prereq);
        }
        node.Bonuses.Clear();
        if (stats != null) node.Bonuses.AddRange(stats);

        EditorUtility.SetDirty(node);
    }

    private static void CreateOrUpdateEdict(string fileName, string title, EdictKind kind, PolarityType polarity, int cost, params StatModifierEntry[] stats)
    {
        string assetPath = $"Assets/Resources/Edicts/{fileName}.asset";
        EdictData edict = AssetDatabase.LoadAssetAtPath<EdictData>(assetPath);
        if (edict == null)
        {
            edict = ScriptableObject.CreateInstance<EdictData>();
            AssetDatabase.CreateAsset(edict, assetPath);
        }

        edict.EdictName = title;
        edict.Kind = kind;
        edict.Polarity = polarity;
        edict.SyncCost = cost;
        edict.HasActiveSkill = false;
        edict.Bonuses.Clear();
        if (stats != null)
        {
            edict.Bonuses.AddRange(stats);
        }

        EditorUtility.SetDirty(edict);
    }

    private static void CreateOrUpdateEdict(string fileName, string title, EdictKind kind, PolarityType polarity, int cost, bool hasActive, string activeName, float cooldown, float apBonus, bool useLighting, params StatModifierEntry[] stats)
    {
        string assetPath = $"Assets/Resources/Edicts/{fileName}.asset";
        EdictData edict = AssetDatabase.LoadAssetAtPath<EdictData>(assetPath);
        if (edict == null)
        {
            edict = ScriptableObject.CreateInstance<EdictData>();
            AssetDatabase.CreateAsset(edict, assetPath);
        }

        edict.EdictName = title;
        edict.Kind = kind;
        edict.Polarity = polarity;
        edict.SyncCost = cost;
        edict.HasActiveSkill = hasActive;
        edict.ActiveSkillName = activeName;
        edict.Cooldown = cooldown;
        edict.InstantAPBonus = apBonus;
        edict.UseOverdriveLighting = useLighting;
        edict.Bonuses.Clear();
        if (stats != null)
        {
            edict.Bonuses.AddRange(stats);
        }

        EditorUtility.SetDirty(edict);
    }

    [MenuItem("Tools/Build Outgame UI Layouts")]
    public static void BuildLayouts()
    {
        string scenePath = "Assets/Scenes/OutgameScene.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        
        GameObject canvasObj = GameObject.Find("MainCanvas");
        if (canvasObj == null) return;

        BuildTitleUI(canvasObj.transform.Find("TitlePanel"));
        BuildMainLobbyUI(canvasObj.transform.Find("MainLobbyPanel"));
        BuildRecruitUI(canvasObj.transform.Find("RecruitPanel"));
        BuildSynchroBoardUI(canvasObj.transform.Find("SynchroBoardPanel"));
        BuildRosterUI(canvasObj.transform.Find("RosterPanel"));
        BuildStageSelectUI(canvasObj.transform.Find("StageSelectPanel"));
        BuildDeckBuilderUI(canvasObj.transform.Find("DeckBuilderPanel"));

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Outgame UI Layouts Built!");
    }

    private static void BuildTitleUI(Transform parent)
    {
        if (parent == null) return;
        ClearChildren(parent);

        var bg = CreateUIObject("Background", parent, typeof(Image));
        bg.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);
        SetFullScreen(bg.GetComponent<RectTransform>());

        var titleTextObj = CreateUIObject("TitleText", parent, typeof(TextMeshProUGUI));
        var titleText = titleTextObj.GetComponent<TextMeshProUGUI>();
        titleText.text = "CHECKMATE RPG";
        titleText.fontSize = 80;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        SetFullScreen(titleTextObj.GetComponent<RectTransform>());

        var startBtnObj = CreateUIObject("StartButton", parent, typeof(Button), typeof(Image));
        startBtnObj.GetComponent<Image>().color = new Color(1, 1, 1, 0); // Transparent
        SetFullScreen(startBtnObj.GetComponent<RectTransform>());
        
        var titleUIScript = parent.GetComponent<TitleUI>();
        if (titleUIScript != null)
        {
            SerializedObject so = new SerializedObject(titleUIScript);
            so.FindProperty("startButton").objectReferenceValue = startBtnObj.GetComponent<Button>();
            so.ApplyModifiedProperties();
        }
    }

    private static void BuildMainLobbyUI(Transform parent)
    {
        if (parent == null) return;
        ClearChildren(parent);

        var bg = CreateUIObject("LobbyBackground", parent, typeof(Image));
        bg.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 1f);
        SetFullScreen(bg.GetComponent<RectTransform>());

        // Top Left Profile Info
        var profilePanel = CreateUIObject("ProfilePanel", parent, typeof(HorizontalLayoutGroup));
        var profileRect = profilePanel.GetComponent<RectTransform>();
        profileRect.anchorMin = new Vector2(0, 1);
        profileRect.anchorMax = new Vector2(0, 1);
        profileRect.pivot = new Vector2(0, 1);
        profileRect.anchoredPosition = new Vector2(50, -50);
        profileRect.sizeDelta = new Vector2(400, 80);

        var plg = profilePanel.GetComponent<HorizontalLayoutGroup>();
        plg.spacing = 30;
        plg.childAlignment = TextAnchor.MiddleLeft;
        plg.childControlWidth = true;
        plg.childControlHeight = true;

        var kingLevelTextObj = CreateUIObject("KingLevelText", profilePanel.transform, typeof(TextMeshProUGUI));
        var klText = kingLevelTextObj.GetComponent<TextMeshProUGUI>();
        klText.text = "King Lv. 1";
        klText.fontSize = 28;
        klText.alignment = TextAlignmentOptions.Left;
        klText.color = Color.white;

        var apTextObj = CreateUIObject("APText", profilePanel.transform, typeof(TextMeshProUGUI));
        var apText = apTextObj.GetComponent<TextMeshProUGUI>();
        apText.text = "AP: 0";
        apText.fontSize = 28;
        apText.alignment = TextAlignmentOptions.Left;
        apText.color = Color.cyan;

        // Secretary Touch Area & Dialogue Bubble
        var secretaryTouch = CreateUIObject("SecretaryTouchArea", parent, typeof(Image), typeof(Button));
        secretaryTouch.GetComponent<Image>().color = new Color(1, 1, 1, 0); // Transparent touch zone over character
        var touchRect = secretaryTouch.GetComponent<RectTransform>();
        touchRect.anchorMin = new Vector2(0.5f, 0.5f);
        touchRect.anchorMax = new Vector2(0.5f, 0.5f);
        touchRect.sizeDelta = new Vector2(500, 700);

        var speechBubble = CreateUIObject("SpeechBubblePanel", parent, typeof(Image));
        speechBubble.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);
        var bubbleRect = speechBubble.GetComponent<RectTransform>();
        bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRect.anchoredPosition = new Vector2(0, 250);
        bubbleRect.sizeDelta = new Vector2(520, 120);

        var bubbleTextObj = CreateUIObject("SpeechBubbleText", speechBubble.transform, typeof(TextMeshProUGUI));
        var bubbleTmp = bubbleTextObj.GetComponent<TextMeshProUGUI>();
        bubbleTmp.text = "......";
        bubbleTmp.fontSize = 24;
        bubbleTmp.alignment = TextAlignmentOptions.Center;
        bubbleTmp.color = Color.white;
        var bTextRect = bubbleTextObj.GetComponent<RectTransform>();
        bTextRect.anchorMin = Vector2.zero;
        bTextRect.anchorMax = Vector2.one;
        bTextRect.offsetMin = new Vector2(15, 10);
        bTextRect.offsetMax = new Vector2(-15, -10);

        // Right side navigation buttons
        var rightNav = CreateUIObject("RightNav", parent, typeof(VerticalLayoutGroup));
        var rightRect = rightNav.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(1, 0.5f);
        rightRect.anchorMax = new Vector2(1, 0.5f);
        rightRect.pivot = new Vector2(1, 0.5f);
        rightRect.anchoredPosition = new Vector2(-50, 0);
        rightRect.sizeDelta = new Vector2(300, 600);

        var vlg = rightNav.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 20;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var stageBtn = CreateButtonWithText("StageSelectButton", rightNav.transform, "Operations (Stage)");
        var rosterBtn = CreateButtonWithText("RosterButton", rightNav.transform, "Operators");
        var synchroBtn = CreateButtonWithText("SynchroBoardButton", rightNav.transform, "Edicts (Synchro)");
        var recruitBtn = CreateButtonWithText("RecruitButton", rightNav.transform, "Recruit");

        var lobbyUIScript = parent.GetComponent<MainLobbyUI>();
        if (lobbyUIScript != null)
        {
            SerializedObject so = new SerializedObject(lobbyUIScript);
            so.FindProperty("stageSelectButton").objectReferenceValue = stageBtn.GetComponent<Button>();
            so.FindProperty("rosterButton").objectReferenceValue = rosterBtn.GetComponent<Button>();
            so.FindProperty("synchroBoardButton").objectReferenceValue = synchroBtn.GetComponent<Button>();
            so.FindProperty("recruitButton").objectReferenceValue = recruitBtn.GetComponent<Button>();
            so.FindProperty("kingLevelText").objectReferenceValue = klText;
            so.FindProperty("tacticalPointsText").objectReferenceValue = apText;
            so.FindProperty("secretaryTouchArea").objectReferenceValue = secretaryTouch.GetComponent<Button>();
            so.FindProperty("speechBubblePanel").objectReferenceValue = speechBubble;
            so.FindProperty("speechBubbleText").objectReferenceValue = bubbleTmp;
            so.ApplyModifiedProperties();
        }
    }

    private static void BuildRecruitUI(Transform parent)
    {
        if (parent == null) return;
        ClearChildren(parent);

        var bg = CreateUIObject("RecruitBackground", parent, typeof(Image));
        bg.GetComponent<Image>().color = new Color(0.1f, 0.2f, 0.3f, 1f);
        SetFullScreen(bg.GetComponent<RectTransform>());

        var buttonContainer = CreateUIObject("ButtonsContainer", parent, typeof(HorizontalLayoutGroup));
        var hc = buttonContainer.GetComponent<RectTransform>();
        hc.anchorMin = new Vector2(0.5f, 0);
        hc.anchorMax = new Vector2(0.5f, 0);
        hc.pivot = new Vector2(0.5f, 0);
        hc.anchoredPosition = new Vector2(0, 100);
        hc.sizeDelta = new Vector2(600, 100);

        var hlg = buttonContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 50;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        var btn1 = CreateButtonWithText("Recruit1TimeButton", buttonContainer.transform, "Recruit 1x");
        var btn10 = CreateButtonWithText("Recruit10TimesButton", buttonContainer.transform, "Recruit 10x");
        
        var backBtn = CreateButtonWithText("BackToLobbyButton", parent, "Back To Lobby");
        var backRect = backBtn.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0, 1);
        backRect.anchorMax = new Vector2(0, 1);
        backRect.pivot = new Vector2(0, 1);
        backRect.anchoredPosition = new Vector2(50, -50);
        backRect.sizeDelta = new Vector2(200, 80);

        // Recruit Result Popup Panel (Overlay)
        var resultPanelObj = CreateUIObject("RecruitResultPanel", parent, typeof(Image), typeof(RecruitResultUI));
        resultPanelObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.88f);
        SetFullScreen(resultPanelObj.GetComponent<RectTransform>());

        var gridContainerObj = CreateUIObject("GridContainer", resultPanelObj.transform, typeof(GridLayoutGroup));
        var gridRect = gridContainerObj.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(900, 500);
        gridRect.anchoredPosition = new Vector2(0, 40);

        var gridLayout = gridContainerObj.GetComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(160, 220);
        gridLayout.spacing = new Vector2(15, 15);
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 5;

        var confirmBtn = CreateButtonWithText("ConfirmButton", resultPanelObj.transform, "Confirm");
        var cRect = confirmBtn.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 0);
        cRect.anchorMax = new Vector2(0.5f, 0);
        cRect.anchoredPosition = new Vector2(0, 80);
        cRect.sizeDelta = new Vector2(240, 60);

        var resultUI = resultPanelObj.GetComponent<RecruitResultUI>();
        if (resultUI != null)
        {
            SerializedObject resSo = new SerializedObject(resultUI);
            resSo.FindProperty("resultPanel").objectReferenceValue = resultPanelObj;
            resSo.FindProperty("gridContainer").objectReferenceValue = gridContainerObj.transform;
            resSo.FindProperty("confirmButton").objectReferenceValue = confirmBtn.GetComponent<Button>();
            resSo.ApplyModifiedProperties();
        }

        var recruitUIScript = parent.GetComponent<RecruitUI>();
        if (recruitUIScript != null)
        {
            SerializedObject so = new SerializedObject(recruitUIScript);
            so.FindProperty("recruit1TimeButton").objectReferenceValue = btn1.GetComponent<Button>();
            so.FindProperty("recruit10TimesButton").objectReferenceValue = btn10.GetComponent<Button>();
            so.FindProperty("backToLobbyButton").objectReferenceValue = backBtn.GetComponent<Button>();
            so.FindProperty("resultUI").objectReferenceValue = resultUI;
            so.ApplyModifiedProperties();
        }
    }

    private static void BuildSynchroBoardUI(Transform parent)
    {
        if (parent == null) return;
        ClearChildren(parent);

        // Dark sci-fi Warframe background
        var bg = CreateUIObject("BoardBackground", parent, typeof(Image));
        bg.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.11f, 1f);
        SetFullScreen(bg.GetComponent<RectTransform>());

        var backBtn = CreateButtonWithText("BackToLobbyButton", parent, "Back To Lobby");
        var backRect = backBtn.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0, 1);
        backRect.anchorMax = new Vector2(0, 1);
        backRect.pivot = new Vector2(0, 1);
        backRect.anchoredPosition = new Vector2(50, -50);
        backRect.sizeDelta = new Vector2(200, 70);

        // Capacity Drain Gauge (Top Center)
        var capacityObj = CreateUIObject("CapacityText", parent, typeof(TextMeshProUGUI));
        var capTmp = capacityObj.GetComponent<TextMeshProUGUI>();
        capTmp.text = "SYNCHRO CIRCUIT CAPACITY: 0 / 20";
        capTmp.fontSize = 28;
        capTmp.alignment = TextAlignmentOptions.Center;
        capTmp.color = Color.white;
        var capRect = capacityObj.GetComponent<RectTransform>();
        capRect.anchorMin = new Vector2(0.5f, 1);
        capRect.anchorMax = new Vector2(0.5f, 1);
        capRect.anchoredPosition = new Vector2(0, -60);
        capRect.sizeDelta = new Vector2(800, 50);

        // Absolute Slot (Warframe Aura Slot) - Top Center
        var absSlotObj = CreateUIObject("AbsoluteSlot", parent, typeof(Image), typeof(Button));
        absSlotObj.GetComponent<Image>().color = new Color(0.2f, 0.18f, 0.1f, 1f); // Bronze aura box
        var absRect = absSlotObj.GetComponent<RectTransform>();
        absRect.anchorMin = new Vector2(0.5f, 0.5f);
        absRect.anchorMax = new Vector2(0.5f, 0.5f);
        absRect.anchoredPosition = new Vector2(0, 160);
        absRect.sizeDelta = new Vector2(240, 150);

        var absTextObj = CreateUIObject("Text", absSlotObj.transform, typeof(TextMeshProUGUI));
        var absTmp = absTextObj.GetComponent<TextMeshProUGUI>();
        absTmp.text = "<b>[ AURA SLOT ]</b>\n<size=75%>Click to Equip</size>";
        absTmp.fontSize = 22;
        absTmp.alignment = TextAlignmentOptions.Center;
        absTmp.color = Color.white;
        SetFullScreen(absTextObj.GetComponent<RectTransform>());

        // 3 General Mod Circuits below
        Button[] genBtns = new Button[3];
        TextMeshProUGUI[] genTmps = new TextMeshProUGUI[3];
        Image[] genImgs = new Image[3];

        float[] xOffsets = { -280f, 0f, 280f };
        for (int i = 0; i < 3; i++)
        {
            var genSlotObj = CreateUIObject($"GeneralSlot_{i}", parent, typeof(Image), typeof(Button));
            genSlotObj.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.18f, 1f);
            var genRect = genSlotObj.GetComponent<RectTransform>();
            genRect.anchorMin = new Vector2(0.5f, 0.5f);
            genRect.anchorMax = new Vector2(0.5f, 0.5f);
            genRect.anchoredPosition = new Vector2(xOffsets[i], -80);
            genRect.sizeDelta = new Vector2(220, 260);

            var genTextObj = CreateUIObject("Text", genSlotObj.transform, typeof(TextMeshProUGUI));
            var genTmp = genTextObj.GetComponent<TextMeshProUGUI>();
            genTmp.text = $"<b>[ MOD SLOT {i+1} ]</b>\n<size=75%>Empty Circuit</size>";
            genTmp.fontSize = 20;
            genTmp.alignment = TextAlignmentOptions.Center;
            genTmp.color = Color.white;
            SetFullScreen(genTextObj.GetComponent<RectTransform>());

            genBtns[i] = genSlotObj.GetComponent<Button>();
            genImgs[i] = genSlotObj.GetComponent<Image>();
            genTmps[i] = genTmp;
        }

        // Warframe Mod Selector Modal (Overlay Window)
        var modalObj = CreateUIObject("EdictSelectorModal", parent, typeof(Image), typeof(EdictSelectorModal));
        modalObj.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.94f);
        SetFullScreen(modalObj.GetComponent<RectTransform>());

        var titleObj = CreateUIObject("ModalTitleText", modalObj.transform, typeof(TextMeshProUGUI));
        var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "SELECT MOD CIRCUIT";
        titleTmp.fontSize = 32;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = Color.cyan;
        var tRect = titleObj.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.5f, 1);
        tRect.anchorMax = new Vector2(0.5f, 1);
        tRect.anchoredPosition = new Vector2(0, -50);
        tRect.sizeDelta = new Vector2(800, 60);

        var modGridObj = CreateUIObject("ModCardGrid", modalObj.transform, typeof(GridLayoutGroup));
        var mGridRect = modGridObj.GetComponent<RectTransform>();
        mGridRect.anchorMin = new Vector2(0.5f, 0.5f);
        mGridRect.anchorMax = new Vector2(0.5f, 0.5f);
        mGridRect.anchoredPosition = new Vector2(0, -10);
        mGridRect.sizeDelta = new Vector2(900, 520);

        var modLayout = modGridObj.GetComponent<GridLayoutGroup>();
        modLayout.cellSize = new Vector2(180, 250);
        modLayout.spacing = new Vector2(20, 20);
        modLayout.childAlignment = TextAnchor.MiddleCenter;
        modLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        modLayout.constraintCount = 4;

        var closeModalBtn = CreateButtonWithText("CloseModalButton", modalObj.transform, "Cancel");
        var cModalRect = closeModalBtn.GetComponent<RectTransform>();
        cModalRect.anchorMin = new Vector2(0.5f, 0);
        cModalRect.anchorMax = new Vector2(0.5f, 0);
        cModalRect.anchoredPosition = new Vector2(0, 60);
        cModalRect.sizeDelta = new Vector2(200, 60);

        var modalScript = modalObj.GetComponent<EdictSelectorModal>();
        if (modalScript != null)
        {
            SerializedObject mSo = new SerializedObject(modalScript);
            mSo.FindProperty("modalPanel").objectReferenceValue = modalObj;
            mSo.FindProperty("windowTitleText").objectReferenceValue = titleTmp;
            mSo.FindProperty("cardGridContainer").objectReferenceValue = modGridObj.transform;
            mSo.FindProperty("closeButton").objectReferenceValue = closeModalBtn.GetComponent<Button>();
            mSo.ApplyModifiedProperties();
        }
        modalObj.SetActive(false);

        // Wire SynchroBoardUI properties
        var synchroScript = parent.GetComponent<SynchroBoardUI>();
        if (synchroScript != null)
        {
            SerializedObject sSo = new SerializedObject(synchroScript);
            sSo.FindProperty("capacityMeterText").objectReferenceValue = capTmp;
            sSo.FindProperty("absoluteSlotButton").objectReferenceValue = absSlotObj.GetComponent<Button>();
            sSo.FindProperty("absoluteSlotText").objectReferenceValue = absTmp;
            sSo.FindProperty("absoluteSlotFrame").objectReferenceValue = absSlotObj.GetComponent<Image>();
            sSo.FindProperty("backToLobbyButton").objectReferenceValue = backBtn.GetComponent<Button>();
            sSo.FindProperty("selectorModal").objectReferenceValue = modalScript;

            SerializedObject so = new SerializedObject(synchroScript);
            SerializedProperty btnsProp = so.FindProperty("generalSlotButtons");
            SerializedProperty tmpsProp = so.FindProperty("generalSlotTexts");
            SerializedProperty imgsProp = so.FindProperty("generalSlotFrames");
            btnsProp.arraySize = 3;
            tmpsProp.arraySize = 3;
            imgsProp.arraySize = 3;

            for (int i = 0; i < 3; i++)
            {
                btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = genBtns[i];
                tmpsProp.GetArrayElementAtIndex(i).objectReferenceValue = genTmps[i];
                imgsProp.GetArrayElementAtIndex(i).objectReferenceValue = genImgs[i];
            }
            so.ApplyModifiedProperties();
        }
    }

    private static void BuildRosterUI(Transform parent)
    {
        if (parent == null) return;
        ClearChildren(parent);

        // Subculture Dark Sci-Fi Background
        var bg = CreateUIObject("RosterBackground", parent, typeof(Image));
        bg.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 1f);
        SetFullScreen(bg.GetComponent<RectTransform>());

        // Back Button (Top Left)
        var backBtn = CreateButtonWithText("BackToLobbyButton", parent, "Back To Lobby");
        var backRect = backBtn.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0, 1);
        backRect.anchorMax = new Vector2(0, 1);
        backRect.pivot = new Vector2(0, 1);
        backRect.anchoredPosition = new Vector2(40, -40);
        backRect.sizeDelta = new Vector2(200, 65);

        // Left Unit List Scroll / Container
        var listContainerObj = CreateUIObject("UnitListContainer", parent, typeof(VerticalLayoutGroup));
        var listRect = listContainerObj.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0, 0.5f);
        listRect.anchorMax = new Vector2(0, 0.5f);
        listRect.pivot = new Vector2(0, 0.5f);
        listRect.anchoredPosition = new Vector2(40, -20);
        listRect.sizeDelta = new Vector2(320, 550);

        var listVlg = listContainerObj.GetComponent<VerticalLayoutGroup>();
        listVlg.spacing = 12;
        listVlg.childAlignment = TextAnchor.UpperCenter;
        listVlg.childControlWidth = true;
        listVlg.childControlHeight = false;

        var unitSlotPrefab = CreateButtonWithText("UnitSlotButtonPrefab", parent, "[Class] Operator Name");
        unitSlotPrefab.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 60);
        unitSlotPrefab.SetActive(false);

        // Center Profile & Inspection
        var profileObj = CreateUIObject("UnitNameText", parent, typeof(TextMeshProUGUI));
        var nameTmp = profileObj.GetComponent<TextMeshProUGUI>();
        nameTmp.text = "Operator Name";
        nameTmp.fontSize = 38;
        nameTmp.alignment = TextAlignmentOptions.Left;
        nameTmp.color = Color.white;
        var pRect = profileObj.GetComponent<RectTransform>();
        pRect.anchorMin = new Vector2(0.5f, 1);
        pRect.anchorMax = new Vector2(0.5f, 1);
        pRect.anchoredPosition = new Vector2(50, -70);
        pRect.sizeDelta = new Vector2(700, 50);

        var classObj = CreateUIObject("ClassText", parent, typeof(TextMeshProUGUI));
        var classTmp = classObj.GetComponent<TextMeshProUGUI>();
        classTmp.text = "Class: Knight";
        classTmp.fontSize = 26;
        classTmp.alignment = TextAlignmentOptions.Left;
        classTmp.color = Color.cyan;
        var cRect = classObj.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.5f, 1);
        cRect.anchorMax = new Vector2(0.5f, 1);
        cRect.anchoredPosition = new Vector2(50, -120);
        cRect.sizeDelta = new Vector2(700, 40);

        var statObj = CreateUIObject("StatSummaryText", parent, typeof(TextMeshProUGUI));
        var statTmp = statObj.GetComponent<TextMeshProUGUI>();
        statTmp.text = "Stats...";
        statTmp.fontSize = 24;
        statTmp.alignment = TextAlignmentOptions.TopLeft;
        statTmp.color = Color.white;
        var sRect = statObj.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.5f, 0.5f);
        sRect.anchorMax = new Vector2(0.5f, 0.5f);
        sRect.anchoredPosition = new Vector2(50, 40);
        sRect.sizeDelta = new Vector2(700, 300);

        // Resonance Progression section
        var resStageObj = CreateUIObject("ResonanceStageText", parent, typeof(TextMeshProUGUI));
        var resTmp = resStageObj.GetComponent<TextMeshProUGUI>();
        resTmp.text = "Resonance Stage: Lv. 1 / 5";
        resTmp.fontSize = 26;
        resTmp.alignment = TextAlignmentOptions.Left;
        resTmp.color = new Color(1f, 0.84f, 0f, 1f);
        var rRect = resStageObj.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.5f, 0.2f);
        rRect.anchorMax = new Vector2(0.5f, 0.2f);
        rRect.anchoredPosition = new Vector2(50, 40);
        rRect.sizeDelta = new Vector2(700, 40);

        var resDiscObj = CreateUIObject("ResonanceDiscountText", parent, typeof(TextMeshProUGUI));
        var discTmp = resDiscObj.GetComponent<TextMeshProUGUI>();
        discTmp.text = "Perk:...";
        discTmp.fontSize = 20;
        discTmp.alignment = TextAlignmentOptions.Left;
        discTmp.color = Color.green;
        var dRect = resDiscObj.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.5f, 0.2f);
        dRect.anchorMax = new Vector2(0.5f, 0.2f);
        dRect.anchoredPosition = new Vector2(50, 0);
        dRect.sizeDelta = new Vector2(700, 35);

        var enhanceBtn = CreateButtonWithText("EnhanceResonanceButton", parent, "Breakthrough (30 TP)");
        var eRect = enhanceBtn.GetComponent<RectTransform>();
        eRect.anchorMin = new Vector2(0.5f, 0);
        eRect.anchorMax = new Vector2(0.5f, 0);
        eRect.anchoredPosition = new Vector2(-120, 60);
        eRect.sizeDelta = new Vector2(260, 65);
        var enhanceCostTmp = enhanceBtn.GetComponentInChildren<TextMeshProUGUI>();

        var notationBtn = CreateButtonWithText("OpenNotationButton", parent, "Notation Circuit (Puzzle)");
        var nBtnRect = notationBtn.GetComponent<RectTransform>();
        nBtnRect.anchorMin = new Vector2(0.5f, 0);
        nBtnRect.anchorMax = new Vector2(0.5f, 0);
        nBtnRect.anchoredPosition = new Vector2(170, 60);
        nBtnRect.sizeDelta = new Vector2(280, 65);
        notationBtn.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.8f, 1f);

        // Notation Puzzle Modal Overlay
        var modalObj = CreateUIObject("NotationModalPanel", parent, typeof(Image));
        modalObj.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.96f);
        SetFullScreen(modalObj.GetComponent<RectTransform>());

        var modalTitleObj = CreateUIObject("ModalTitleText", modalObj.transform, typeof(TextMeshProUGUI));
        var modalTitleTmp = modalTitleObj.GetComponent<TextMeshProUGUI>();
        modalTitleTmp.text = "TACTICAL NOTATION CIRCUIT (PUZZLE TREE)";
        modalTitleTmp.fontSize = 32;
        modalTitleTmp.alignment = TextAlignmentOptions.Center;
        modalTitleTmp.color = Color.cyan;
        var mtRect = modalTitleObj.GetComponent<RectTransform>();
        mtRect.anchorMin = new Vector2(0.5f, 1);
        mtRect.anchorMax = new Vector2(0.5f, 1);
        mtRect.anchoredPosition = new Vector2(0, -45);
        mtRect.sizeDelta = new Vector2(900, 50);

        var tpBudgetObj = CreateUIObject("TPBudgetText", modalObj.transform, typeof(TextMeshProUGUI));
        var tpBudgetTmp = tpBudgetObj.GetComponent<TextMeshProUGUI>();
        tpBudgetTmp.text = "Tactical Points Budget: 100 TP";
        tpBudgetTmp.fontSize = 24;
        tpBudgetTmp.alignment = TextAlignmentOptions.Center;
        tpBudgetTmp.color = new Color(0f, 1f, 0.8f, 1f);
        var tbRect = tpBudgetObj.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0.5f, 1);
        tbRect.anchorMax = new Vector2(0.5f, 1);
        tbRect.anchoredPosition = new Vector2(0, -90);
        tbRect.sizeDelta = new Vector2(600, 40);

        var notGridObj = CreateUIObject("NotationGrid", modalObj.transform, typeof(GridLayoutGroup));
        var notGridRect = notGridObj.GetComponent<RectTransform>();
        notGridRect.anchorMin = new Vector2(0.5f, 0.5f);
        notGridRect.anchorMax = new Vector2(0.5f, 0.5f);
        notGridRect.anchoredPosition = new Vector2(0, -10);
        notGridRect.sizeDelta = new Vector2(960, 500);

        var notLayout = notGridObj.GetComponent<GridLayoutGroup>();
        notLayout.cellSize = new Vector2(210, 270);
        notLayout.spacing = new Vector2(25, 25);
        notLayout.childAlignment = TextAnchor.MiddleCenter;
        notLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        notLayout.constraintCount = 4;

        var closeModalBtn = CreateButtonWithText("CloseNotationModalButton", modalObj.transform, "Return to Profile");
        var cModalRect = closeModalBtn.GetComponent<RectTransform>();
        cModalRect.anchorMin = new Vector2(0.5f, 0);
        cModalRect.anchorMax = new Vector2(0.5f, 0);
        cModalRect.anchoredPosition = new Vector2(0, 50);
        cModalRect.sizeDelta = new Vector2(240, 60);

        modalObj.SetActive(false);

        // Wire RosterUI script properties
        var rosterScript = parent.GetComponent<RosterUI>();
        if (rosterScript != null)
        {
            SerializedObject rSo = new SerializedObject(rosterScript);
            rSo.FindProperty("backToLobbyButton").objectReferenceValue = backBtn.GetComponent<Button>();
            rSo.FindProperty("unitListContainer").objectReferenceValue = listContainerObj.transform;
            rSo.FindProperty("unitSlotButtonPrefab").objectReferenceValue = unitSlotPrefab.GetComponent<Button>();
            rSo.FindProperty("unitNameText").objectReferenceValue = nameTmp;
            rSo.FindProperty("pieceTypeText").objectReferenceValue = classTmp;
            rSo.FindProperty("statSummaryText").objectReferenceValue = statTmp;
            rSo.FindProperty("resonanceStageText").objectReferenceValue = resTmp;
            rSo.FindProperty("resonanceDiscountText").objectReferenceValue = discTmp;
            rSo.FindProperty("enhanceResonanceButton").objectReferenceValue = enhanceBtn.GetComponent<Button>();
            rSo.FindProperty("openNotationButton").objectReferenceValue = notationBtn.GetComponent<Button>();
            rSo.FindProperty("enhanceCostText").objectReferenceValue = enhanceCostTmp;
            rSo.FindProperty("notationModalPanel").objectReferenceValue = modalObj;
            rSo.FindProperty("notationGridContainer").objectReferenceValue = notGridObj.transform;
            rSo.FindProperty("closeNotationButton").objectReferenceValue = closeModalBtn.GetComponent<Button>();
            rSo.FindProperty("tpBudgetText").objectReferenceValue = tpBudgetTmp;
            rSo.ApplyModifiedProperties();
        }
    }

    private static void BuildStageSelectUI(Transform parent)
    {
        if (parent == null) return;
        ClearChildren(parent);

        var bgObj = CreateUIObject("Background", parent, typeof(Image));
        bgObj.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 1f);
        SetFullScreen(bgObj.GetComponent<RectTransform>());

        var titleObj = CreateUIObject("TitleText", parent, typeof(TextMeshProUGUI));
        var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "OPERATIONS (TACTICAL SORTIE)";
        titleTmp.fontSize = 42;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = Color.white;
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1);
        titleRect.anchorMax = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -60);
        titleRect.sizeDelta = new Vector2(800, 60);

        var backBtn = CreateButtonWithText("BackToLobbyButton", parent, "Back To Lobby");
        var bRect = backBtn.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0, 1);
        bRect.anchorMax = new Vector2(0, 1);
        bRect.anchoredPosition = new Vector2(160, -50);
        bRect.sizeDelta = new Vector2(220, 50);

        // Stage List Container
        var listContainerObj = CreateUIObject("StageListContainer", parent, typeof(VerticalLayoutGroup));
        var lcRect = listContainerObj.GetComponent<RectTransform>();
        lcRect.anchorMin = new Vector2(0.5f, 0.5f);
        lcRect.anchorMax = new Vector2(0.5f, 0.5f);
        lcRect.anchoredPosition = new Vector2(0, -30);
        lcRect.sizeDelta = new Vector2(600, 450);
        var vlg = listContainerObj.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 16;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        var stageBtnPrefab = CreateButtonWithText("StageButtonPrefab", parent, "OP-00 : Stage Name");
        stageBtnPrefab.GetComponent<RectTransform>().sizeDelta = new Vector2(550, 75);
        stageBtnPrefab.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.3f, 1f);
        var pText = stageBtnPrefab.GetComponentInChildren<TextMeshProUGUI>();
        if (pText != null) { pText.color = Color.cyan; pText.fontSize = 28; }
        stageBtnPrefab.SetActive(false);

        // Tactical Briefing Modal
        var modalObj = CreateUIObject("BriefingModalPanel", parent, typeof(Image));
        modalObj.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.04f, 0.96f);
        SetFullScreen(modalObj.GetComponent<RectTransform>());

        var modalTitleObj = CreateUIObject("StageTitleText", modalObj.transform, typeof(TextMeshProUGUI));
        var stTmp = modalTitleObj.GetComponent<TextMeshProUGUI>();
        stTmp.text = "<b>[OP-01]</b> Stage Title";
        stTmp.fontSize = 38;
        stTmp.alignment = TextAlignmentOptions.Center;
        stTmp.color = Color.cyan;
        var mtRect = modalTitleObj.GetComponent<RectTransform>();
        mtRect.anchorMin = new Vector2(0.5f, 1);
        mtRect.anchorMax = new Vector2(0.5f, 1);
        mtRect.anchoredPosition = new Vector2(0, -60);
        mtRect.sizeDelta = new Vector2(700, 50);

        var objTextObj = CreateUIObject("ObjectiveText", modalObj.transform, typeof(TextMeshProUGUI));
        var objTmp = objTextObj.GetComponent<TextMeshProUGUI>();
        objTmp.text = "• MISSION OBJECTIVE: Annihilation";
        objTmp.fontSize = 26;
        objTmp.alignment = TextAlignmentOptions.Left;
        objTmp.color = new Color(1f, 0.6f, 0f, 1f);
        var oRect = objTextObj.GetComponent<RectTransform>();
        oRect.anchorMin = new Vector2(0.5f, 1);
        oRect.anchorMax = new Vector2(0.5f, 1);
        oRect.anchoredPosition = new Vector2(0, -130);
        oRect.sizeDelta = new Vector2(700, 40);

        var descTextObj = CreateUIObject("DescriptionText", modalObj.transform, typeof(TextMeshProUGUI));
        var descTmp = descTextObj.GetComponent<TextMeshProUGUI>();
        descTmp.text = "Description...";
        descTmp.fontSize = 22;
        descTmp.alignment = TextAlignmentOptions.TopLeft;
        descTmp.color = Color.white;
        var dRect = descTextObj.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.5f, 1);
        dRect.anchorMax = new Vector2(0.5f, 1);
        dRect.anchoredPosition = new Vector2(0, -200);
        dRect.sizeDelta = new Vector2(700, 80);

        var intelTextObj = CreateUIObject("EnemyIntelText", modalObj.transform, typeof(TextMeshProUGUI));
        var intelTmp = intelTextObj.GetComponent<TextMeshProUGUI>();
        intelTmp.text = "• HOSTILE FORCES:...";
        intelTmp.fontSize = 24;
        intelTmp.alignment = TextAlignmentOptions.TopLeft;
        intelTmp.color = new Color(1f, 0.4f, 0.4f, 1f);
        var iRect = intelTextObj.GetComponent<RectTransform>();
        iRect.anchorMin = new Vector2(0.5f, 1);
        iRect.anchorMax = new Vector2(0.5f, 1);
        iRect.anchoredPosition = new Vector2(0, -320);
        iRect.sizeDelta = new Vector2(700, 140);

        var rewardTextObj = CreateUIObject("RewardText", modalObj.transform, typeof(TextMeshProUGUI));
        var rewTmp = rewardTextObj.GetComponent<TextMeshProUGUI>();
        rewTmp.text = "• REWARD FORECAST: +50 TP";
        rewTmp.fontSize = 24;
        rewTmp.alignment = TextAlignmentOptions.TopLeft;
        rewTmp.color = new Color(0.2f, 1f, 0.8f, 1f);
        var rewRect = rewardTextObj.GetComponent<RectTransform>();
        rewRect.anchorMin = new Vector2(0.5f, 1);
        rewRect.anchorMax = new Vector2(0.5f, 1);
        rewRect.anchoredPosition = new Vector2(0, -470);
        rewRect.sizeDelta = new Vector2(700, 100);

        var sortieBtn = CreateButtonWithText("ConfirmSortieButton", modalObj.transform, "SORTIE (출전 준비)");
        var sBtnRect = sortieBtn.GetComponent<RectTransform>();
        sBtnRect.anchorMin = new Vector2(0.5f, 0);
        sBtnRect.anchorMax = new Vector2(0.5f, 0);
        sBtnRect.anchoredPosition = new Vector2(150, 60);
        sBtnRect.sizeDelta = new Vector2(260, 65);
        sortieBtn.GetComponent<Image>().color = new Color(0f, 0.7f, 0.5f, 1f);
        var sbText = sortieBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (sbText != null) sbText.color = Color.white;

        var abortBtn = CreateButtonWithText("CloseBriefingButton", modalObj.transform, "ABORT (대기)");
        var aBtnRect = abortBtn.GetComponent<RectTransform>();
        aBtnRect.anchorMin = new Vector2(0.5f, 0);
        aBtnRect.anchorMax = new Vector2(0.5f, 0);
        aBtnRect.anchoredPosition = new Vector2(-150, 60);
        aBtnRect.sizeDelta = new Vector2(200, 65);
        abortBtn.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 1f);

        modalObj.SetActive(false);

        var stageScript = parent.GetComponent<StageSelectUI>();
        if (stageScript != null)
        {
            SerializedObject so = new SerializedObject(stageScript);
            so.FindProperty("_stageListContainer").objectReferenceValue = listContainerObj.transform;
            so.FindProperty("_stageButtonPrefab").objectReferenceValue = stageBtnPrefab;
            so.FindProperty("_briefingModalPanel").objectReferenceValue = modalObj;
            so.FindProperty("_stageTitleText").objectReferenceValue = stTmp;
            so.FindProperty("_objectiveText").objectReferenceValue = objTmp;
            so.FindProperty("_descriptionText").objectReferenceValue = descTmp;
            so.FindProperty("_enemyIntelText").objectReferenceValue = intelTmp;
            so.FindProperty("_rewardText").objectReferenceValue = rewTmp;
            so.FindProperty("_confirmSortieButton").objectReferenceValue = sortieBtn.GetComponent<Button>();
            so.FindProperty("_closeBriefingButton").objectReferenceValue = abortBtn.GetComponent<Button>();
            so.FindProperty("_backToLobbyButton").objectReferenceValue = backBtn.GetComponent<Button>();
            so.ApplyModifiedProperties();
        }
    }

    private static void BuildDeckBuilderUI(Transform parent)
    {
        if (parent == null) return;
        ClearChildren(parent);

        var bgObj = CreateUIObject("Background", parent, typeof(Image));
        bgObj.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 1f);
        SetFullScreen(bgObj.GetComponent<RectTransform>());

        var titleObj = CreateUIObject("TitleText", parent, typeof(TextMeshProUGUI));
        var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "TACTICAL SQUAD PREPARATION";
        titleTmp.fontSize = 38;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.color = Color.white;
        var titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1);
        titleRect.anchorMax = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(-150, -50);
        titleRect.sizeDelta = new Vector2(600, 50);

        var backBtn = CreateButtonWithText("BackToMapButton", parent, "Back To Operations");
        var bRect = backBtn.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0, 1);
        bRect.anchorMax = new Vector2(0, 1);
        bRect.anchoredPosition = new Vector2(160, -50);
        bRect.sizeDelta = new Vector2(240, 50);

        var powerObj = CreateUIObject("SquadPowerText", parent, typeof(TextMeshProUGUI));
        var powerTmp = powerObj.GetComponent<TextMeshProUGUI>();
        powerTmp.text = "SQUAD COMBAT RATING: 0";
        powerTmp.fontSize = 28;
        powerTmp.alignment = TextAlignmentOptions.Right;
        powerTmp.color = new Color(0f, 1f, 0.8f, 1f);
        var powRect = powerObj.GetComponent<RectTransform>();
        powRect.anchorMin = new Vector2(1, 1);
        powRect.anchorMax = new Vector2(1, 1);
        powRect.anchoredPosition = new Vector2(-250, -45);
        powRect.sizeDelta = new Vector2(500, 40);

        var syncObj = CreateUIObject("ActiveSynchroText", parent, typeof(TextMeshProUGUI));
        var syncTmp = syncObj.GetComponent<TextMeshProUGUI>();
        syncTmp.text = "• KING SYNCHRO: Active";
        syncTmp.fontSize = 22;
        syncTmp.alignment = TextAlignmentOptions.Right;
        syncTmp.color = new Color(1f, 0.84f, 0f, 1f);
        var syncRect = syncObj.GetComponent<RectTransform>();
        syncRect.anchorMin = new Vector2(1, 1);
        syncRect.anchorMax = new Vector2(1, 1);
        syncRect.anchoredPosition = new Vector2(-250, -85);
        syncRect.sizeDelta = new Vector2(600, 35);

        // Roster Slots Grid
        var rosterGridObj = CreateUIObject("RosterGrid", parent, typeof(GridLayoutGroup));
        var rGridRect = rosterGridObj.GetComponent<RectTransform>();
        rGridRect.anchorMin = new Vector2(0.5f, 0.5f);
        rGridRect.anchorMax = new Vector2(0.5f, 0.5f);
        rGridRect.anchoredPosition = new Vector2(0, 40);
        rGridRect.sizeDelta = new Vector2(920, 240);
        var rLayout = rosterGridObj.GetComponent<GridLayoutGroup>();
        rLayout.cellSize = new Vector2(210, 220);
        rLayout.spacing = new Vector2(20, 20);
        rLayout.childAlignment = TextAnchor.MiddleCenter;
        rLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        rLayout.constraintCount = 4;

        var rosterSlotPrefabObj = CreateUIObject("RosterSlotPrefab", parent, typeof(Image), typeof(Button), typeof(RosterSlotUI));
        rosterSlotPrefabObj.GetComponent<RectTransform>().sizeDelta = new Vector2(210, 220);
        rosterSlotPrefabObj.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.25f, 1f);
        var rsScript = rosterSlotPrefabObj.GetComponent<RosterSlotUI>();
        var rsTextObj = CreateUIObject("NameText", rosterSlotPrefabObj.transform, typeof(TextMeshProUGUI));
        var rsTmp = rsTextObj.GetComponent<TextMeshProUGUI>();
        rsTmp.text = "Slot / Unit";
        rsTmp.fontSize = 22;
        rsTmp.alignment = TextAlignmentOptions.Center;
        SetFullScreen(rsTextObj.GetComponent<RectTransform>());
        SerializedObject rsSo = new SerializedObject(rsScript);
        rsSo.FindProperty("_unitNameText").objectReferenceValue = rsTmp;
        rsSo.FindProperty("_actionButton").objectReferenceValue = rosterSlotPrefabObj.GetComponent<Button>();
        rsSo.ApplyModifiedProperties();
        rosterSlotPrefabObj.SetActive(false);

        var startBattleBtn = CreateButtonWithText("StartBattleButton", parent, "START MISSION (작전 개시)");
        var sbRect = startBattleBtn.GetComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(0.5f, 0);
        sbRect.anchorMax = new Vector2(0.5f, 0);
        sbRect.anchoredPosition = new Vector2(0, 60);
        sbRect.sizeDelta = new Vector2(340, 75);
        startBattleBtn.GetComponent<Image>().color = new Color(1f, 0.6f, 0.1f, 1f);
        var sbtTmp = startBattleBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (sbtTmp != null) { sbtTmp.color = Color.black; sbtTmp.fontStyle = FontStyles.Bold; sbtTmp.fontSize = 26; }

        // Inventory Modal
        var invPanelObj = CreateUIObject("InventoryModalPanel", parent, typeof(Image));
        invPanelObj.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.96f);
        SetFullScreen(invPanelObj.GetComponent<RectTransform>());

        var invTitleObj = CreateUIObject("InvTitleText", invPanelObj.transform, typeof(TextMeshProUGUI));
        var invTmp = invTitleObj.GetComponent<TextMeshProUGUI>();
        invTmp.text = "SELECT OPERATOR TO DEPLOY";
        invTmp.fontSize = 34;
        invTmp.alignment = TextAlignmentOptions.Center;
        invTmp.color = Color.cyan;
        var itRect = invTitleObj.GetComponent<RectTransform>();
        itRect.anchorMin = new Vector2(0.5f, 1);
        itRect.anchorMax = new Vector2(0.5f, 1);
        itRect.anchoredPosition = new Vector2(0, -50);
        itRect.sizeDelta = new Vector2(700, 50);

        var invGridObj = CreateUIObject("InventoryGrid", invPanelObj.transform, typeof(GridLayoutGroup));
        var igRect = invGridObj.GetComponent<RectTransform>();
        igRect.anchorMin = new Vector2(0.5f, 0.5f);
        igRect.anchorMax = new Vector2(0.5f, 0.5f);
        igRect.anchoredPosition = new Vector2(0, -10);
        igRect.sizeDelta = new Vector2(900, 480);
        var iLayout = invGridObj.GetComponent<GridLayoutGroup>();
        iLayout.cellSize = new Vector2(180, 190);
        iLayout.spacing = new Vector2(20, 20);
        iLayout.childAlignment = TextAnchor.UpperCenter;

        var invSlotPrefabObj = CreateUIObject("InvSlotPrefab", parent, typeof(Image), typeof(Button), typeof(RosterSlotUI));
        invSlotPrefabObj.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 190);
        invSlotPrefabObj.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f, 1f);
        var invsScript = invSlotPrefabObj.GetComponent<RosterSlotUI>();
        var invsTextObj = CreateUIObject("NameText", invSlotPrefabObj.transform, typeof(TextMeshProUGUI));
        var invsTmp = invsTextObj.GetComponent<TextMeshProUGUI>();
        invsTmp.text = "Unit Name";
        invsTmp.fontSize = 20;
        invsTmp.alignment = TextAlignmentOptions.Center;
        SetFullScreen(invsTextObj.GetComponent<RectTransform>());
        SerializedObject invSo = new SerializedObject(invsScript);
        invSo.FindProperty("_unitNameText").objectReferenceValue = invsTmp;
        invSo.FindProperty("_actionButton").objectReferenceValue = invSlotPrefabObj.GetComponent<Button>();
        invSo.ApplyModifiedProperties();
        invSlotPrefabObj.SetActive(false);

        var closeInvBtn = CreateButtonWithText("CloseInventoryButton", invPanelObj.transform, "Cancel Selection");
        var ciRect = closeInvBtn.GetComponent<RectTransform>();
        ciRect.anchorMin = new Vector2(0.5f, 0);
        ciRect.anchorMax = new Vector2(0.5f, 0);
        ciRect.anchoredPosition = new Vector2(0, 50);
        ciRect.sizeDelta = new Vector2(220, 55);
        invPanelObj.SetActive(false);

        var deckScript = parent.GetComponent<DeckBuilderUI>();
        if (deckScript != null)
        {
            SerializedObject dSo = new SerializedObject(deckScript);
            dSo.FindProperty("_rosterContainer").objectReferenceValue = rosterGridObj.transform;
            dSo.FindProperty("_inventoryContainer").objectReferenceValue = invGridObj.transform;
            dSo.FindProperty("_inventoryPanel").objectReferenceValue = invPanelObj;
            dSo.FindProperty("_rosterSlotPrefab").objectReferenceValue = rsScript;
            dSo.FindProperty("_inventorySlotPrefab").objectReferenceValue = invsScript;
            dSo.FindProperty("_startBattleButton").objectReferenceValue = startBattleBtn.GetComponent<Button>();
            dSo.FindProperty("_backToMapButton").objectReferenceValue = backBtn.GetComponent<Button>();
            dSo.FindProperty("_closeInventoryButton").objectReferenceValue = closeInvBtn.GetComponent<Button>();
            dSo.FindProperty("_squadPowerText").objectReferenceValue = powerTmp;
            dSo.FindProperty("_activeSynchroText").objectReferenceValue = syncTmp;
            dSo.ApplyModifiedProperties();
        }
    }

    private static GameObject CreateButtonWithText(string name, Transform parent, string text)
    {
        var btnObj = CreateUIObject(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f, 1f);
        btnObj.GetComponent<LayoutElement>().minHeight = 80;
        btnObj.GetComponent<LayoutElement>().minWidth = 200;

        var textObj = CreateUIObject("Text", btnObj.transform, typeof(TextMeshProUGUI));
        var tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        SetFullScreen(textObj.GetComponent<RectTransform>());

        return btnObj;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        foreach (var comp in components)
        {
            obj.AddComponent(comp);
        }
        return obj;
    }

    private static void SetFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T CreateOrGetView<T>(Transform parent, string name, OutgameViewType viewType) where T : OutgameViewBase
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            SetFullScreen(rect);
        }

        T view = obj.GetComponent<T>();
        if (view == null) view = obj.AddComponent<T>();
        view.ViewType = viewType;
        
        return view;
    }
}
