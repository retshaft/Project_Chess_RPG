using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core
{
    public static class SnapshotDiffUtility
    {
        public const float DefaultFloatTolerance = 0.0001f;

        public static SnapshotDiffResult Compare(
            FrameSnapshot expected,
            FrameSnapshot actual,
            float floatTolerance = DefaultFloatTolerance)
        {
            if (floatTolerance < 0f)
                throw new ArgumentOutOfRangeException(nameof(floatTolerance));

            var differences = new List<string>();
            int? expectedTick = expected?.Tick;
            int? actualTick = actual?.Tick;

            if (expected == null || actual == null)
            {
                if (expected == null && actual == null)
                    return new SnapshotDiffResult("Snapshot", expectedTick, actualTick, Array.Empty<string>());

                differences.Add(expected == null
                    ? "Expected snapshot is missing."
                    : "Actual snapshot is missing.");

                return new SnapshotDiffResult("Snapshot", expectedTick, actualTick, differences);
            }

            if (expected.Tick != actual.Tick)
                differences.Add($"Tick mismatch: expected={expected.Tick}, actual={actual.Tick}.");

            CompareUnits(expected.Units, actual.Units, differences);
            CompareCurrentActions(expected.ActiveActions, actual.ActiveActions, differences);
            CompareActiveEffects(expected.ActiveEffects, actual.ActiveEffects, floatTolerance, differences);

            return new SnapshotDiffResult("Snapshot", expected.Tick, actual.Tick, differences);
        }

        private static void CompareUnits(
            List<UnitFrameSnapshot> expectedUnits,
            List<UnitFrameSnapshot> actualUnits,
            List<string> differences)
        {
            var expectedById = new SortedDictionary<string, UnitFrameSnapshot>(StringComparer.Ordinal);
            var actualById = new SortedDictionary<string, UnitFrameSnapshot>(StringComparer.Ordinal);

            AddUnits(expectedUnits, expectedById);
            AddUnits(actualUnits, actualById);

            var unitIds = new SortedSet<string>(expectedById.Keys, StringComparer.Ordinal);
            unitIds.UnionWith(actualById.Keys);

            foreach (string unitId in unitIds)
            {
                bool hasExpected = expectedById.TryGetValue(unitId, out UnitFrameSnapshot expectedUnit);
                bool hasActual = actualById.TryGetValue(unitId, out UnitFrameSnapshot actualUnit);

                if (!hasExpected || !hasActual)
                {
                    differences.Add(!hasExpected
                        ? $"Unit[{unitId}] missing from expected snapshot."
                        : $"Unit[{unitId}] missing from actual snapshot.");
                    continue;
                }

                if (expectedUnit.HP != actualUnit.HP)
                    differences.Add($"Unit[{unitId}].HP mismatch: expected={expectedUnit.HP}, actual={actualUnit.HP}.");

                if (expectedUnit.PosX != actualUnit.PosX || expectedUnit.PosY != actualUnit.PosY)
                {
                    differences.Add(
                        $"Unit[{unitId}].Position mismatch: expected=({expectedUnit.PosX},{expectedUnit.PosY}), " +
                        $"actual=({actualUnit.PosX},{actualUnit.PosY}).");
                }

                string expectedActionId = Normalize(expectedUnit.CurrentActionId);
                string actualActionId = Normalize(actualUnit.CurrentActionId);
                if (!string.Equals(expectedActionId, actualActionId, StringComparison.Ordinal))
                {
                    differences.Add(
                        $"Unit[{unitId}].CurrentAction mismatch: expected={FormatValue(expectedActionId)}, " +
                        $"actual={FormatValue(actualActionId)}.");
                }
            }
        }

        private static void CompareCurrentActions(
            List<ActionFrameSnapshot> expectedActions,
            List<ActionFrameSnapshot> actualActions,
            List<string> differences)
        {
            var expectedById = new SortedDictionary<string, ActionFrameSnapshot>(StringComparer.Ordinal);
            var actualById = new SortedDictionary<string, ActionFrameSnapshot>(StringComparer.Ordinal);

            AddActions(expectedActions, expectedById);
            AddActions(actualActions, actualById);

            var actionIds = new SortedSet<string>(expectedById.Keys, StringComparer.Ordinal);
            actionIds.UnionWith(actualById.Keys);

            foreach (string actionId in actionIds)
            {
                bool hasExpected = expectedById.TryGetValue(actionId, out ActionFrameSnapshot expectedAction);
                bool hasActual = actualById.TryGetValue(actionId, out ActionFrameSnapshot actualAction);

                if (!hasExpected || !hasActual)
                {
                    differences.Add(!hasExpected
                        ? $"CurrentAction[{actionId}] missing from expected snapshot."
                        : $"CurrentAction[{actionId}] missing from actual snapshot.");
                    continue;
                }

                CompareActionField(actionId, "ActorId", expectedAction.ActorId, actualAction.ActorId, differences);
                CompareActionField(actionId, "ActionType", expectedAction.ActionType, actualAction.ActionType, differences);
                CompareActionField(actionId, "State", expectedAction.State, actualAction.State, differences);
                CompareActionField(actionId, "QueuedTick", expectedAction.QueuedTick, actualAction.QueuedTick, differences);
                CompareActionField(actionId, "StartTick", expectedAction.StartTick, actualAction.StartTick, differences);
                CompareActionField(actionId, "ResolveTick", expectedAction.ResolveTick, actualAction.ResolveTick, differences);
                CompareActionField(actionId, "RecoveryEndTick", expectedAction.RecoveryEndTick, actualAction.RecoveryEndTick, differences);
                CompareActionField(actionId, "Data", Normalize(expectedAction.Data), Normalize(actualAction.Data), differences);
            }
        }

        private static void CompareActiveEffects(
            List<EffectFrameSnapshot> expectedEffects,
            List<EffectFrameSnapshot> actualEffects,
            float floatTolerance,
            List<string> differences)
        {
            var expectedByKey = new SortedDictionary<string, EffectFrameSnapshot>(StringComparer.Ordinal);
            var actualByKey = new SortedDictionary<string, EffectFrameSnapshot>(StringComparer.Ordinal);

            AddEffects(expectedEffects, expectedByKey);
            AddEffects(actualEffects, actualByKey);

            var effectKeys = new SortedSet<string>(expectedByKey.Keys, StringComparer.Ordinal);
            effectKeys.UnionWith(actualByKey.Keys);

            foreach (string effectKey in effectKeys)
            {
                bool hasExpected = expectedByKey.TryGetValue(effectKey, out EffectFrameSnapshot expectedEffect);
                bool hasActual = actualByKey.TryGetValue(effectKey, out EffectFrameSnapshot actualEffect);

                if (!hasExpected || !hasActual)
                {
                    differences.Add(!hasExpected
                        ? $"ActiveEffect[{effectKey}] missing from expected snapshot."
                        : $"ActiveEffect[{effectKey}] missing from actual snapshot.");
                    continue;
                }

                CompareEffectField(effectKey, "RemainingTick", expectedEffect.RemainingTick, actualEffect.RemainingTick, differences);
                CompareEffectField(effectKey, "StackCount", expectedEffect.StackCount, actualEffect.StackCount, differences);
                CompareEffectField(effectKey, "TickInterval", expectedEffect.TickInterval, actualEffect.TickInterval, differences);
                CompareEffectField(effectKey, "NextTickIn", expectedEffect.NextTickIn, actualEffect.NextTickIn, differences);

                if (Math.Abs(expectedEffect.Magnitude - actualEffect.Magnitude) > floatTolerance)
                {
                    differences.Add(
                        $"ActiveEffect[{effectKey}].Magnitude mismatch: expected={expectedEffect.Magnitude}, " +
                        $"actual={actualEffect.Magnitude}, tolerance={floatTolerance}.");
                }
            }
        }

        private static void AddUnits(
            List<UnitFrameSnapshot> units,
            IDictionary<string, UnitFrameSnapshot> destination)
        {
            if (units == null)
                return;

            for (int i = 0; i < units.Count; i++)
            {
                UnitFrameSnapshot unit = units[i];
                if (unit == null)
                    continue;

                destination[Normalize(unit.UnitId)] = unit;
            }
        }

        private static void AddActions(
            List<ActionFrameSnapshot> actions,
            IDictionary<string, ActionFrameSnapshot> destination)
        {
            if (actions == null)
                return;

            for (int i = 0; i < actions.Count; i++)
            {
                ActionFrameSnapshot action = actions[i];
                if (action == null)
                    continue;

                destination[Normalize(action.ActionId)] = action;
            }
        }

        private static void AddEffects(
            List<EffectFrameSnapshot> effects,
            IDictionary<string, EffectFrameSnapshot> destination)
        {
            if (effects == null)
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                EffectFrameSnapshot effect = effects[i];
                if (effect == null)
                    continue;

                destination[BuildEffectKey(effect)] = effect;
            }
        }

        private static string BuildEffectKey(EffectFrameSnapshot effect)
        {
            return $"{Normalize(effect.TargetId)}:{Normalize(effect.EffectId)}:{Normalize(effect.SourceId)}";
        }

        private static void CompareActionField<T>(
            string actionId,
            string fieldName,
            T expected,
            T actual,
            List<string> differences)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
                return;

            differences.Add(
                $"CurrentAction[{actionId}].{fieldName} mismatch: expected={FormatValue(expected)}, actual={FormatValue(actual)}.");
        }

        private static void CompareEffectField<T>(
            string effectKey,
            string fieldName,
            T expected,
            T actual,
            List<string> differences)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
                return;

            differences.Add(
                $"ActiveEffect[{effectKey}].{fieldName} mismatch: expected={FormatValue(expected)}, actual={FormatValue(actual)}.");
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        private static string FormatValue<T>(T value)
        {
            return value == null ? "null" : value.ToString();
        }
    }
}
