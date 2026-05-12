using CheckmateRPG.Units;

namespace CheckmateRPG.Core.Actions
{
    public readonly record struct ActionCostBreakdown(
        float APCost,
        int SPCost,
        int CooldownCost,
        APActionReason APReason);

    public readonly record struct ActionCostContext(
        UnitBrain Actor,
        AbilityDefinition AbilityDefinition,
        AbilityRuntimeState AbilityRuntimeState);

    public interface IActionCostPolicy
    {
        ActionCostBreakdown Evaluate(IActionCommand action, ActionCostContext context);
    }
}
