using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Runtime.Mutations;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class EffectMutationProcessor
    {
        private readonly Simulation.SimulationRuntime _simulationRuntime;
        private readonly Func<EffectRuntimeState, bool> _applyEffect;
        private readonly Func<string, bool> _isPhysicalCcEffect;
        private readonly Func<ApplyEffectMutation, ApplyEffectMutation> _substituteWithStagger;

        public EffectMutationProcessor(
            Simulation.SimulationRuntime simulationRuntime,
            Func<EffectRuntimeState, bool> applyEffect,
            Func<string, bool> isPhysicalCcEffect,
            Func<ApplyEffectMutation, ApplyEffectMutation> substituteWithStagger)
        {
            _simulationRuntime = simulationRuntime ?? throw new ArgumentNullException(nameof(simulationRuntime));
            _applyEffect = applyEffect ?? throw new ArgumentNullException(nameof(applyEffect));
            _isPhysicalCcEffect = isPhysicalCcEffect ?? throw new ArgumentNullException(nameof(isPhysicalCcEffect));
            _substituteWithStagger = substituteWithStagger ?? throw new ArgumentNullException(nameof(substituteWithStagger));
        }

        public IReadOnlyList<IGameEvent> Apply(ApplyEffectMutation mutation)
        {
            if (string.IsNullOrWhiteSpace(mutation.EffectId) || mutation.TargetId == Guid.Empty)
                return Array.Empty<IGameEvent>();

            ApplyEffectMutation resolvedMutation = ResolveMutation(mutation);
            var state = new EffectRuntimeState(
                resolvedMutation.EffectId,
                resolvedMutation.SourceId,
                resolvedMutation.TargetId,
                Mathf.Max(1, resolvedMutation.DurationTicks),
                Mathf.Max(1, resolvedMutation.StackCount),
                Mathf.Max(1, resolvedMutation.TickInterval),
                Mathf.Clamp(resolvedMutation.InitialTickIn, 1, Mathf.Max(1, resolvedMutation.TickInterval)),
                Mathf.Max(0f, resolvedMutation.Magnitude),
                timingPhase: resolvedMutation.TimingPhase,
                actionSpeedLevel: resolvedMutation.ActionSpeedLevel,
                isReaction: resolvedMutation.IsReaction,
                stackPolicy: resolvedMutation.StackPolicy,
                maxStackCap: resolvedMutation.MaxStackCap,
                isHidden: resolvedMutation.IsHidden,
                statOverrides: resolvedMutation.StatOverrides);

            _ = _applyEffect(state);
            return Array.Empty<IGameEvent>();
        }

        private ApplyEffectMutation ResolveMutation(ApplyEffectMutation mutation)
        {
            if (!_isPhysicalCcEffect(mutation.EffectId))
                return mutation;
            if (!_simulationRuntime.TryGetUnit(mutation.TargetId, out IReadOnlyUnitRuntimeState targetState))
                return mutation;
            if ((targetState.StatusFlags & UnitStatusFlags.Stagger) != 0)
                return mutation;

            ApplyEffectMutation substituted = _substituteWithStagger(mutation);
            return string.IsNullOrWhiteSpace(substituted.EffectId)
                ? mutation
                : substituted;
        }
    }
}
