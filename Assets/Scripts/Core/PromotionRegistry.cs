using System;
using UnityEngine;
using CheckmateRPG.Data;

namespace CheckmateRPG.Core
{
    /// <summary>
    /// GDD Part 3에 정의된 서브클래스별 프로모션 타겟(ChessPieceType) 매핑 정보를 제공합니다.
    /// </summary>
    public static class PromotionRegistry
    {
        /// <summary>
        /// 주어진 서브클래스(직군)가 프로모션할 때 변환될 체스 기물 타입을 반환합니다.
        /// </summary>
        public static ChessPieceType GetPromotionTarget(UnitSubclassType subclass)
        {
            return subclass switch
            {
                UnitSubclassType.Vanguard => ChessPieceType.Rook,
                UnitSubclassType.Charger => ChessPieceType.Knight,
                UnitSubclassType.Defender => ChessPieceType.Rook,
                UnitSubclassType.Flagbearer => ChessPieceType.Bishop,
                UnitSubclassType.Agent => ChessPieceType.Queen,
                UnitSubclassType.Engineer => ChessPieceType.Rook,
                UnitSubclassType.Scout => ChessPieceType.Knight,
                UnitSubclassType.Elite => ChessPieceType.King,
                UnitSubclassType.Jester => ChessPieceType.Bishop, // 기본적으로 비숍급 이상의 매우 높은 성능으로 정의됨
                UnitSubclassType.Ambusher => ChessPieceType.Knight,
                UnitSubclassType.Marksman => ChessPieceType.Bishop, // 저격수 역할
                UnitSubclassType.Ronin => ChessPieceType.King,
                UnitSubclassType.Spy => ChessPieceType.Queen, // 킹에게 막대한 피해, 면역 등 특수 기믹이 있으나 퀸 베이스
                UnitSubclassType.Commander => ChessPieceType.King,
                UnitSubclassType.Herald => ChessPieceType.Pawn, // 역방향 폰
                UnitSubclassType.Conscript => ChessPieceType.Queen, // 기본값(징집병)은 퀸
                _ => ChessPieceType.Queen
            };
        }
    }
}
