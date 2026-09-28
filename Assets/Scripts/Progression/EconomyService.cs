using System;
using System.Collections.Generic;
using CheckmateRPG.Data;

namespace CheckmateRPG.Progression
{
    /// <summary>Account economy only. UTC time is injected for deterministic recovery and tests.</summary>
    public static class EconomyService
    {
        public const int Version = 2;
        public const long CreditCap = 99999999;
        public const long RecoveryTicks = TimeSpan.TicksPerMinute * 6;
        public const int RecordT1Exp = 200;
        public const int RecordT2Exp = 1000;
        public const int RecordT3Exp = 4000;
        public const int RecordT4Exp = 20000;
        private static readonly int[] RechargeCosts = {40,40,60,60,80,100,120,150,180,200};

        public static void Migrate(SaveData save, DateTime now, bool legacy = false)
        {
            save.UnlockedUnits ??= new List<string>();
            save.CurrentDeck ??= new List<string>();
            save.UnitProgressions ??= new List<UnitProgressionSaveData>();
            save.KingProgression ??= new KingProgressionSaveData();
            save.ClearedStages ??= new List<string>();
            if (legacy)
            {
                save.Sanity = 120; save.AetherCredit = 50000; save.ResonanceCrystals = 1500;
                save.StandardPermits = 5; save.LastSanityRecoverTimeTicks = now.Ticks;
                foreach (var id in save.CurrentDeck)
                    if (!string.IsNullOrEmpty(id) && !save.UnlockedUnits.Contains(id)) save.UnlockedUnits.Add(id);
                foreach (var unit in save.UnitProgressions)
                {
                    if (unit == null) continue;
                    // Keep the existing prototype's personal budget, not the account TP balance.
                    unit.LegacyNotationTP = Math.Max(0, 10 + (Math.Clamp(unit.ResonanceStage,1,5)-1)*2 - ResonanceTP(unit.ResonanceStage));
                }
            }
            foreach (var unit in save.UnitProgressions)
                if (unit != null) { unit.Level = Math.Max(1,unit.Level); unit.CurrentExp = Math.Max(0, unit.CurrentExp); unit.ResonanceStage = Math.Max(0, unit.ResonanceStage); unit.UnlockedNodeIds ??= new List<string>(); }
            save.EconomyVersion = Version;
            save.MaxSanity = (int)Math.Min(180,120L+Math.Max(0L,(long)save.KingProgression.SuitLevel-1)*2);
            save.Sanity = Math.Clamp(save.Sanity,0,999);
            save.AetherCredit = Math.Clamp(save.AetherCredit,0,CreditCap);
            save.RecordChip_T1 = Math.Max(0, save.RecordChip_T1);
            save.RecordChip_T2 = Math.Max(0, save.RecordChip_T2);
            save.RecordChip_T3 = Math.Max(0, save.RecordChip_T3);
            save.RecordChip_T4 = Math.Max(0, save.RecordChip_T4);
            if (save.RecordChip_T1 == 0 && save.ExperienceChips > 0)
            {
                // Older saves stored a single chip count. Treat it as the basic T1 record.
                save.RecordChip_T1 = save.ExperienceChips;
            }
            Recover(save,now);
        }

        public static bool Recover(SaveData save, DateTime now)
        {
            long ticks = now.Ticks;
            if (save.LastSanityRecoverTimeTicks <= 0 || save.LastSanityRecoverTimeTicks > ticks || save.Sanity >= save.MaxSanity)
            { save.LastSanityRecoverTimeTicks=ticks; return false; }
            long earned = (ticks-save.LastSanityRecoverTimeTicks)/RecoveryTicks;
            if (earned==0) return false;
            int added = (int)Math.Min(earned,save.MaxSanity-save.Sanity);
            save.Sanity += added;
            save.LastSanityRecoverTimeTicks = save.Sanity >= save.MaxSanity ? ticks : save.LastSanityRecoverTimeTicks+added*RecoveryTicks;
            return added>0;
        }

