using System;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Actions;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    /// <summary>
    /// 회귀: 유닛 사망 시 활성 액션 정리(TerminateActionsForActors)가 상태 머신을 위반해
    /// "Resolving → Interrupted" InvalidOperationException을 던지며 틱 처리를 중단시켰던 문제(2026-09-29).
    ///  - Resolving 상태는 해약이 이미 적용된 뒤이므로 Completed로 마무리되어야 한다.
    ///  - Queued 상태 중단은 상태 머신과 CanInterrupt 정책 모두 허용해야 한다.
    /// </summary>
    [TestFixture]
    public sealed class ActionDeathTerminationRegressionTests
    {
        private ActionScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _scheduler = new ActionScheduler();
        }

        private MoveActionCommand ScheduleMove(Guid actorId, int startTick)
        {
            MoveActionCommand command = new MoveActionCommand(
                actorId,
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                startTick,
                ActionSpeedTier.Normal);
            ActionAdmissionResult result = _scheduler.ScheduleAction(command);
            Assert.AreEqual(ActionAdmissionStatus.Scheduled, result.Status, "테스트 액션은 정상 등록되어야 한다.");
            return command;
        }

        private void AdvanceUntilState(BaseActionCommand action, ActionState targetState, int maxTicks = 64)
        {
            for (int i = 0; i < maxTicks && action.State != targetState; i++)
                _scheduler.AdvanceTick();

            Assert.AreEqual(targetState, action.State, $"{targetState}에 도달하지 못했다.");
        }

        [Test]
        public void TerminateActor_ResolvingAction_CompletesWithoutThrow()
        {
            Guid actorId = Guid.NewGuid();
            MoveActionCommand action = ScheduleMove(actorId, _scheduler.CurrentTick + 1);
            AdvanceUntilState(action, ActionState.Resolving);

            Assert.DoesNotThrow(() => _scheduler.TerminateActionsForActors(new Guid[] { actorId }));
            Assert.IsTrue(action.IsCompleted, "해약이 적용된 액션은 완료로 정리되어야 한다.");
            Assert.AreEqual(ActionState.Completed, action.State);
        }

        [Test]
        public void TerminateActor_QueuedAction_IsInterruptedWithoutThrow()
        {
            Guid actorId = Guid.NewGuid();
            MoveActionCommand action = ScheduleMove(actorId, _scheduler.CurrentTick + 5);

            Assert.DoesNotThrow(() => _scheduler.TerminateActionsForActors(new Guid[] { actorId }));
            Assert.AreEqual(ActionState.Interrupted, action.State, "시작 전 액션은 중단으로 정리되어야 한다.");
        }

        [Test]
        public void StateMachine_AllowsQueuedToInterrupted()
        {
            Assert.IsTrue(ActionStateMachine.IsValidTransition(ActionState.Queued, ActionState.Interrupted));
            Assert.IsTrue(ActionStateMachine.IsValidTransition(ActionState.Casting, ActionState.Interrupted));
            Assert.IsTrue(ActionStateMachine.IsValidTransition(ActionState.Recovery, ActionState.Interrupted));
        }
    }
}
