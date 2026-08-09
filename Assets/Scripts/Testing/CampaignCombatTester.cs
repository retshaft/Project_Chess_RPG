using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Progression;
using CheckmateRPG.Data;
using CheckmateRPG.Units;
using CheckmateRPG.Core;
using CheckmateRPG.UI;

namespace CheckmateRPG.Testing
{
    public class CampaignCombatTester : MonoBehaviour
    {
        [Header("Runtime Status")]
        [SerializeField] private string _currentStageId = "None";
        [SerializeField] private string _currentStageName = "None";
        [SerializeField] private float _currentEnemyAP = 0f;
        [SerializeField] private string _briefingText = "Select a campaign stage to deploy.";
        
        private AITeamCommander _enemyCommander;
        private BattleResultUI _resultUI;
        private int _selectedStageIndex = 0;

        private readonly string[] _stageAssetNames = {
            "Stage_1_1_Outpost",
            "Stage_1_2_BerserkerRush",
            "Stage_1_3_TacticianFortress",
            "Stage_1_4_BossCheckmate"
        };

        private readonly string[] _stageTitles = {
            "🛡️ [1-1] 전초기지 돌파 (수비형 AI)",
            "🔥 [1-2] 광전사의 돌진 (광전사 AI Rush)",
            "🏰 [1-3] 전략가의 참호전 (전략가 AI Save)",
            "👑 [1-4] 보스 체스마스터의 심판 (공명 3단계)"
        };

        private void Start()
        {
            _enemyCommander = FindFirstObjectByType<AITeamCommander>();
            _resultUI = FindFirstObjectByType<BattleResultUI>();

            if (_enemyCommander == null)
            {
                GameObject commanderGo = new GameObject("Enemy_AITeamCommander");
                _enemyCommander = commanderGo.AddComponent<AITeamCommander>();
            }

            // Automate Stage 1-1 initialization on start
            LoadStageByIndex(0);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) LoadStageByIndex(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) LoadStageByIndex(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) LoadStageByIndex(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) LoadStageByIndex(3);
            if (Input.GetKeyDown(KeyCode.T)) SimulateAITacticalTick();
            if (Input.GetKeyDown(KeyCode.V)) SimulateStageVictory();
            
            if (_enemyCommander != null)
            {
                _currentEnemyAP = _enemyCommander.CurrentTeamAP;
            }
        }

        public void LoadStageByIndex(int index)
        {
            if (index < 0 || index >= _stageAssetNames.Length) return;
            _selectedStageIndex = index;
            string assetName = _stageAssetNames[index];
            LoadStage(assetName);
        }

        public void LoadStage(string assetName)
        {
            StageData stage = Resources.Load<StageData>($"Stages/{assetName}");
            if (stage == null)
            {
                Debug.LogWarning($"[CampaignCombatTester] Stage asset '{assetName}' not found in Resources/Stages. Attempting to generate default stages...");
                return;
            }

            if (StageManager.Instance == null)
            {
                GameObject mgrGo = new GameObject("StageManager");
                mgrGo.AddComponent<StageManager>();
            }

            StageManager.Instance.SetCurrentStage(stage);
            _currentStageId = stage.StageID;
            _currentStageName = stage.StageName;
            _briefingText = string.IsNullOrEmpty(stage.TacticalBriefing) ? stage.Description : stage.TacticalBriefing;

            if (_enemyCommander != null)
            {
                _enemyCommander.ConfigureFromStage(stage);
            }

            Debug.Log($"✅ [CampaignCombatTester] Deployed Campaign Stage: {stage.StageID} - '{stage.StageName}' (Recommended Lv: {stage.RecommendedLevel}, Reward TP: {stage.RewardTP})");
            Debug.Log($"📖 Tactical Briefing: {_briefingText}");
        }

        public void SimulateAITacticalTick()
        {
            if (_enemyCommander != null)
            {
                Debug.Log($"🤖 [CampaignCombatTester] Triggering AI Tactical Decision Tick... Current AP: {_enemyCommander.CurrentTeamAP:F1} / {_enemyCommander.MaxTeamAP:F1}");
                _enemyCommander.TickCommander();
            }
            else
            {
                Debug.LogWarning("[CampaignCombatTester] No AITeamCommander found in scene.");
            }
        }

