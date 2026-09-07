#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CheckmateRPG.Data;

namespace CheckmateRPG.Testing.Editor
{
    /// <summary>
    /// UnitData 에셋의 수치를 GDD 단일 기준 테이블(GddUnitSpecs)과 동기화하는 에디터 툴.
    /// 도메인 리로드마다 실행되어 기물별 행동 AP 소모(이동/공격)와 배치 코스트를 규격으로 되돌린다.
    /// </summary>
    public static class UpdateCosts
    {
        [InitializeOnLoadMethod]
        public static void UpdateUnitCosts()
        {
            // 플레이어 측 표준 기물 에셋 (GDD Part 3 배치 코스트: 폰1/나이트3/비숍3/룩4/퀸5, 킹은 기존 관례 유지)
            UpdateCost("PawnUnitData.asset", 1, ChessPieceType.Pawn);
            UpdateCost("KnightUnitData.asset", 3, ChessPieceType.Knight);
            UpdateCost("BishopUnitData.asset", 3, ChessPieceType.Bishop);
            UpdateCost("RookUnitData.asset", 4, ChessPieceType.Rook);
            UpdateCost("QueenUnitData.asset", 5, ChessPieceType.Queen);
            UpdateCost("KingUnitData 1.asset", 3, ChessPieceType.King);

            // 적 측 에셋 — 스테이지가 참조하는 정식 경로와 Resources 사본 모두 규격화
            UpdateCost("Assets/Data/Units/Enemies/Enemy_BlackPawn.asset", 1, ChessPieceType.Pawn);
            UpdateCost("Assets/Data/Units/Enemies/Enemy_BlackKnight.asset", 3, ChessPieceType.Knight);
            UpdateCost("Assets/Resources/Units/Enemies/Enemy_BlackPawn.asset", 1, ChessPieceType.Pawn);
            UpdateCost("Assets/Resources/Units/Enemies/Enemy_BlackKnight.asset", 3, ChessPieceType.Knight);
            UpdateCost("Assets/Resources/Units/Enemies/Enemy_BlackBishop.asset", 3, ChessPieceType.Bishop);
            UpdateCost("Assets/Resources/Units/Enemies/Enemy_BlackRook.asset", 4, ChessPieceType.Rook);
            UpdateCost("Assets/Resources/Units/Enemies/Enemy_BlackQueen.asset", 5, ChessPieceType.Queen);
            UpdateCost("Assets/Resources/Units/Enemies/Enemy_BlackKing.asset", 3, ChessPieceType.King);

            AssetDatabase.SaveAssets();
        }

        private static void UpdateCost(string path, int cost, ChessPieceType pieceType)
        {
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (data == null)
                return;

            bool dirty = false;

            if (data.DeploymentCost != cost)
            {
                data.DeploymentCost = cost;
                dirty = true;
            }

            // 기물 오분류 수정(예: 나이트 에셋이 폰으로 돼 있는 경우) — 파생 수치도 함께 갱신된다.
            if (data.PieceType != pieceType)
            {
                data.PieceType = pieceType;
                dirty = true;
            }

            float actionCost = GddUnitSpecs.GetActionAPCost(pieceType);
            if (!Mathf.Approximately(data.MoveCostAP, actionCost))
            {
                data.MoveCostAP = actionCost;
                dirty = true;
            }

            if (!Mathf.Approximately(data.AttackCostAP, actionCost))
            {
                data.AttackCostAP = actionCost;
                dirty = true;
            }

            if (dirty)
            {
                data.SyncDefaultChessMetadata(); // MovePattern/MoveCooldown 등 기물 파생 수치 갱신
                EditorUtility.SetDirty(data);
                Debug.Log($"[UpdateCosts] Synced {path} -> {pieceType}, Deploy={cost}, AP={actionCost}");
            }
        }
    }
}
#endif
