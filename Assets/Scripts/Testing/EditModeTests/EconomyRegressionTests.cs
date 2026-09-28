using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CheckmateRPG.Data;
using CheckmateRPG.Progression;

namespace CheckmateRPG.Tests
{
    public class EconomyRegressionTests
    {
        private readonly DateTime now=new DateTime(2026,9,28,0,0,0,DateTimeKind.Utc);
        private readonly List<UnityEngine.Object> assets=new();
        private T Asset<T>() where T:ScriptableObject {var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a;}
        private SaveData Save()=>new SaveData{LastSanityRecoverTimeTicks=now.Ticks,EconomyVersion=1};
        [TearDown]public void Cleanup(){foreach(var a in assets)UnityEngine.Object.DestroyImmediate(a);assets.Clear();}
        [Test]public void RecoveryRetainsPartialMinuteAcrossCalls()
        {var s=Save();s.Sanity=100;EconomyService.Recover(s,now.AddMinutes(7));Assert.That(s.Sanity,Is.EqualTo(101));EconomyService.Recover(s,now.AddMinutes(12));Assert.That(s.Sanity,Is.EqualTo(102));}
        [Test]public void OverflowDoesNotRegenerateAndFullDoesNotBankTime()
        {var s=Save();s.Sanity=150;EconomyService.Recover(s,now.AddDays(2));Assert.That(s.Sanity,Is.EqualTo(150));s.Sanity=119;EconomyService.Recover(s,now.AddDays(2).AddMinutes(5));Assert.That(s.Sanity,Is.EqualTo(119));}
        [Test]public void BackwardsClockDoesNotAwardSanity()
        {var s=Save();s.Sanity=10;EconomyService.Recover(s,now.AddDays(-1));Assert.That(s.Sanity,Is.EqualTo(10));}
        [Test]public void RechargeUsesDailyScheduleAndResetsOnUtcDate()
        {var s=Save();s.ResonanceCrystals=10000;int[] costs={40,40,60,60,80,100,120,150,180,200};foreach(int cost in costs){s.Sanity=0;Assert.That(EconomyService.RechargeCost(s,now),Is.EqualTo(cost));Assert.That(EconomyService.TryRecharge(s,now,out _),Is.True);}Assert.That(EconomyService.TryRecharge(s,now,out _),Is.False);Assert.That(EconomyService.RechargeCost(s,now.AddDays(1)),Is.EqualTo(40));}
        [Test]public void ShortageAndOverflowNeverConsumeCurrencyOrPacks()
        {var s=Save();s.ResonanceCrystals=39;Assert.That(EconomyService.TryRecharge(s,now,out _),Is.False);Assert.That(s.ResonanceCrystals,Is.EqualTo(39));s.Sanity=980;s.SanityRationsSmall=1;Assert.That(EconomyService.TryUseRation(s,30,now,out _),Is.False);Assert.That(s.SanityRationsSmall,Is.EqualTo(1));}
        [Test]public void LegacyMigrationPreservesProgressAndDoesNotConvertAccountTP()
        {var s=Save();s.TacticalPoints=900;s.KingProgression.PlayTP=450;s.UnitProgressions.Add(new UnitProgressionSaveData{UnitId="unit",ResonanceStage=3});s.CurrentDeck.Add("unit");EconomyService.Migrate(s,now,true);Assert.That(s.TacticalPoints,Is.EqualTo(900));Assert.That(s.AetherCredit,Is.EqualTo(50000));Assert.That(EconomyService.TotalNotationTP(s.UnitProgressions[0]),Is.EqualTo(14));s.ResonanceCrystals=1;EconomyService.Migrate(s,now);Assert.That(s.ResonanceCrystals,Is.EqualTo(1));}
        [Test]public void NewPersonalTPComesFromLevelResonanceAndMasteryOnly()
        {var u=new UnitProgressionSaveData();Assert.That(EconomyService.TotalNotationTP(u),Is.Zero);u.Level=10;u.ResonanceStage=3;u.MasteryTP=99;Assert.That(EconomyService.TotalNotationTP(u),Is.EqualTo(12));}
        [Test]public void ResonanceUsesRarityCapsAndNonResettingLevels()
        {var s=Save();var unit=Asset<UnitData>();unit.name="u";unit.Rarity=5;var u=EconomyService.Unit(s,"u");u.Level=50;u.OperatorCertificates=20;s.AetherCredit=160000;Assert.That(EconomyService.TryUpgradeResonance(s,"u",unit,out _),Is.True);Assert.That(u.Level,Is.EqualTo(50));Assert.That(u.ResonanceStage,Is.EqualTo(1));u.Level=70;Assert.That(EconomyService.TryUpgradeResonance(s,"u",unit,out _),Is.True);Assert.That(u.ResonanceStage,Is.EqualTo(2));Assert.That(s.AetherCredit,Is.EqualTo(0));Assert.That(EconomyService.GetMaxLevel(5,2),Is.EqualTo(85));}
        [Test]public void UnconfiguredStageCannotChargeAndDoubleLaunchCannotChargeTwice()
        {var s=Save();var stage=Asset<StageData>();Assert.That(EconomyService.TryBeginSortie(s,stage,now,out _),Is.False);stage.StageID="s";stage.EconomyConfigured=true;stage.SanityCost=6;Assert.That(EconomyService.TryBeginSortie(s,stage,now,out _),Is.True);Assert.That(EconomyService.TryBeginSortie(s,stage,now,out _),Is.False);Assert.That(s.Sanity,Is.EqualTo(114));}
        [TestCase(0,.02)][TestCase(49,.02)][TestCase(50,.04)][TestCase(59,.22)][TestCase(78,.60)][TestCase(79,1)]public void PityMatchesSpecification(int misses,double expected)=>Assert.That(RecruitmentService.FiveStarChance(misses),Is.EqualTo(expected).Within(.00001));
        private RecruitBannerData Banner()
        {var b=Asset<RecruitBannerData>();b.BannerId="test";b.Entries=new RecruitEntry[3];for(int i=0;i<3;i++){var u=Asset<UnitData>();u.name="star"+(i+3);u.PieceType=ChessPieceType.Pawn;b.Entries[i]=new RecruitEntry{Unit=u,Stars=i+3};}return b;}
        [Test]public void TenPullUsesTicketsFirstGuaranteesFourAndGrantsRealUnits()
        {var s=Save();var b=Banner();Assert.That(RecruitmentService.TryRecruit(s,b,10,now,()=>.99,out var results,out _),Is.True);Assert.That(s.StandardPermits,Is.Zero);Assert.That(s.ResonanceCrystals,Is.EqualTo(750));Assert.That(results[9].Stars,Is.EqualTo(4));Assert.That(s.UnlockedUnits,Does.Contain("star3"));Assert.That(EconomyService.Unit(s,"star3").OperatorCertificates,Is.EqualTo(8));Assert.That(EconomyService.Unit(s,"star3").ResonanceStage,Is.EqualTo(0));}
        [Test]public void HardPityGuaranteesFiveResetsAndSurvivesJson()
        {var s=Save();s.PityCounter=79;s=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s));Assert.That(RecruitmentService.TryRecruit(s,Banner(),1,now,()=>.99,out var result,out _),Is.True);Assert.That(result[0].Stars,Is.EqualTo(5));Assert.That(s.PityCounter,Is.Zero);}
        [Test]public void InvalidPoolAndInsufficientFundsNeverMutateSave()
        {var s=Save();var b=Banner();b.Entries[0].Unit.PieceType=ChessPieceType.King;string before=JsonUtility.ToJson(s);Assert.That(RecruitmentService.TryRecruit(s,b,1,now,()=>.1,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(s),Is.EqualTo(before));b.Entries[0].Unit.PieceType=ChessPieceType.Pawn;s.StandardPermits=0;s.ResonanceCrystals=149;before=JsonUtility.ToJson(s);Assert.That(RecruitmentService.TryRecruit(s,b,1,now,()=>.1,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(s),Is.EqualTo(before));}
        [Test]public void CoreExchangeConsumesOneFor100()
        {var s=Save();s.PaidOriginCores=1;Assert.That(EconomyService.TryExchangeCore(s,out _),Is.True);Assert.That(s.PaidOriginCores,Is.Zero);Assert.That(s.ResonanceCrystals,Is.EqualTo(1600));Assert.That(EconomyService.TryExchangeCore(s,out _),Is.False);}
        [Test]public void LevelFormulaAndRecordInjectionFollowSpecification()
        {Assert.That(EconomyService.RequiredExpForLevel(1),Is.EqualTo(100));var s=Save();var unit=Asset<UnitData>();unit.name="record-unit";unit.Rarity=3;s.RecordChip_T1=1;s.AetherCredit=1000;Assert.That(EconomyService.TryInjectRecords(s,unit,1,1,out int gained,out _),Is.True);Assert.That(gained,Is.EqualTo(1));Assert.That(EconomyService.Unit(s,"record-unit").Level,Is.EqualTo(2));Assert.That(s.RecordChip_T1,Is.Zero);Assert.That(s.AetherCredit,Is.EqualTo(960));}
        [Test]public void OfficialRosterContainsFortyOperatorsAndValidStandardBanner()
        {Assert.That(RecruitRosterCatalog.AllDefinitions.Count,Is.EqualTo(40));var banner=RecruitRosterCatalog.GetStandardBanner();Assert.That(banner.Entries.Length,Is.EqualTo(40));Assert.That(RecruitmentService.Validate(banner,now,out _),Is.True);Assert.That(banner.Entries.Count(e=>e.Stars==5),Is.EqualTo(10));Assert.That(banner.Entries.Count(e=>e.Stars==4),Is.EqualTo(14));Assert.That(banner.Entries.Count(e=>e.Stars==3),Is.EqualTo(16));}
    }
}
