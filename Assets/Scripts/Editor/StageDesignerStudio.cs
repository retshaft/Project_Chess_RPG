#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CheckmateRPG.Progression;
using CheckmateRPG.Data;
using CheckmateRPG.Units;
using CheckmateRPG.Components;

namespace CheckmateRPG.Editor
{
    public class StageDesignerStudio : EditorWindow
    {
        private string _stageId = "STG-1-1";
        private string _stageName = "전초기지 돌파 (Tutorial)";
        private int _recommendedLevel = 1;
        private ObjectiveType _objective = ObjectiveType.KingDefeat;
        private int _rewardTP = 50;
        private float _initialEnemyAP = 30f;
        private float _enemyAPRegen = 4f;
        private float _maxEnemyAP = 100f;
        private string _description = "체스판 위의 수비형 대기 진형을 유추하고 적 킹을 제압하세요.";
        private string _briefing = "적 기물이 수비형(Defensive) 진형을 유지하며 대기합니다. 사거리 외 접근 시 카운터에 주의하세요.";
        private AIBehaviorType _defaultEnemyAI = AIBehaviorType.Defensive;

        [MenuItem("Tools/Stage Designer Studio")]
        [MenuItem("CheckmateRPG/Stage Designer Studio")]
        public static void ShowWindow()
        {
            GetWindow<StageDesignerStudio>("Stage Designer Studio");
        }

        [MenuItem("CheckmateRPG/Bake 4 Campaign AI Stages", false, 25)]
        public static void BakeAllFromMenu()
        {
            BakeDefaultCampaignStages();
        }

        private void OnGUI()
        {
            GUILayout.Label("🏆 [Phase 11] 캠페인 스테이지 & AI 대전 디자이너", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _stageId = EditorGUILayout.TextField("Stage ID", _stageId);
            _stageName = EditorGUILayout.TextField("Stage Name", _stageName);
            _recommendedLevel = EditorGUILayout.IntSlider("Recommended Level", _recommendedLevel, 1, 30);
            _objective = (ObjectiveType)EditorGUILayout.EnumPopup("Objective Type", _objective);
            _rewardTP = EditorGUILayout.IntSlider("King TP Reward (No-Gacha)", _rewardTP, 10, 500);

            EditorGUILayout.Space();
            GUILayout.Label("⚡ Enemy Commander AP Tempo", EditorStyles.boldLabel);
            _initialEnemyAP = EditorGUILayout.Slider("Initial Enemy AP", _initialEnemyAP, 0f, 100f);
            _enemyAPRegen = EditorGUILayout.Slider("Enemy AP Regen/Sec", _enemyAPRegen, 1f, 20f);
            _maxEnemyAP = EditorGUILayout.Slider("Max Enemy AP", _maxEnemyAP, 50f, 200f);

            EditorGUILayout.Space();
            GUILayout.Label("📖 Descriptions & Tactical Briefing", EditorStyles.boldLabel);
            _description = EditorGUILayout.TextField("Description", _description);
            _briefing = EditorGUILayout.TextArea(_briefing, GUILayout.Height(60));

            EditorGUILayout.Space();
            _defaultEnemyAI = (AIBehaviorType)EditorGUILayout.EnumPopup("Default Enemy AI", _defaultEnemyAI);

            EditorGUILayout.Space(15);
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("⚡ Bake & Save This Custom Stage SO", GUILayout.Height(35)))
            {
                BakeCustomStage();
            }

            EditorGUILayout.Space(10);
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("🔥 [M11] 4대 공식 캠페인 AI 대전 스테이지 자동 베이킹", GUILayout.Height(45)))
            {
                BakeDefaultCampaignStages();
            }
            GUI.backgroundColor = Color.white;
        }

        private void BakeCustomStage()
        {
            EnsureDirectories();
            string assetPath = $"Assets/Resources/Stages/Custom_{_stageId.Replace("-", "_")}.asset";
            StageData stage = EnsureStageAsset(assetPath);
            stage.StageID = _stageId;
            stage.StageName = _stageName;
            stage.RecommendedLevel = _recommendedLevel;
            stage.Objective = _objective;
            stage.RewardTP = _rewardTP;
            stage.InitialEnemyAP = _initialEnemyAP;
            stage.EnemyAPRegen = _enemyAPRegen;
            stage.MaxEnemyAP = _maxEnemyAP;
            stage.Description = _description;
            stage.TacticalBriefing = _briefing;

            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[StageDesignerStudio] Saved custom stage: {assetPath}");
        }