        public static int RechargeCost(SaveData save, DateTime now)
        {
            int count=save.RechargeUtcDate==now.ToString("yyyy-MM-dd") ? save.DailyRechargeCount : 0;
            return count>=0 && count<RechargeCosts.Length ? RechargeCosts[count] : -1;
        }
        public static bool TryRecharge(SaveData save, DateTime now, out string reason)
        {
            Recover(save,now);
            int cost=RechargeCost(save,now);
            reason=cost<0 ? "오늘 전력 충전 10회를 모두 사용했습니다." : save.ResonanceCrystals<cost ? "공명석이 부족합니다." : save.Sanity>939 ? "전력 보유 상한 999를 초과합니다." : "";
            if(reason.Length>0)return false;
            if(save.RechargeUtcDate!=now.ToString("yyyy-MM-dd"))save.DailyRechargeCount=0;
            save.RechargeUtcDate=now.ToString("yyyy-MM-dd"); save.DailyRechargeCount++;
            save.ResonanceCrystals-=cost; save.Sanity+=60;
            return true;
        }
        public static bool TryUseRation(SaveData save, int amount, DateTime now, out string reason)
        {
            Recover(save,now);
            int count=amount==30 ? save.SanityRationsSmall : amount==60 ? save.SanityRationsMedium : amount==120 ? save.SanityRationsLarge : 0;
            reason=count<=0 ? "해당 비상 전력 팩이 없습니다." : save.Sanity>999-amount ? "전력 보유 상한 999를 초과합니다." : "";
            if(reason.Length>0)return false;
            if(amount==30)save.SanityRationsSmall--; else if(amount==60)save.SanityRationsMedium--; else save.SanityRationsLarge--;
            save.Sanity+=amount; return true;
        }
        public static bool TryExchangeCore(SaveData save, out string reason)
        {
            reason=save.PaidOriginCores<1 ? "보유 오리진 코어가 없습니다. 현금 결제는 아직 연결되지 않았습니다." : save.ResonanceCrystals>int.MaxValue-100 ? "공명석 저장 상한에 도달했습니다." : "";
            if(reason.Length>0)return false;
            save.PaidOriginCores--; save.ResonanceCrystals+=100; return true;
        }
        public static bool CanEnter(SaveData save, StageData stage, DateTime now, out string reason)
        {
            Recover(save,now);
            int cost=stage==null?0:GetSanityCost(stage);
            reason=stage==null ? "작전을 먼저 선택하세요." : !stage.EconomyConfigured ? "이 작전의 전력·보상 수치 설정이 필요합니다." : cost<=0 || cost>999 || stage.RewardCredits<0 || stage.FirstClearBonusCredits<0 || stage.RewardExperienceChips<0 || stage.RepeatCredits<0 ? "작전 경제 설정이 올바르지 않습니다." : save.Sanity<cost ? "인가 전력이 부족합니다." : "";
            return reason.Length==0;
        }
        public static bool TryBeginSortie(SaveData save, StageData stage, DateTime now, out string reason)
        {
            if(!CanEnter(save,stage,now,out reason))return false;
            if(!string.IsNullOrEmpty(save.ActiveSortieId)){reason="이미 출격 처리 중입니다."; return false;}
            int cost=GetSanityCost(stage); save.Sanity-=cost;
            save.ActiveSortieId=Guid.NewGuid().ToString("N"); save.ActiveSortieStageId=StageKey(stage); save.ActiveSortieCost=cost;
            return true;
        }
        public static int GetSanityCost(StageData stage) => stage==null?0:(stage.SanityCost>0?stage.SanityCost:stage.RequiredSanity);
        public static string StageKey(StageData stage)=>string.IsNullOrEmpty(stage.StageID)?stage.name:stage.StageID;
        public static void EndSortie(SaveData save) { save.ActiveSortieId=""; save.ActiveSortieStageId=""; save.ActiveSortieCost=0; }
        public static bool TrySpendCredits(SaveData save,long cost)
        { if(cost<0 || save.AetherCredit<cost)return false; save.AetherCredit-=cost; return true; }
        public static int ResonanceTP(int stage)=>stage>=3?5:stage==2?2:0;
        public static int TotalNotationTP(UnitProgressionSaveData unit)=>Math.Max(0,unit.LegacyNotationTP)+Math.Max(1,unit.Level)/5+ResonanceTP(unit.ResonanceStage)+Math.Clamp(unit.MasteryTP,0,5);
        public static UnitProgressionSaveData Unit(SaveData save,string id)
        {
            var unit=save.UnitProgressions.Find(x=>x!=null&&x.UnitId==id);
            if(unit==null){unit=new UnitProgressionSaveData{UnitId=id};save.UnitProgressions.Add(unit);}
            return unit;
        }
        public static int GetMaxLevel(int rarity, int resonanceStage)
        {
            int stage=Math.Clamp(resonanceStage,0,2);
            if (rarity==3) return stage==0?30:stage==1?45:55;
            if (rarity==4) return stage==0?40:stage==1?60:70;
            if (rarity==5) return stage==0?50:stage==1?70:85;
            return 1;
        }

