using System;
using NUnit.Framework;
using CheckmateRPG.Progression;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    public class BattleRewardServiceTests
    {
        private StageData stage;
        private SaveData save;
        private readonly DateTime now=new DateTime(2026,9,28,0,0,0,DateTimeKind.Utc);
        [SetUp] public void Setup()
        {
            stage=ScriptableObject.CreateInstance<StageData>();stage.StageID="economy-test";stage.EconomyConfigured=true;
            stage.SanityCost=6;stage.RewardCredits=100;stage.FirstClearBonusCredits=200;stage.RewardExperienceChips=2;
            save=new SaveData{LastSanityRecoverTimeTicks=now.Ticks};
        }
        [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(stage);
        private void Begin()=>Assert.That(EconomyService.TryBeginSortie(save,stage,now,out _),Is.True);
        [Test] public void FirstClearCreditsAndCrystalsNeverAwardAccountTPOrSuitLevel()
        {
            Begin();Assert.That(BattleRewardService.TryGrantStageClearReward(save,stage,out long c,out int r,out int chips,out bool first),Is.True);
            Assert.That(c,Is.EqualTo(300));Assert.That(r,Is.EqualTo(50));Assert.That(chips,Is.EqualTo(2));Assert.That(first,Is.True);
            Assert.That(save.TacticalPoints,Is.Zero);Assert.That(save.KingProgression.PlayTP,Is.Zero);Assert.That(save.KingProgression.SuitLevel,Is.EqualTo(1));
        }
        [Test] public void SameSortieCannotRewardTwiceButPaidRepeatCan()
        {
            Begin();BattleRewardService.TryGrantStageClearReward(save,stage,out _,out _,out _,out _);
            string before=JsonUtility.ToJson(save);
            Assert.That(BattleRewardService.TryGrantStageClearReward(save,stage,out _,out _,out _,out _),Is.False);
            Assert.That(JsonUtility.ToJson(save),Is.EqualTo(before));
            Begin();Assert.That(BattleRewardService.TryGrantStageClearReward(save,stage,out long c,out int r,out _,out bool first),Is.True);
            Assert.That(c,Is.EqualTo(100));Assert.That(r,Is.Zero);Assert.That(first,Is.False);Assert.That(save.Sanity,Is.EqualTo(108));
        }
        [Test] public void AnotherStageCannotConsumeReceipt()
        {Begin();stage.StageID="other";Assert.That(BattleRewardService.TryGrantStageClearReward(save,stage,out _,out _,out _,out _),Is.False);}
        [Test] public void InvalidRewardsAreRejectedWithoutMutation()
        {Begin();stage.RewardCredits=-1;string before=JsonUtility.ToJson(save);Assert.That(BattleRewardService.TryGrantStageClearReward(save,stage,out _,out _,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(save),Is.EqualTo(before));}
        [Test] public void BossFirstClearAwards100AndCreditCapDoesNotWrap()
        {save.AetherCredit=EconomyService.CreditCap-10;stage.IsBossStage=true;Begin();BattleRewardService.TryGrantStageClearReward(save,stage,out long c,out int r,out _,out _);Assert.That(c,Is.EqualTo(10));Assert.That(r,Is.EqualTo(100));}
        [Test] public void ReceiptSurvivesReloadAndIsConsumedOnce()
        {Begin();save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));Assert.That(BattleRewardService.TryGrantStageClearReward(save,stage,out _,out _,out _,out _),Is.True);save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));Assert.That(BattleRewardService.TryGrantStageClearReward(save,stage,out _,out _,out _,out _),Is.False);}
        [Test] public void OfficialTierRewardsSplitFirstAndRepeatAndUseChanceDrop()
        {stage.OfficialRewardTable=true;stage.RewardCredits=999;stage.FirstClearBonusCredits=0;stage.FirstClearCredits=3000;stage.FirstClearCrystals=50;stage.RewardExperienceChips=0;stage.FirstClearChipT1=5;stage.RepeatCredits=600;stage.RepeatChipT1=2;stage.ChanceDropT1=1f;Begin();Assert.That(BattleRewardService.TryGrantStageClearReward(save,stage,()=>0f,out var first),Is.True);Assert.That(first.Credits,Is.EqualTo(3000));Assert.That(first.T1,Is.EqualTo(5));Begin();Assert.That(BattleRewardService.TryGrantStageClearReward(save,stage,()=>0f,out var repeat),Is.True);Assert.That(repeat.Credits,Is.EqualTo(600));Assert.That(repeat.T1,Is.EqualTo(3));Assert.That(save.RecordChip_T1,Is.EqualTo(8));}
    }
}
