using CheckmateRPG.Data;
using CheckmateRPG.Units;

namespace CheckmateRPG.Core
{
    public static class BattleDiagnostics
    {
        public static bool EnableDamageDebug { get; private set; }
        public static bool EnableMovementDebug { get; private set; }
        public static bool MovementDebugOnlyKnight { get; private set; } = true;
        public static bool IncludeUnitNameInLogs { get; private set; } = true;

        public static void Configure(
            bool enableDamageDebug,
            bool enableMovementDebug,
            bool movementDebugOnlyKnight,
            bool includeUnitNameInLogs)
        {
            EnableDamageDebug = enableDamageDebug;
            EnableMovementDebug = enableMovementDebug;
            MovementDebugOnlyKnight = movementDebugOnlyKnight;
            IncludeUnitNameInLogs = includeUnitNameInLogs;
        }

        public static bool ShouldLogMovement(UnitBrain unit)
        {
            if (!EnableMovementDebug || unit == null)
                return false;
            if (!MovementDebugOnlyKnight)
                return true;
            return unit.UnitData != null && unit.UnitData.PieceType == ChessPieceType.Knight;
        }
    }
}
