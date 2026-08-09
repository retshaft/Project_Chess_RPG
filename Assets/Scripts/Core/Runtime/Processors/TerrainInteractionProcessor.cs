using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Events;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;
using CheckmateRPG.Components;
using UnityEngine;

namespace CheckmateRPG.Core.Runtime.Processors
{
    public sealed class TerrainInteractionProcessor : IDisposable
    {
        private const float SpikeDamagePercent = 0.03f;
        private const float SanctuaryHealPercent = 0.02f;

        private readonly IEventBus _eventBus;
        private readonly MutationCommitService _mutationCommitService;
        private readonly Func<SimulationRuntime> _simulationRuntimeProvider;
        private readonly Func<Guid, UnitBrain> _unitLookup;

        public TerrainInteractionProcessor(
            IEventBus eventBus,
            MutationCommitService mutationCommitService,
            Func<SimulationRuntime> simulationRuntimeProvider,
            Func<Guid, UnitBrain> unitLookup)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _mutationCommitService = mutationCommitService ?? throw new ArgumentNullException(nameof(mutationCommitService));
            _simulationRuntimeProvider = simulationRuntimeProvider ?? throw new ArgumentNullException(nameof(simulationRuntimeProvider));
            _unitLookup = unitLookup ?? throw new ArgumentNullException(nameof(unitLookup));
        }

        public void Attach()
        {
            _eventBus.Subscribe<MoveCompletedEvent>(HandleMoveCompleted);
        }

        public void Dispose()
        {
            _eventBus.Unsubscribe<MoveCompletedEvent>(HandleMoveCompleted);
        }

        private void HandleMoveCompleted(MoveCompletedEvent evt)
        {
            if (evt == null || evt.Payload == null)
                return;

            Vector2Int destination = evt.Payload.To;
            GridSystem grid = GridSystem.Instance;
            if (grid == null || !grid.IsValidCell(destination))
                return;

            TileType tileType = grid.GetTileType(destination);
            if (tileType == TileType.Normal || tileType == TileType.Swamp)
                return;

            var queue = new MutationQueue();
            SimulationRuntime runtime = _simulationRuntimeProvider();
            int currentTick = runtime != null ? runtime.CurrentTick : 0;

            if (tileType == TileType.Spikes)
            {
                TryQueueSpikeDamage(queue, evt.Payload.UnitId, currentTick);
            }
            else if (tileType == TileType.Sanctuary)
            {
                TryQueueSanctuaryHeal(queue, evt.Payload.UnitId, currentTick);
            }

            if (queue.Count > 0)
                _mutationCommitService.Commit(queue);
        }

        public void AdvanceTick(int currentTick)
        {
            GridSystem grid = GridSystem.Instance;
            if (grid == null)
                return;

            SimulationRuntime runtime = _simulationRuntimeProvider();
            if (runtime == null)
                return;

            var queue = new MutationQueue();

            // 1초 단위로 틱 발생 (ActionScheduler의 틱 길이 가정, 보통 10틱 = 1초)
            // 여기서는 단순화를 위해 특정 주기를 상정할 수 있으나, 일단 매번 TickAdvancedEvent에서 처리 (또는 TickInterval에 맞춰 처리 가능)
            // 임시로 매 10틱마다 환경 효과 발생이라 가정
            if (currentTick % 10 != 0)
                return;

            foreach (Guid unitId in runtime.EnumerateUnitIds())
            {
                UnitBrain unit = _unitLookup(unitId);
                if (unit == null || unit.IsDead || unit.Movement == null)
                    continue;

                Vector2Int pos = unit.Movement.GridPosition;
                TileType tileType = grid.GetTileType(pos);

                if (tileType == TileType.Spikes)
                {
                    TryQueueSpikeDamage(queue, unitId, currentTick);
                }
                else if (tileType == TileType.Sanctuary)
                {
                    TryQueueSanctuaryHeal(queue, unitId, currentTick);
                }
            }

            if (queue.Count > 0)
                _mutationCommitService.Commit(queue);
        }

        private void TryQueueSpikeDamage(MutationQueue queue, Guid targetId, int tick)
        {
            UnitBrain unit = _unitLookup(targetId);
            if (unit == null || unit.Health == null)
                return;

            int amount = Mathf.FloorToInt(unit.Health.MaxHealth * SpikeDamagePercent);
            if (amount <= 0)
                return;

            var context = new MutationContext(tick, Guid.Empty, targetId, "SpikeDamage");
            queue.Enqueue(new DamageMutation(
                SeededRandomProvider.Shared.NextGuid(),
                targetId,
                Guid.Empty,
                amount,
                IsCritical: false,
                Context: context,
                DamageType: DamageType.True,
                IsTrueDamage: true
            ), ActionSpeedTier.Normal, int.MaxValue);
        }

        private void TryQueueSanctuaryHeal(MutationQueue queue, Guid targetId, int tick)
        {
            UnitBrain unit = _unitLookup(targetId);
            if (unit == null || unit.Health == null)
                return;

            // Sanctuary 효과는 아군(플레이어)에게만 적용 (가정)
            if (unit.TryGetComponent(out TeamComponent team) && !team.IsPlayer)
                return;

            int amount = Mathf.FloorToInt(unit.Health.MaxHealth * SanctuaryHealPercent);
            if (amount <= 0)
                return;

            var context = new MutationContext(tick, Guid.Empty, targetId, "SanctuaryHeal");
            queue.Enqueue(new HealMutation(
                SeededRandomProvider.Shared.NextGuid(),
                targetId,
                Guid.Empty,
                amount,
                Context: context
            ), ActionSpeedTier.Normal, int.MaxValue);

            // TODO: Sanctuary 방어력 버프의 경우, EffectMutation을 통해 처리 가능.
        }
    }
}
