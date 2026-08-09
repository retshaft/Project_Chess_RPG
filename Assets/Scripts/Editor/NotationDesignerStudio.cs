#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CheckmateRPG.Progression;

namespace CheckmateRPG.Editor
{
    public class NotationDesignerStudio : EditorWindow
    {
        [MenuItem("CheckmateRPG/Notation Designer Studio (M12 기보 스킬트리 셋업)", false, 20)]
        [MenuItem("Tools/Notation Designer Studio (M12 기보 스킬트리 셋업)", false, 20)]
        public static void ShowWindow()
        {
            GetWindow<NotationDesignerStudio>("Notation Studio");
        }

        private void OnGUI()
        {
            GUILayout.Label("🏆 [M12] 기물 기보(Chess Notation) 디자이너 스튜디오", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("마스터 기획서 명세: '의도된 3~4 TP 부족 설계'와 '공명(Resonance) 돌파를 통한 보조 TP 완충' 구조가 반영된 3대 대표 캐릭터 기보 트리를 에셋베이스에 베이킹합니다.\n\n" +
                "• 총 요구 TP: 14 TP\n" +
                "• 명함(Stage 1) 기본 TP: 10 TP (4 TP 결핍으로 궁극 노드 도달 불가능!)\n" +
                "• 공명 3단(Stage 3) 달성 시: +4 TP 보너스 -> 총 14 TP로 궁극 체크메이트 노드 해금 성공!", MessageType.Info);
            
            GUILayout.Space(15);
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button("⚡ 3대 대표 기물 기보 트리 1-Click 베이킹 (Bake All Canonical Trees)", GUILayout.Height(45)))
            {
                BakeDefaultNotationTrees();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(20);
            GUILayout.Label("💡 기보 트리 베이킹 대상:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. [Kiara] Queen's Gambit Tree (e4 -> Nf3 -> Bc4 -> Qxf7#)");
            EditorGUILayout.LabelField("2. [Elena] Sicilian Defense Tree (c5 -> Nc6 -> g6 -> Bg7#)");
            EditorGUILayout.LabelField("3. [Vesta] Caro-Kann Defense Tree (c6 -> d5 -> Bf5 -> Nd7#)");
        }

        public static void BakeDefaultNotationTrees()
        {
            EnsureFolderExists("Assets", "Resources");
            EnsureFolderExists("Assets/Resources", "Notations");

            // 1. Kiara Tree
            BakeTree("Kiara", "Queen's Gambit", new string[] {
                "Kiara_e4|e4|2|BasicStat|오프닝 전술: 공격력 +10%|Attack|10",
                "Kiara_Nf3|Nf3|3|TacticalPassive|나이트 전개: 초기 SP +15 충전|MaxHealth|15",
                "Kiara_Bc4|Bc4|4|BasicStat|비숍 침투: 장갑 관통 +15% 증폭|Attack|15",
                "Kiara_Qxf7|Qxf7#|5|UltimateCheckmate|궁극 퀸스 갬빗 체크메이트: 스킬 피해량 및 돌파력 최종 극대화|Attack|25"
            });

            // 2. Elena Tree
            BakeTree("Elena", "Sicilian Defense", new string[] {
                "Elena_c5|c5|2|BasicStat|시실리언 오프닝: 치명타 확률 +10%|Attack|8",
                "Elena_Nc6|Nc6|3|TacticalPassive|측면 돌파: 이동 및 공속 전술 상승|MaxHealth|10",
                "Elena_g6|g6|4|BasicStat|피드포워드: 치명타 피해 +20%|Attack|18",
                "Elena_Bg7|Bg7#|5|UltimateCheckmate|궁극 드래곤 바리에이션: 광전사 맹폭 폭딜 극대화|Attack|30"
            });

            // 3. Vesta Tree
            BakeTree("Vesta", "Caro-Kann Defense", new string[] {
                "Vesta_c6|c6|2|BasicStat|카로-칸 방진: 방어력 +15%|Defense|15",
                "Vesta_d5|d5|3|TacticalPassive|중앙 수호: CC 상태이상 저항 +20%|Defense|20",
                "Vesta_Bf5|Bf5|4|BasicStat|전진 방벽: 최대 HP +25%|MaxHealth|25",
                "Vesta_Nd7|Nd7#|5|UltimateCheckmate|궁극 강철 참호: 피격 시 파이어월 아군 무효화 실드 결속|Defense|40"
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("✅ [NotationDesignerStudio] Successfully baked Kiara, Elena, and Vesta Notation Trees in Resources/Notations!");
        }

        private static void BakeTree(string unitId, string title, string[] nodeSpecs)
        {
            List<NotationNodeData> createdNodes = new List<NotationNodeData>();
            NotationNodeData prevNode = null;

            foreach (string spec in nodeSpecs)
            {
                string[] parts = spec.Split('|');
                string assetName = parts[0];
                string coord = parts[1];
                int cost = int.Parse(parts[2]);
                NotationNodeType nodeType = (NotationNodeType)System.Enum.Parse(typeof(NotationNodeType), parts[3]);
                string desc = parts[4];
                string statTypeStr = parts[5];
                float statVal = float.Parse(parts[6]);

                string nodePath = $"Assets/Resources/Notations/{assetName}.asset";
                var node = AssetDatabase.LoadAssetAtPath<NotationNodeData>(nodePath);
                if (node == null)
                {
                    node = ScriptableObject.CreateInstance<NotationNodeData>();
                    AssetDatabase.CreateAsset(node, nodePath);
                }

                node.NodeId = assetName;
                node.NotationCoord = coord;
                node.TPCost = cost;
                node.NodeType = nodeType;
                node.Description = desc;

                node.Prerequisites.Clear();
                if (prevNode != null)
                {
                    node.Prerequisites.Add(prevNode);
                }

                node.Bonuses.Clear();
                string statKey = statTypeStr == "Attack" ? "AttackDamage" : statTypeStr;
                if (System.Enum.TryParse<MetaStatType>(statKey, out MetaStatType statType))
                {
                    node.Bonuses.Add(new StatModifierEntry { Stat = statType, FlatBonus = statVal, PercentBonus = 0f });
                }

                EditorUtility.SetDirty(node);
                createdNodes.Add(node);
                prevNode = node;
            }

            string treePath = $"Assets/Resources/Notations/Tree_{unitId}.asset";
            var tree = AssetDatabase.LoadAssetAtPath<NotationTreeData>(treePath);
            if (tree == null)
            {
                tree = ScriptableObject.CreateInstance<NotationTreeData>();
                AssetDatabase.CreateAsset(tree, treePath);
            }

            tree.TargetUnitId = unitId;
            tree.NotationTitle = title;
            tree.BaseNotationTP = 10; // Designed to be 4 TP short of 14!
            tree.RootNode = createdNodes[0];
            tree.Nodes = new List<NotationNodeData>(createdNodes);

            EditorUtility.SetDirty(tree);
            Debug.Log($"   🔸 Baked Tree for [{unitId}]: '{title}' with {createdNodes.Count} Notation Nodes (Total Cost: 14 TP, Base TP: {tree.BaseNotationTP})");
        }

        private static void EnsureFolderExists(string parent, string newFolderName)
        {
            string fullPath = $"{parent}/{newFolderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, newFolderName);
            }
        }
    }
}
#endif
