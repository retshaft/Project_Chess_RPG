#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CheckmateRPG.Progression;
using CheckmateRPG.Data;
using CheckmateRPG.UI;
using CheckmateRPG.Units;
using CheckmateRPG.Components;

namespace CheckmateRPG.EditorScripts
{
    public static class M12ContentSetupMenu
    {
        [MenuItem("CheckmateRPG/Setup M12 (Generate Stages & UI)", false, 20)]
        public static void SetupM12()
        {
            GenerateDummyData();
            SetupOutgameScene();
            Debug.Log("[M12] Content and Outgame Scene setup complete.");
        }

        private static void GenerateDummyData()
        {
            string dataPath = "Assets/Data/Stages";
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Stages"))
                AssetDatabase.CreateFolder("Assets/Data", "Stages");

            string unitPath = "Assets/Data/Units/Enemies";
            if (!AssetDatabase.IsValidFolder("Assets/Data/Units"))
                AssetDatabase.CreateFolder("Assets/Data", "Units");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Units/Enemies"))
                AssetDatabase.CreateFolder("Assets/Data/Units", "Enemies");

            // 1. Create Enemy Prefabs
            GameObject pawnPrefab = CreateDummyEnemyPrefab("BlackPawnPrefab", new Color(0.2f, 0.2f, 0.2f));
            GameObject knightPrefab = CreateDummyEnemyPrefab("BlackKnightPrefab", new Color(0.1f, 0.1f, 0.1f));

            // 2. Create UnitData
            UnitData pawnData = CreateUnitData(unitPath + "/Enemy_BlackPawn.asset", "Black Pawn", pawnPrefab);
            UnitData knightData = CreateUnitData(unitPath + "/Enemy_BlackKnight.asset", "Black Knight", knightPrefab);

            // 3. Create StageData
            StageData stage1 = CreateStageData(dataPath + "/Stage_01.asset", "STG-01", "Pawn Vanguard");
            stage1.EnemySpawns.Clear();
            stage1.EnemySpawns.Add(new EnemySpawnInfo { EnemyUnit = pawnData, GridPosition = new Vector2Int(3, 5) });
            stage1.EnemySpawns.Add(new EnemySpawnInfo { EnemyUnit = pawnData, GridPosition = new Vector2Int(4, 5) });
            EditorUtility.SetDirty(stage1);

            StageData stage2 = CreateStageData(dataPath + "/Stage_02.asset", "STG-02", "Knight's Charge");
            stage2.EnemySpawns.Clear();
            stage2.EnemySpawns.Add(new EnemySpawnInfo { EnemyUnit = knightData, GridPosition = new Vector2Int(2, 6) });
            stage2.EnemySpawns.Add(new EnemySpawnInfo { EnemyUnit = knightData, GridPosition = new Vector2Int(5, 6) });
            stage2.EnemySpawns.Add(new EnemySpawnInfo { EnemyUnit = pawnData, GridPosition = new Vector2Int(3, 5) });
            EditorUtility.SetDirty(stage2);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject CreateDummyEnemyPrefab(string name, Color color)
        {
            string prefabPath = $"Assets/Prefabs/Units/{name}.prefab";
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Units")) AssetDatabase.CreateFolder("Assets/Prefabs", "Units");

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.GetComponent<Renderer>().sharedMaterial.color = color;
            go.AddComponent<UnitBrain>();
            go.AddComponent<MovementComponent>();
            go.AddComponent<CombatComponent>();
            go.AddComponent<StatusEffectComponent>();
            go.AddComponent<TeamComponent>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            GameObject.DestroyImmediate(go);
            return prefab;
        }

        private static UnitData CreateUnitData(string path, string unitName, GameObject prefab)
        {
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UnitData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.UnitName = unitName;
            data.Prefab = prefab;
            data.MaxHealth = 30;
            data.MaxSP = 10;
            data.Weight = 1;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static StageData CreateStageData(string path, string id, string stageName)
        {
            StageData data = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.StageID = id;
            data.StageName = stageName;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void SetupOutgameScene()
        {
            // 1. Create and Save new Scene
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
                
            string scenePath = "Assets/Scenes/OutgameScene.unity";
            UnityEngine.SceneManagement.Scene newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            
            // Camera
            GameObject camObj = new GameObject("Main Camera", typeof(Camera));
            camObj.tag = "MainCamera";

            // 2. M11의 DeckBuilder 셋업 호출 (현재 활성 씬인 OutgameScene에 GameManagers, Canvas, DeckBuilderPanel을 생성함)
            M11SceneSetupMenu.SetupScene();

            // GameManagers에 StageManager가 없다면 추가
            GameObject managersGO = GameObject.Find("GameManagers");
            if (managersGO != null && managersGO.GetComponent<StageManager>() == null)
            {
                managersGO.AddComponent<StageManager>();
            }

            // 3. 만들어진 Canvas 찾기
            Canvas canvas = GameObject.FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 4. Stage Select Panel 생성
            GameObject panelGO = new GameObject("StageSelectPanel", typeof(RectTransform), typeof(Image), typeof(StageSelectUI));
            panelGO.transform.SetParent(canvas.transform, false);
            panelGO.transform.SetAsLastSibling(); // 맨 위에 보이도록 (덱빌더 가림)
            
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
            panelGO.GetComponent<Image>().color = new Color(0.15f, 0.1f, 0.2f, 1f); // Dark purple

            // Title
            GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(panelGO.transform, false);
            RectTransform titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.8f); titleRect.anchorMax = new Vector2(1, 0.95f);
            titleRect.offsetMin = Vector2.zero; titleRect.offsetMax = Vector2.zero;
            var tmp = titleGO.GetComponent<TextMeshProUGUI>();
            tmp.text = "SELECT STAGE";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 50; tmp.fontStyle = FontStyles.Bold; tmp.color = Color.white;

            // List Container
            GameObject listGO = new GameObject("StageList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGO.transform.SetParent(panelGO.transform, false);
            RectTransform listRect = listGO.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.2f, 0.1f); listRect.anchorMax = new Vector2(0.8f, 0.7f);
            listRect.offsetMin = Vector2.zero; listRect.offsetMax = Vector2.zero;
            var vlg = listGO.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 20; vlg.childControlHeight = false; vlg.childControlWidth = true;

            // Prefab Button
            GameObject btnPrefab = new GameObject("StageButtonPrefab", typeof(RectTransform), typeof(Image), typeof(Button));
            btnPrefab.transform.SetParent(panelGO.transform, false);
            btnPrefab.SetActive(false);
            var btnRect = btnPrefab.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(0, 80);
            btnPrefab.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(btnPrefab.transform, false);
            var tr = textGO.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var btnTmp = textGO.GetComponent<TextMeshProUGUI>();
            btnTmp.text = "Stage"; btnTmp.alignment = TextAlignmentOptions.Center; btnTmp.fontSize = 30; btnTmp.color = Color.white;

            // 5. DeckBuilder UI 찾기 및 상호 연결
            DeckBuilderUI deckBuilderUI = GameObject.FindObjectOfType<DeckBuilderUI>();
            
            SerializedObject so = new SerializedObject(panelGO.GetComponent<StageSelectUI>());
            so.FindProperty("_stageListContainer").objectReferenceValue = listGO.transform;
            so.FindProperty("_stageButtonPrefab").objectReferenceValue = btnPrefab;
            if (deckBuilderUI != null)
            {
                so.FindProperty("_deckBuilderPanel").objectReferenceValue = deckBuilderUI.gameObject;
            }
            
            // Auto add created stages
            string[] guids = AssetDatabase.FindAssets("t:StageData");
            var listProp = so.FindProperty("_availableStages");
            listProp.ClearArray();
            int index = 0;
            foreach (var guid in guids)
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageData>(AssetDatabase.GUIDToAssetPath(guid));
                if (stage != null)
                {
                    listProp.InsertArrayElementAtIndex(index);
                    listProp.GetArrayElementAtIndex(index).objectReferenceValue = stage;
                    index++;
                }
            }
            so.ApplyModifiedProperties();

            // DeckBuilder에 StageSelect 패널 연결, BackToMap 버튼 생성
            if (deckBuilderUI != null)
            {
                SerializedObject dbSO = new SerializedObject(deckBuilderUI);
                dbSO.FindProperty("_stageSelectPanel").objectReferenceValue = panelGO;
                
                // Back To Map 버튼 생성
                GameObject backBtnGO = new GameObject("BackToMapButton", typeof(RectTransform), typeof(Image), typeof(Button));
                backBtnGO.transform.SetParent(deckBuilderUI.transform, false);
                RectTransform backBtnRect = backBtnGO.GetComponent<RectTransform>();
                backBtnRect.anchorMin = new Vector2(0.05f, 0.9f);
                backBtnRect.anchorMax = new Vector2(0.2f, 0.98f);
                backBtnRect.offsetMin = Vector2.zero;
                backBtnRect.offsetMax = Vector2.zero;
                backBtnGO.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f); // Red button

                GameObject backTextGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                backTextGO.transform.SetParent(backBtnGO.transform, false);
                RectTransform backTextRect = backTextGO.GetComponent<RectTransform>();
                backTextRect.anchorMin = Vector2.zero; backTextRect.anchorMax = Vector2.one;
                backTextRect.offsetMin = Vector2.zero; backTextRect.offsetMax = Vector2.zero;
                TextMeshProUGUI backTmp = backTextGO.GetComponent<TextMeshProUGUI>();
                backTmp.text = "< BACK"; backTmp.alignment = TextAlignmentOptions.Center;
                backTmp.fontSize = 24; backTmp.fontStyle = FontStyles.Bold; backTmp.color = Color.white;

                dbSO.FindProperty("_backToMapButton").objectReferenceValue = backBtnGO.GetComponent<Button>();
                dbSO.ApplyModifiedProperties();

                // 덱빌더는 처음에 숨김
                deckBuilderUI.gameObject.SetActive(false);
            }
            
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"[M12] Saved OutgameScene to {scenePath}");
        }
    }
}
#endif
