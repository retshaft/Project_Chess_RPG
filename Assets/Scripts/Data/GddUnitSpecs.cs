using UnityEngine;

namespace CheckmateRPG.Data
{
    /// <summary>
    /// GDD Part 3 §3.1~3.6 기물별 행동 AP 소모 기본값의 단일 기준 테이블.
    /// 에디터 툴(UpdateCosts)과 회귀 테스트가 이 테이블만 참조해 수치를 일치시킨다.
    /// 하위 변형(정찰병 이동 6, 기동기사 이동 18 등)은 개별 에셋에서 기획 확정 시 예외로 둔다.
    /// </summary>
    public static class GddUnitSpecs
    {
        /// <summary>기물의 행동(이동/공격) 1회 기본 AP 소모량. GDD Part 3 "AP 소모 기본".</summary>
        public static float GetActionAPCost(ChessPieceType pieceType)
        {
            return pieceType switch
            {
                ChessPieceType.Pawn => 8f,
                ChessPieceType.Knight => 24f,
                ChessPieceType.Bishop => 24f,
                ChessPieceType.Rook => 36f,
                ChessPieceType.Queen => 48f,
                ChessPieceType.King => 24f,
                _ => 8f
            };
        }
    }
}
