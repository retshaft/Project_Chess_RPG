using System;
using System.Reflection;
using System.Text.RegularExpressions;
using CheckmateRPG.Core.Events.ActionEvents;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CheckmateRPG.Tests
{
    public sealed class RepresentativeComboRegressionTests
    {
        private Type _tool;
        private Guid _actionId;
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [SetUp]
        public void SetUp()
        {
            // Keep the runtime test assembly independent of the editor assembly.
            _tool = Type.GetType("CheckmateRPG.Editor.RepresentativeComboSetupEditor, CheckmateRPG.Editor", true);
            Invoke("ClearPendingState");
            _actionId = Guid.NewGuid();
            _tool.GetField("_pendingActionId", PrivateStatic).SetValue(null, _actionId);
            _tool.GetField("_pushArmed", PrivateStatic).SetValue(null, true);
        }

        [TearDown]
        public void TearDown() => Invoke("ClearPendingState");

        [TestCase("PlaceComboUnits")]
        [TestCase("RunKnockbackSplatCombo")]
        public void PendingCombo_RejectsBothMenusBeforeAccessingBattle(string menu)
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[M2Combo\] 이전 푸시"));
            Invoke(menu);
            Assert.IsTrue(IsArmed);
            Assert.AreEqual(_actionId, _tool.GetField("_pendingActionId", PrivateStatic).GetValue(null));
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Interruption_OnlyReleasesMatchingCombo(bool matching)
        {
            Invoke("HandleActionInterrupted", new ActionInterruptedEvent(new ActionInterruptedPayload(
                Guid.Empty, matching ? _actionId : Guid.NewGuid(), Guid.Empty, default, 1)));
            Assert.AreEqual(!matching, IsArmed);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Cancellation_OnlyReleasesMatchingCombo(bool matching)
        {
            Invoke("HandleActionCancelled", new ActionCancelledEvent(new ActionCancelledPayload(
                matching ? _actionId : Guid.NewGuid(), Guid.Empty, default, 1)));
            Assert.AreEqual(!matching, IsArmed);
        }

        private bool IsArmed => (bool)_tool.GetField("_pushArmed", PrivateStatic).GetValue(null);
        private void Invoke(string method, params object[] args) =>
            _tool.GetMethod(method, PrivateStatic).Invoke(null, args);
    }
}