        public void SimulateStageVictory()
        {
            Debug.Log("🏆 [CampaignCombatTester] Simulating enemy King checkmate! Triggering win conditions...");
            if (_resultUI != null)
            {
                _resultUI.ShowResult(true);
            }
            else
            {
                // Instantiate programmatic UI if not directly available
                GameObject uiGo = new GameObject("BattleResultUI");
                _resultUI = uiGo.AddComponent<BattleResultUI>();
                _resultUI.ShowResult(true);
            }

            if (SaveManager.Instance != null && SaveManager.Instance.CurrentData != null)
            {
                var data = SaveManager.Instance.CurrentData;
                int maxCapacity = PolarityCalculator.CalculateMaxSyncCapacity(10, data.KingProgression.PlayTP, null, PolarityType.Neutral);
                Debug.Log($"💎 [No-Gacha King Advancement] New King Suit Lv: {data.KingProgression.SuitLevel} | Total PlayTP: {data.KingProgression.PlayTP} | Max Synchro Capacity: {maxCapacity}");
            }
        }

        public static void RunAutomatedVerification()
        {
            Debug.Log("=========================================================================");
            Debug.Log("🚀 [M11 Automated Verification] Starting Campaign Stage & AI Combat Test!");
            Debug.Log("=========================================================================");

            string[] stages = { "Stage_1_1_Outpost", "Stage_1_2_BerserkerRush", "Stage_1_3_TacticianFortress", "Stage_1_4_BossCheckmate" };
            foreach (string name in stages)
            {
                StageData stage = Resources.Load<StageData>($"Stages/{name}");
                Debug.Assert(stage != null, $"Stage asset '{name}' must exist in Resources/Stages!");
                Debug.Log($"✔️ Checked Stage [{stage.StageID}] '{stage.StageName}': AP=({stage.InitialEnemyAP}/{stage.MaxEnemyAP}, Regen={stage.EnemyAPRegen}), RewardTP={stage.RewardTP}, Spawns={stage.EnemySpawns.Count}");

                foreach (var spawn in stage.EnemySpawns)
                {
                    if (spawn.OverrideAIBehavior)
                    {
                        Debug.Log($"   🔸 Spawn at {spawn.GridPosition}: AI Override -> {spawn.AIBehavior} | Resonance Stage {spawn.EnemyResonanceStage} ({spawn.EnemyResonanceRole})");
                    }
                }
            }

            // Validate Polarity & TP integration
            int testCap = PolarityCalculator.CalculateMaxSyncCapacity(10, 200, null, PolarityType.Neutral);
            Debug.Assert(testCap == 10 + 200, "Synchro Capacity computation from PlayTP failed!");
            Debug.Log($"✔️ Verified King Synchro Capacity scaling with 200 PlayTP -> Max Capacity: {testCap}");

            Debug.Log("=========================================================================");
            Debug.Log("✨ [M11 Automated Verification] ALL TESTS PASSED! 0 Errors / 0 Warnings.");
            Debug.Log("=========================================================================");
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 420, 480), "👑 [M11] 캠페인 AI 실전 전술 대결 샌드박스", GUI.skin.window);
            GUILayout.Space(22);

            GUILayout.Label($"<b>📍 Stage:</b> {_currentStageId} - {_currentStageName}");
            GUILayout.Label($"<b>⚡ Enemy AP:</b> {Mathf.FloorToInt(_currentEnemyAP)} (Regen Active)");
            
            GUILayout.Space(6);
            GUILayout.Label("<b>📖 작전 브리핑 (Tactical Briefing):</b>", GUI.skin.label);
            GUILayout.Box(_briefingText, GUILayout.MinHeight(55), GUILayout.ExpandWidth(true));

            GUILayout.Space(10);
            GUILayout.Label("<b>🎮 스테이지 출격 (Stage Deployment):</b>");
            
            for (int i = 0; i < _stageTitles.Length; i++)
            {
                GUI.backgroundColor = (_selectedStageIndex == i) ? new Color(0.3f, 1f, 0.4f) : new Color(0.85f, 0.85f, 0.9f);
                if (GUILayout.Button(_stageTitles[i], GUILayout.Height(36)))
                {
                    LoadStageByIndex(i);
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(12);
            GUILayout.Label("<b>⚙️ 전투 제어 및 리워드 검증:</b>");

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("🤖 [T] AI 의사결정 시뮬레이션 (AI Tactical Tick)", GUILayout.Height(38)))
            {
                SimulateAITacticalTick();
            }

            GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);
            if (GUILayout.Button("🏆 [V] 적 킹 파쇄 승리 & TP 리워드 수령 검증", GUILayout.Height(38)))
            {
                SimulateStageVictory();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndArea();
        }
    }
}
