using System;
using CheckmateRPG.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    /// <summary>
    /// R01a 회귀: 스테이지 클리어 보상의 첫 클리어/반복 구분, 중복 기록 방지, 저장 라운드트립을 검증한다.
    /// BattleRewardService는 SaveManager 싱글턴 없이 SaveData 인스턴스로 동작하는 순수 정적 함수이므로
    /// 실제 저장 파일을 건드리지 않는다.
    /// </summary>
    [TestFixture]
    public sealed class BattleRewardServiceTests
    {
        [Test]
        public void FirstClear_GrantsRepeatPlusBonus_AndRecordsStage()
        {
            var save = new SaveData();

            bool granted = BattleRewardService.TryGrantStageClearReward(
                save, "Stage_01", repeatRewardTP: 50, firstClearBonusTP: 20,
                out int grantedTP, out bool firstClear);

            Assert.IsTrue(granted);
            Assert.IsTrue(firstClear, "첫 클리어로 기록된다.");
            Assert.AreEqual(70, grantedTP, "첫 클리어는 반복 보상 + 첫 클리어 보너스를 합해 지급한다.");
            Assert.AreEqual(70, save.TacticalPoints);
            Assert.AreEqual(70, save.KingProgression.PlayTP);
            Assert.IsTrue(save.ClearedStages.Contains("Stage_01"), "첫 클리어가 저장 데이터에 기록된다.");
        }

        [Test]
        public void RepeatClear_GrantsRepeatOnly_AndDoesNotDuplicateRecord()
        {
            var save = new SaveData();
            BattleRewardService.TryGrantStageClearReward(save, "Stage_01", 50, 20, out _, out _);

            bool granted = BattleRewardService.TryGrantStageClearReward(
                save, "Stage_01", 50, 20, out int grantedTP, out bool firstClear);

            Assert.IsTrue(granted, "반복 클리어도 반복 보상은 지급된다.");
            Assert.IsFalse(firstClear, "두 번째 클리어는 첫 클리어가 아니다.");
            Assert.AreEqual(50, grantedTP, "반복 클리어에는 첫 클리어 보너스가 없다.");
            Assert.AreEqual(1, save.ClearedStages.Count, "같은 스테이지 기록이 중복 추가되지 않는다.");
        }

        [Test]
        public void MultipleStages_AreTrackedIndependently()
        {
            var save = new SaveData();
            BattleRewardService.TryGrantStageClearReward(save, "Stage_01", 50, 0, out _, out bool stage1First);
            BattleRewardService.TryGrantStageClearReward(save, "Stage_02", 30, 0, out _, out bool stage2First);
            BattleRewardService.TryGrantStageClearReward(save, "Stage_01", 50, 0, out _, out bool stage1Repeat);

            Assert.IsTrue(stage1First, "Stage_01 첫 클리어가 기록된다.");
            Assert.IsTrue(stage2First, "Stage_02 첫 클리어가 별도로 기록된다.");
            Assert.IsFalse(stage1Repeat, "Stage_01 재클리어는 반복 지급으로 처리된다.");
            Assert.IsTrue(save.ClearedStages.Contains("Stage_01"));
            Assert.IsTrue(save.ClearedStages.Contains("Stage_02"));
        }

        [Test]
        public void InvalidInput_ReturnsFalse_AndGrantsNothing()
        {
            var save = new SaveData();

            Assert.IsFalse(BattleRewardService.TryGrantStageClearReward(null, "Stage_01", 50, 0, out int tp1, out _));
            Assert.AreEqual(0, tp1);
            Assert.IsFalse(BattleRewardService.TryGrantStageClearReward(save, "", 50, 0, out int tp2, out _));
            Assert.AreEqual(0, tp2);
            Assert.AreEqual(0, save.TacticalPoints, "거부된 지급은 저장 데이터를 건드리지 않는다.");
        }

        [Test]
        public void NegativeRewardValues_AreClampedToZero()
        {
            var save = new SaveData();

            bool granted = BattleRewardService.TryGrantStageClearReward(save, "Stage_01", -5, -3, out int grantedTP, out _);

            Assert.IsTrue(granted);
            Assert.AreEqual(0, grantedTP, "음수 보상은 0으로 제한된다.");
            Assert.AreEqual(0, save.TacticalPoints);
        }

        [Test]
        public void ClearedStages_SurvivesJsonRoundTrip()
        {
            var save = new SaveData();
            save.ClearedStages.Add("Stage_01");
            save.ClearedStages.Add("Stage_02");

            string json = JsonUtility.ToJson(save, true);
            var restored = JsonUtility.FromJson<SaveData>(json);

            Assert.IsNotNull(restored.ClearedStages);
            Assert.AreEqual(2, restored.ClearedStages.Count, "첫 클리어 기록은 저장 라운드트립 후에도 유지된다.");
            Assert.IsTrue(restored.ClearedStages.Contains("Stage_01"));
            Assert.IsTrue(restored.ClearedStages.Contains("Stage_02"));
        }
    }
}