        public static void BakeDefaultCampaignStages()
        {
            EnsureDirectories();

            // 1. Create or load sample Enemy Units
            UnitData pawn = GetOrCreateEnemyUnit("Enemy_BlackPawn", "Black Pawn", ChessPieceType.Pawn, 35, 12, 1, new Color(0.2f, 0.2f, 0.2f));
            UnitData knight = GetOrCreateEnemyUnit("Enemy_BlackKnight", "Black Knight", ChessPieceType.Knight, 60, 24, 2, new Color(0.15f, 0.15f, 0.2f));
            UnitData bishop = GetOrCreateEnemyUnit("Enemy_BlackBishop", "Black Bishop", ChessPieceType.Bishop, 50, 20, 2, new Color(0.2f, 0.1f, 0.2f));
            UnitData rook = GetOrCreateEnemyUnit("Enemy_BlackRook", "Black Rook", ChessPieceType.Rook, 80, 28, 3, new Color(0.25f, 0.2f, 0.15f));
            UnitData queen = GetOrCreateEnemyUnit("Enemy_BlackQueen", "Black Queen", ChessPieceType.Queen, 120, 35, 4, new Color(0.3f, 0.1f, 0.1f));
            UnitData king = GetOrCreateEnemyUnit("Enemy_BlackKing", "Black King", ChessPieceType.King, 200, 40, 5, new Color(0.4f, 0.1f, 0.1f));

            // Stage 1-1: 전초기지 돌파
            StageData stg1 = EnsureStageAsset("Assets/Resources/Stages/Stage_1_1_Outpost.asset");
            stg1.StageID = "STG 1-1";
            stg1.StageName = "전초기지 돌파";
            stg1.RecommendedLevel = 1;
            stg1.RewardTP = 50;
            stg1.Objective = ObjectiveType.KingDefeat;
            stg1.InitialEnemyAP = 20f;
            stg1.EnemyAPRegen = 3f;
            stg1.MaxEnemyAP = 100f;
            stg1.Description = "프랙탈 존 외각의 체스 수비선을 돌파하세요.";
            stg1.TacticalBriefing = "적 기물이 수비형(Defensive) 진형을 유지하며 대기합니다. 사거리 외 접근 시 카운터를 주의하세요.";
            stg1.EnemySpawns.Clear();
            stg1.EnemySpawns.Add(CreateSpawn(pawn, new Vector2Int(2, 5), true, AIBehaviorType.Defensive, ResonanceRoleType.Defensive, 0));
            stg1.EnemySpawns.Add(CreateSpawn(pawn, new Vector2Int(4, 5), true, AIBehaviorType.Defensive, ResonanceRoleType.Defensive, 0));
            stg1.EnemySpawns.Add(CreateSpawn(king, new Vector2Int(3, 7), true, AIBehaviorType.Defensive, ResonanceRoleType.Defensive, 0));
            EditorUtility.SetDirty(stg1);

            // Stage 1-2: 광전사의 돌진
            StageData stg2 = EnsureStageAsset("Assets/Resources/Stages/Stage_1_2_BerserkerRush.asset");
            stg2.StageID = "STG 1-2";
            stg2.StageName = "광전사의 돌진";
            stg2.RecommendedLevel = 3;
            stg2.RewardTP = 80;
            stg2.Objective = ObjectiveType.Annihilation;
            stg2.InitialEnemyAP = 50f;
            stg2.EnemyAPRegen = 6f;
            stg2.MaxEnemyAP = 100f;
            stg2.Description = "AP 보존 없이 즉시 돌격을 단행하는 적 나이트 부대의 파전입니다.";
            stg2.TacticalBriefing = "적 나이트 부대가 광전사(Berserker) 성향으로 AP가 모이는 즉시 저축 없이 돌격해 옵니다. 공격형 공명 1단계(+10% 공) 적용.";
            stg2.EnemySpawns.Clear();
            stg2.EnemySpawns.Add(CreateSpawn(knight, new Vector2Int(2, 6), true, AIBehaviorType.Berserker, ResonanceRoleType.Offensive, 1));
            stg2.EnemySpawns.Add(CreateSpawn(knight, new Vector2Int(4, 6), true, AIBehaviorType.Berserker, ResonanceRoleType.Offensive, 1));
            stg2.EnemySpawns.Add(CreateSpawn(pawn, new Vector2Int(3, 5), true, AIBehaviorType.Berserker, ResonanceRoleType.Offensive, 1));
            EditorUtility.SetDirty(stg2);

            // Stage 1-3: 전략가의 참호전
            StageData stg3 = EnsureStageAsset("Assets/Resources/Stages/Stage_1_3_TacticianFortress.asset");
            stg3.StageID = "STG 1-3";
            stg3.StageName = "전략가의 참호전";
            stg3.RecommendedLevel = 5;
            stg3.RewardTP = 120;
            stg3.Objective = ObjectiveType.KingDefeat;
            stg3.InitialEnemyAP = 30f;
            stg3.EnemyAPRegen = 5f;
            stg3.MaxEnemyAP = 120f;
            stg3.Description = "치유와 원거리 마법 장격을 준비하는 적 전술 대열과의 수싸움입니다.";
            stg3.TacticalBriefing = "적 비숍과 룩이 전략가(Tactician) 및 수비형(Defensive) 성향으로 협력합니다. AP가 70% 이하일 때 강력한 스킬을 위해 저축하므로 AP 템포를 빼앗으세요.";
            stg3.EnemySpawns.Clear();
            stg3.EnemySpawns.Add(CreateSpawn(bishop, new Vector2Int(2, 6), true, AIBehaviorType.Tactician, ResonanceRoleType.Support, 2));
            stg3.EnemySpawns.Add(CreateSpawn(rook, new Vector2Int(4, 6), true, AIBehaviorType.Defensive, ResonanceRoleType.Defensive, 2));
            stg3.EnemySpawns.Add(CreateSpawn(king, new Vector2Int(3, 7), true, AIBehaviorType.Tactician, ResonanceRoleType.Defensive, 2));
            EditorUtility.SetDirty(stg3);

            // Stage 1-4: 보스 체스마스터의 심판
            StageData stg4 = EnsureStageAsset("Assets/Resources/Stages/Stage_1_4_BossCheckmate.asset");
            stg4.StageID = "BOSS 1-4";
            stg4.StageName = "체스마스터의 심판";
            stg4.RecommendedLevel = 10;
            stg4.RewardTP = 200;
            stg4.Objective = ObjectiveType.KingDefeat;
            stg4.InitialEnemyAP = 60f;
            stg4.EnemyAPRegen = 8f;
            stg4.MaxEnemyAP = 150f;
            stg4.Description = "프랙탈 존 심층부의 절대 지휘관과의 결전입니다.";
            stg4.TacticalBriefing = "보스 퀸과 킹입니다. 공명 3단계(AP 할인 및 TP 보강)와 4대 절대 조건 트리거(체크메이트/위협회피/도발/낙사)가 극도로 발현됩니다!";
            stg4.EnemySpawns.Clear();
            stg4.EnemySpawns.Add(CreateSpawn(queen, new Vector2Int(3, 6), true, AIBehaviorType.Tactician, ResonanceRoleType.Offensive, 3));
            stg4.EnemySpawns.Add(CreateSpawn(rook, new Vector2Int(1, 6), true, AIBehaviorType.Defensive, ResonanceRoleType.Defensive, 3));
            stg4.EnemySpawns.Add(CreateSpawn(rook, new Vector2Int(5, 6), true, AIBehaviorType.Defensive, ResonanceRoleType.Defensive, 3));
            stg4.EnemySpawns.Add(CreateSpawn(king, new Vector2Int(3, 7), true, AIBehaviorType.Tactician, ResonanceRoleType.Defensive, 3));
            EditorUtility.SetDirty(stg4);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("✅ [StageDesignerStudio] Successfully baked 4 Canonical Campaign AI Stages in Resources/Stages/!");
        }

