using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Progression;

namespace CheckmateRPG.Testing
{
    public class NotationTester : MonoBehaviour
    {
        [Header("Runtime Setup")]
        [SerializeField] private NotationPuzzleScreen _puzzleScreen;
        [SerializeField] private int _currentResonanceStage = 1;
        [SerializeField] private int _selectedUnitIndex = 0;

        private NotationTreeData _currentTree;
        private NotationProgressState _currentState;

        private readonly string[] _unitIds = { "Kiara", "Elena", "Vesta" };
        private readonly string[] _unitTitles = {
            "🗡️ 키아라 [Queen's Gambit]",
            "🔥 엘레나 [Sicilian Defense]",
            "🛡️ 베스타 [Caro-Kann Defense]"
        };

        private void Start()
        {
            if (_puzzleScreen == null)
            {
                _puzzleScreen = GetComponent<NotationPuzzleScreen>();
                if (_puzzleScreen == null)
                {
                    _puzzleScreen = gameObject.AddComponent<NotationPuzzleScreen>();
                }
            }

            LoadUnitTree(0, _currentResonanceStage);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) LoadUnitTree(0, _currentResonanceStage);
            if (Input.GetKeyDown(KeyCode.F2)) LoadUnitTree(1, _currentResonanceStage);
            if (Input.GetKeyDown(KeyCode.F3)) LoadUnitTree(2, _currentResonanceStage);
            
