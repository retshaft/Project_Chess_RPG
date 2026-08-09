#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.UI;
using CheckmateRPG.Testing;
using CheckmateRPG.Data;
using CheckmateRPG.Core;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;
using UnityEngine.UI;

namespace CheckmateRPG.Editor
{
    public static class CombatSandboxSetupEditor
    {
        [MenuItem("CheckmateRPG/Launch Combat Sandbox (전투 씬 기반 샌드박스 생성 및 시연)", false, 10)]
        [MenuItem("Tools/Launch Combat Sandbox", false, 10)]
        public static void SetupSandboxScene()
        {
            string referenceScenePath = "Assets/Scenes/Test/BattleScene.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(referenceScenePath))
            {
                Debug.LogError($"[CombatSandboxSetupEditor] 기준 전투 씬 '{referenceScenePath}'을(를) 찾을 수 없습니다! 올바른 전투 씬 경로를 확인해주세요.");
                return;
            }

            // 1. 실제 전투 씬(BattleScene)을 열어 기반으로 삼습니다 (그리드, AP매니저, 입력 및 UI 시스템 완비)
            var scene = EditorSceneManager.OpenScene(referenceScenePath, OpenSceneMode.Single);

            // 기존 샌드박스 생성물 정리
            CleanupOldSandboxObjects();

            // 2. BattleManager의 자동 유닛 스폰(spawnOnAwake) 비활성화
            var battleMgr = Object.FindFirstObjectByType<BattleManager>();
            if (battleMgr != null)
            {
                var so = new SerializedObject(battleMgr);
                var prop = so.FindProperty("_spawnOnAwake");
                if (prop != null)
                {
                    prop.boolValue = false;
                    so.ApplyModifiedProperties();
                }
            }

            // 3. 전투 보드 GridSystem 참조 확보
            var grid = Object.FindFirstObjectByType<GridSystem>();
            if (grid == null)
            {
                Debug.LogWarning("[CombatSandboxSetupEditor] GridSystem을 씬에서 찾지 못했습니다. 0,0 좌표 기준으로 배치합니다.");
            }

            // 4. 기획 캐릭터 3인 스쿼드 에셋 일괄 셋업 및 Bake 검증
            OperatorDesignerStudio.BakeAllFromMenu();

            // 5. 3인 스쿼드 기물 생성 및 전략 배치
            // 키아라(검객) : 3, 2 전방위 전술 거점
            GameObject kiaraGo = CreateOperatorUnit("Player_Operator_Kiara", new Vector2Int(3, 2), new Color(0.15f, 0.75f, 1f), new Vector3(0.7f, 0.7f, 0.7f), "Units/Kiara", grid);
            // 엘레나(저격수) : 2, 1 후방 좌측 저격 스팟
            GameObject elenaGo = CreateOperatorUnit("Player_Operator_Elena", new Vector2Int(2, 1), new Color(0.85f, 0.9f, 0.95f), new Vector3(0.65f, 0.65f, 0.65f), "Units/Elena", grid);
            // 베스타(공성병기) : 4, 1 후방 우측 공성 캐논 고지
            GameObject vestaGo = CreateOperatorUnit("Player_Operator_Vesta", new Vector2Int(4, 1), new Color(0.95f, 0.4f, 0.1f), new Vector3(0.85f, 0.85f, 0.85f), "Units/Vesta", grid);


            // 6. 적 보스 (샌드백 dummy 기물) 생성 (그리드 좌표 3, 5 배치)
            Vector2Int bossCell = new Vector2Int(3, 5);
            GameObject bossGo = new GameObject("Enemy_King_Boss_Sandbag");
            if (grid != null) bossGo.transform.position = grid.GridToWorld(bossCell);

            var bVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bVisual.name = "Visual";
            bVisual.transform.SetParent(bossGo.transform, false);
            bVisual.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            bVisual.transform.localScale = new Vector3(1.1f, 1.5f, 1.1f);
            Object.DestroyImmediate(bVisual.GetComponent<Collider>());

            var bRend = bVisual.GetComponent<Renderer>();
            if (bRend != null)
            {
                var bMat = new Material(bRend.sharedMaterial);
                bMat.color = new Color(0.85f, 0.15f, 0.2f); // Dark Crimson Boss
                bRend.material = bMat;
            }

            var bHealth = bossGo.AddComponent<HealthComponent>();
            bossGo.AddComponent<MovementComponent>();
            bossGo.AddComponent<CombatComponent>();
            var bStatus = bossGo.AddComponent<StatusEffectComponent>();
            var bTeam = bossGo.AddComponent<TeamComponent>();
            bTeam.SetIsEnemy(true);

            var bCol = bossGo.AddComponent<CapsuleCollider>();
            bCol.center = new Vector3(0f, 0.8f, 0f);
            bCol.height = 1.6f;
            bCol.radius = 0.6f;
            bossGo.AddComponent<SPComponent>();

            UnitData bossData = ScriptableObject.CreateInstance<UnitData>();
            bossData.name = "SandbagBoss_Data";
            bossData.UnitName = "적군 수호장 (테스트 Dummy)";
            bossData.PieceType = ChessPieceType.King;
            bossData.MaxHealth = 50000f;
            bossData.Defense = 40f;
            bossData.Resistance = 0.2f;
            bossData.IsBoss = true;
            bossData.MoveRange = 2;
            bossData.MoveCostAP = 20f;
            
            var bBrain = bossGo.AddComponent<UnitBrain>();
            bBrain.Prepare(bossData, bossCell);
            bHealth.Initialise(bossData);

            var bHUD = new GameObject("WorldHUD");
            bHUD.transform.SetParent(bossGo.transform, false);
            var bStatusBar = bHUD.AddComponent<UnitStatusBar>();
            bStatusBar.Initialize(bossGo.transform);


            // 7. 서브컬처 전투 UI Overlay 및 연출 캔버스 셋업
            GameObject uiRoot = new GameObject("Subculture_UI_Overlays");
            var canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiRoot.AddComponent<CanvasScaler>();
            uiRoot.AddComponent<GraphicRaycaster>();

            var ftmGo = new GameObject("FloatingTextManager");
            ftmGo.AddComponent<FloatingTextManager>();

            var cutInGo = new GameObject("SkillCutInUI");
            cutInGo.transform.SetParent(uiRoot.transform, false);
            var cutIn = cutInGo.AddComponent<SkillCutInUI>();

            var checkmateGo = new GameObject("CheckmateCinematicUI");
            checkmateGo.transform.SetParent(uiRoot.transform, false);
            var checkmate = checkmateGo.AddComponent<CheckmateCinematicUI>();

            var synchroGo = new GameObject("KingSynchroUI");
            synchroGo.transform.SetParent(uiRoot.transform, false);
            var synchro = synchroGo.AddComponent<KingSynchroUI>();

            // 8. 샌드박스 테스터 콘솔 장착
            GameObject testerGo = new GameObject("Sandbox_Tester_Controller");
            var tester = testerGo.AddComponent<CombatSandboxTester>();
            tester.playerUnit = kiaraGo.transform;
            tester.squadUnits = new Transform[] { kiaraGo.transform, elenaGo.transform, vestaGo.transform };
            tester.bossUnit = bossGo.transform;
            tester.skillCutIn = cutIn;
            tester.checkmateCinematic = checkmate;
            tester.kingSynchro = synchro;

            // 9. 변경된 실제 전투 전술 씬을 CombatSandbox.unity로 독립 저장
            string savePath = "Assets/Scenes/CombatSandbox.unity";
            EditorSceneManager.SaveScene(scene, savePath);
            AssetDatabase.Refresh();

            Debug.Log($"[CombatSandboxSetupEditor] 3인 오퍼레이터 전술 스쿼드(키아라/엘레나/베스타)와 함께 샌드박스 씬 '{savePath}'을(를) 완벽히 구축 및 저장했습니다!");
        }