        private static EnemySpawnInfo CreateSpawn(UnitData unit, Vector2Int pos, bool overrideAI, AIBehaviorType ai, ResonanceRoleType role, int stage)
        {
            return new EnemySpawnInfo
            {
                EnemyUnit = unit,
                GridPosition = pos,
                OverrideAIBehavior = overrideAI,
                AIBehavior = ai,
                EnemyResonanceRole = role,
                EnemyResonanceStage = stage
            };
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Stages")) AssetDatabase.CreateFolder("Assets/Resources", "Stages");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Units")) AssetDatabase.CreateFolder("Assets/Resources", "Units");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Units/Enemies")) AssetDatabase.CreateFolder("Assets/Resources/Units", "Enemies");
        }

        private static StageData EnsureStageAsset(string path)
        {
            StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(stage, path);
            }
            return stage;
        }

        private static UnitData GetOrCreateEnemyUnit(string assetName, string displayName, ChessPieceType pieceType, int hp, int attackCost, int weight, Color color)
        {
            string path = $"Assets/Resources/Units/Enemies/{assetName}.asset";
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UnitData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.UnitName = displayName;
            data.PieceType = pieceType;
            data.MaxHealth = hp;
            data.AttackCostAP = attackCost;
            data.Weight = weight;
            data.MaxSP = 100f;

            string prefabPath = $"Assets/Prefabs/Units/{assetName}.prefab";
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Units")) AssetDatabase.CreateFolder("Assets/Prefabs", "Units");
            
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = assetName;
                if (go.GetComponent<Renderer>() != null)
                    go.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
                go.AddComponent<UnitBrain>();
                go.AddComponent<MovementComponent>();
                go.AddComponent<CombatComponent>();
                go.AddComponent<StatusEffectComponent>();
                var team = go.AddComponent<TeamComponent>();
                team.SetIsEnemy(true);
                prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                GameObject.DestroyImmediate(go);
            }
            data.Prefab = prefab;

            EditorUtility.SetDirty(data);
            return data;
        }
    }
}
#endif