        public static int RequiredExpForLevel(int level)
        {
            if (level < 1) return 0;
            return 100 + (int)Math.Floor(40d * Math.Pow(level - 1, 1.25d));
        }

        public static int RecordExp(int tier) => tier switch { 1 => RecordT1Exp, 2 => RecordT2Exp, 3 => RecordT3Exp, 4 => RecordT4Exp, _ => 0 };
        public static int RecordCount(SaveData save, int tier) => tier switch { 1 => save.RecordChip_T1, 2 => save.RecordChip_T2, 3 => save.RecordChip_T3, 4 => save.RecordChip_T4, _ => 0 };
        private static void SetRecordCount(SaveData save, int tier, int value)
        {
            switch (tier) { case 1: save.RecordChip_T1=value; break; case 2: save.RecordChip_T2=value; break; case 3: save.RecordChip_T3=value; break; case 4: save.RecordChip_T4=value; break; }
        }

        public static bool TryInjectRecords(SaveData save, UnitData unitData, int tier, int count, out int levelsGained, out string reason)
        {
            levelsGained=0; reason="";
            if (save==null || unitData==null || count<=0 || tier<1 || tier>4) { reason="작전기록 칩 주입 정보가 올바르지 않습니다."; return false; }
            var progression=Unit(save,unitData.name);
            int max=GetMaxLevel(Math.Clamp(unitData.Rarity,3,5),progression.ResonanceStage);
            if (progression.Level>=max) { reason=$"현재 공명 단계의 최대 레벨({max})에 도달했습니다."; return false; }
            int available=Math.Min(count,RecordCount(save,tier));
            if (available<=0) { reason="해당 등급의 작전기록 칩이 없습니다."; return false; }
            int expPer=RecordExp(tier); int consumed=0; int simulatedLevel=progression.Level; int simulatedExp=progression.CurrentExp;
            for(int i=0;i<available && simulatedLevel<max;i++)
            {
                consumed++;
                simulatedExp+=expPer;
                while(simulatedLevel<max)
                {
                    int needed=RequiredExpForLevel(simulatedLevel);
                    if(simulatedExp<needed) break;
                    simulatedExp-=needed; simulatedLevel++; levelsGained++;
                }
                if(simulatedLevel>=max) simulatedExp=0;
            }
            long totalCost=(long)expPer*consumed/5;
            if (consumed==0) { levelsGained=0; reason="현재 공명 단계의 최대 레벨에 도달했습니다."; return false; }
            if (save.AetherCredit<totalCost) { levelsGained=0; reason="에테르 크레딧이 부족합니다."; return false; }
            progression.Level=simulatedLevel; progression.CurrentExp=simulatedExp;
            SetRecordCount(save,tier,RecordCount(save,tier)-consumed); save.AetherCredit-=totalCost;
            reason=$"Lv.{progression.Level} / +{levelsGained} 레벨"; return true;
        }

        public static bool TryUpgradeResonance(SaveData save,string id,out string reason)
            => TryUpgradeResonance(save, id, FindUnit(id), out reason);

        public static bool TryUpgradeResonance(SaveData save,string id,UnitData unitData,out string reason)
        {
            var unit=Unit(save,id); int rank=Math.Clamp(unit.ResonanceStage,0,2);
            int rarity=Math.Clamp(unitData!=null?unitData.Rarity:5,3,5);
            int requiredLevel=GetMaxLevel(rarity,rank);
            int certificates=rank==0?5:15;
            long credits=rank==0?(rarity==3?10000:rarity==4?20000:40000):(rarity==3?30000:rarity==4?60000:120000);
            reason=rank>=2?"공명 돌파 완료":unit.Level<requiredLevel?$"현재 레벨 {requiredLevel}을 달성해야 합니다.":unit.OperatorCertificates<certificates?"공명 증표가 부족합니다.":save.AetherCredit<credits?"에테르 크레딧이 부족합니다.":"";
            if(reason.Length>0)return false;
            unit.OperatorCertificates-=certificates;save.AetherCredit-=credits;unit.ResonanceStage++;return true;
        }

        private static UnitData FindUnit(string id)
        {
            var all=UnityEngine.Resources.LoadAll<UnitData>("Units");
            foreach(var item in all) if(item!=null&&item.name==id) return item;
            return null;
        }
    }
}