        private static GameObject CreateOperatorUnit(string goName, Vector2Int cell, Color color, Vector3 scale, string dataPath, GridSystem grid)
        {
            GameObject playerGo = new GameObject(goName);
            if (grid != null) playerGo.transform.position = grid.GridToWorld(cell);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(playerGo.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            visual.transform.localScale = scale;
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            var rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(rend.sharedMaterial);
                mat.color = color;
                rend.material = mat;
            }

            var health = playerGo.AddComponent<HealthComponent>();
            playerGo.AddComponent<MovementComponent>();
            playerGo.AddComponent<CombatComponent>();
            playerGo.AddComponent<StatusEffectComponent>();
            var team = playerGo.AddComponent<TeamComponent>();
            team.SetIsEnemy(false);

            var col = playerGo.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.height = 1.6f;
            col.radius = 0.4f;

            var sp = playerGo.AddComponent<SPComponent>();
            playerGo.AddComponent<SubclassComponent>();

            var data = Resources.Load<UnitData>(dataPath);
            var brain = playerGo.AddComponent<UnitBrain>();
            if (data != null)
            {
                brain.Prepare(data, cell);
                health.Initialise(data);
                sp.Initialise(data);
            }

            var hud = new GameObject("WorldHUD");
            hud.transform.SetParent(playerGo.transform, false);
            var statusBar = hud.AddComponent<UnitStatusBar>();
            statusBar.Initialize(playerGo.transform);

            return playerGo;
        }

        private static void CleanupOldSandboxObjects()
        {
            string[] names = {
                "Player_Operator_Kiara", "Player_Operator_Elena", "Player_Operator_Vesta",
                "Enemy_King_Boss_Sandbag", "Subculture_UI_Overlays", 
                "Sandbox_Tester_Controller", "FloatingTextManager"
            };
            foreach (var n in names)
            {
                var obj = GameObject.Find(n);
                if (obj != null) Object.DestroyImmediate(obj);
            }
        }
    }
}
#endif