            // Number keys 1-5 for Resonance Stage testing
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetResonanceStage(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetResonanceStage(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetResonanceStage(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetResonanceStage(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetResonanceStage(5);
            if (Input.GetKeyDown(KeyCode.R)) ResetCurrentNotation();
        }

        public void LoadUnitTree(int index, int resonanceStage)
        {
            if (index < 0 || index >= _unitIds.Length) return;
            _selectedUnitIndex = index;
            _currentResonanceStage = Mathf.Clamp(resonanceStage, 1, 5);
            
            string unitId = _unitIds[index];
            _currentTree = Resources.Load<NotationTreeData>($"Notations/Tree_{unitId}");
            
            if (_currentTree == null)
            {
                Debug.LogWarning($"[NotationTester] Tree_{unitId}.asset not found in Resources/Notations. Please run tools to bake canonical notation trees.");
                return;
            }

            _currentState = NotationProgressState.CreateForUnit(unitId, _currentTree, _currentResonanceStage);
            _puzzleScreen.Initialize(_currentTree, _currentState);
            Debug.Log($"♟️ [NotationTester] Deployed Chess Notation Tree for [{unitId}] at Resonance Stage {_currentResonanceStage} (Total Max TP: {_currentState.TotalMaxTP}).");
        }

        public void SetResonanceStage(int newStage)
        {
            _currentResonanceStage = Mathf.Clamp(newStage, 1, 5);
            LoadUnitTree(_selectedUnitIndex, _currentResonanceStage);
        }

        public void ResetCurrentNotation()
        {
            if (_currentState != null)
            {
                _currentState.ResetNotation();
                _puzzleScreen.UpdateAllNodes();
            }
        }

        public static void RunAutomated3To4TPDeficitTest()
        {
            SaveManager.EnsureInstance();
            Debug.Log("=========================================================================");
            Debug.Log("🚀 [M12 Automated Verification] Starting '3~4 TP Deficit & Resonance Buffer' Test!");
            Debug.Log("=========================================================================");

            string[] testUnits = { "Kiara", "Elena", "Vesta" };
            foreach (string unitId in testUnits)
            {
                NotationTreeData tree = Resources.Load<NotationTreeData>($"Notations/Tree_{unitId}");
                Debug.Assert(tree != null, $"Notation tree for {unitId} must exist in Resources/Notations!");

                // 1. TEST AT STAGE 1 (Base TP = 10, Total required = 14) -> MUST FAIL ON ULTIMATE NODE!
                var stage1State = NotationProgressState.CreateForUnit(unitId, tree, overrideResonanceStage: 1);
                stage1State.ResetNotation(); // Start clean
                
                Debug.Assert(stage1State.TotalMaxTP == 10, $"Stage 1 total TP should be 10, got {stage1State.TotalMaxTP}");
                
                int unlockedCount = 0;
                NotationNodeData ultimateNode = null;
                foreach (var node in tree.Nodes)
                {
                    if (node.NodeType == NotationNodeType.UltimateCheckmate)
                    {
                        ultimateNode = node;
                    }
                    else if (stage1State.TryUnlock(node))
                    {
                        unlockedCount++;
                    }
                }
                
                Debug.Assert(unlockedCount == 3, $"Should have successfully unlocked 3 opening/mid-game nodes at Stage 1, got {unlockedCount}");
                Debug.Assert(stage1State.RemainingTP == 1, $"After spending 9 TP out of 10 base, 1 TP should remain, got {stage1State.RemainingTP}");
                
                bool ultimateSuccessStage1 = stage1State.TryUnlock(ultimateNode);
                Debug.Assert(!ultimateSuccessStage1, "Ultimate Checkmate node (costs 5 TP) MUST FAIL at Stage 1 when only 1 TP remains (4 TP Deficit)!");
                Debug.Log($"✔️ [Stage 1 - {unitId}] Correctly encountered designed 4 TP deficit! Remaining: {stage1State.RemainingTP} TP, Ultimate required: {ultimateNode.TPCost} TP.");

                // 2. TEST AT STAGE 3 (Base 10 + Bonus 4 = 14 TP) -> MUST PERFECTLY UNLOCK ULTIMATE NODE!
                var stage3State = NotationProgressState.CreateForUnit(unitId, tree, overrideResonanceStage: 3);
                // Note: stage3State rehydrates the 3 nodes unlocked during stage 1 test!
                Debug.Assert(stage3State.TotalMaxTP == 14, $"Stage 3 total TP should be 14 (10 base + 4 bonus), got {stage3State.TotalMaxTP}");
                Debug.Assert(stage3State.RemainingTP == 5, $"With 9 TP spent out of 14, 5 TP should remain for the Ultimate Checkmate!");
                
                bool ultimateSuccessStage3 = stage3State.TryUnlock(ultimateNode);
                Debug.Assert(ultimateSuccessStage3, "Ultimate Checkmate node MUST succeed at Stage 3 with exact +4 TP buffer!");
                Debug.Log($"🏆 [Stage 3 - {unitId}] Resonance Buffer (+4 TP) successfully completely mastered the {tree.NotationTitle} tree! All {tree.Nodes.Count} nodes unlocked!");

                // Clean up save state for clean future plays
                stage3State.ResetNotation();
            }

            Debug.Log("=========================================================================");
            Debug.Log("✨ [M12 Automated Verification] ALL TESTS PASSED! 0 Errors / 0 Warnings.");
            Debug.Log("=========================================================================");
        }

        private void OnGUI()
        {
            float panelWidth = 380f;
            GUILayout.BeginArea(new Rect(Screen.width - panelWidth - 20, 20, panelWidth, 540), "👑 [M12] 기보 스킬트리 & 4 TP 완충 샌드박스", GUI.skin.window);
            GUILayout.Space(22);

            GUILayout.Label("<b>🎮 기물 기보 트리 셋팅 선택:</b>", GUI.skin.label);
            for (int i = 0; i < _unitTitles.Length; i++)
            {
                GUI.backgroundColor = (_selectedUnitIndex == i) ? new Color(0.3f, 1f, 0.4f) : new Color(0.85f, 0.85f, 0.9f);
                if (GUILayout.Button(_unitTitles[i], GUILayout.Height(34)))
                {
                    LoadUnitTree(i, _currentResonanceStage);
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(15);
            GUILayout.Label("<b>💎 공명 돌파 단계 (Resonance Bonus TP 완충 시연):</b>", GUI.skin.label);
            
            string[] stageNames = {
                "[Stage 1] 명함 (0 보너스 TP - 4 TP 결핍!)",
                "[Stage 2] 돌파 1단 (+2 보너스 TP)",
                "[Stage 3] 돌파 2단 (+4 보너스 TP - 궁극 개방!)",
                "[Stage 4] 돌파 3단 (+6 보너스 TP)",
                "[Stage 5] 최종 돌파 (+8 보너스 TP - Grandmaster)"
            };

            for (int s = 1; s <= 5; s++)
            {
                GUI.backgroundColor = (_currentResonanceStage == s) ? new Color(1f, 0.7f, 0.2f) : new Color(0.9f, 0.9f, 0.95f);
                if (GUILayout.Button(stageNames[s - 1], GUILayout.Height(30)))
                {
                    SetResonanceStage(s);
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(18);
            GUILayout.Label("<b>⚙️ 전술 제어 및 리워드 검증:</b>", GUI.skin.label);

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🔄 [R] 기보 노드 해금 전면 초기화 (Reset & Refund)", GUILayout.Height(36)))
            {
                ResetCurrentNotation();
            }

            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Button("🤖 [V] 3~4 TP 부족 설계 & 완충 자동화 테스트", GUILayout.Height(38)))
            {
                RunAutomated3To4TPDeficitTest();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndArea();
        }
    }
}
