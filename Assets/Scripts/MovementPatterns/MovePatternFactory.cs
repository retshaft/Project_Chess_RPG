using CheckmateRPG.Data;

namespace CheckmateRPG.MovementPatterns
{
    public static class MovePatternFactory
    {
        public static IMovePattern Create(UnitData unitData)
        {
            if (unitData == null)
                return null;

            return unitData.MovePattern switch
            {
                MovePatternType.Pawn => new PawnMovePattern(),
                MovePatternType.Knight => new KnightMovePattern(),
                MovePatternType.King => new KingMovePattern(),
                MovePatternType.SlidingDiagonal => new SlidingMovePattern(allowOrthogonal: false, allowDiagonal: true),
                MovePatternType.SlidingOmni => new SlidingMovePattern(allowOrthogonal: true, allowDiagonal: true),
                MovePatternType.SlidingOrthogonal => new SlidingMovePattern(allowOrthogonal: true, allowDiagonal: false),
                _ => null
            };
        }
    }
}
