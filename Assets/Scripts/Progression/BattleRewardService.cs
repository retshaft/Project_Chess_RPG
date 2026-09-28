using System;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    public struct StageRewardBreakdown
    {
        public long Credits;
        public int Crystals;
        public int T1, T2, T3, T4;
        public bool FirstClear;
        public int TotalRecords => T1 + T2 + T3 + T4;
    }

    public static class BattleRewardService
    {
        public static bool TryGrantStageClearReward(SaveData save, StageData stage, out long credits, out int crystals, out int chips, out bool firstClear)
        {
            bool ok=TryGrantStageClearReward(save,stage,()=>UnityEngine.Random.value,out StageRewardBreakdown reward);
            credits=reward.Credits; crystals=reward.Crystals; chips=reward.TotalRecords; firstClear=reward.FirstClear;
            return ok;
        }

        public static bool TryGrantStageClearReward(SaveData save, StageData stage, Func<float> roll, out StageRewardBreakdown reward)
        {
            reward=new StageRewardBreakdown();
            if(save==null||stage==null||roll==null||!stage.EconomyConfigured||string.IsNullOrEmpty(save.ActiveSortieId)||save.LastRewardedSortieId==save.ActiveSortieId||save.ActiveSortieStageId!=EconomyService.StageKey(stage))return false;
            if(stage.RewardCredits<0||stage.FirstClearBonusCredits<0||stage.RewardExperienceChips<0||stage.RepeatCredits<0)return false;
            string key=EconomyService.StageKey(stage);
            bool first=!save.ClearedStages.Contains(key);
            reward.FirstClear=first;
            long requestedCredits=first
                ? (stage.OfficialRewardTable?stage.FirstClearCredits:stage.FirstClearBonusCredits+stage.RewardCredits)
                : (stage.RepeatCredits>0?stage.RepeatCredits:stage.RewardCredits);
            reward.Credits=Math.Min(requestedCredits,Math.Max(0,EconomyService.CreditCap-save.AetherCredit));
            reward.Crystals=first?(stage.OfficialRewardTable?stage.FirstClearCrystals:0):0;
            if(first && !stage.OfficialRewardTable && stage.FirstClearCredits==0 && stage.FirstClearChipT1==0 && stage.FirstClearChipT2==0 && stage.FirstClearChipT3==0 && stage.FirstClearChipT4==0) reward.Crystals=stage.IsBossStage?100:50;
            reward.T1=first?(stage.OfficialRewardTable?stage.FirstClearChipT1:stage.RewardExperienceChips):stage.RepeatChipT1;
            reward.T2=first?stage.FirstClearChipT2:stage.RepeatChipT2;
            reward.T3=first?stage.FirstClearChipT3:stage.RepeatChipT3;
            reward.T4=first?stage.FirstClearChipT4:stage.RepeatChipT4;
            if(!first)
            {
                int tier=stage.ChanceDropT4>0?4:stage.ChanceDropT3>0?3:stage.ChanceDropT2>0?2:stage.ChanceDropT1>0?1:0;
                float chance=tier==4?stage.ChanceDropT4:tier==3?stage.ChanceDropT3:tier==2?stage.ChanceDropT2:stage.ChanceDropT1;
                if(tier>0 && roll()<=chance)
                {
                    int count=Math.Max(1,stage.ChanceDropCount);
                    if(tier==1)reward.T1+=count; else if(tier==2)reward.T2+=count; else if(tier==3)reward.T3+=count; else reward.T4+=count;
                }
            }
            if(save.ResonanceCrystals>int.MaxValue-reward.Crystals)return false;
            if((long)save.RecordChip_T1+reward.T1>int.MaxValue||(long)save.RecordChip_T2+reward.T2>int.MaxValue||(long)save.RecordChip_T3+reward.T3>int.MaxValue||(long)save.RecordChip_T4+reward.T4>int.MaxValue)return false;
            save.AetherCredit+=reward.Credits; save.ResonanceCrystals+=reward.Crystals;
            save.RecordChip_T1+=reward.T1; save.RecordChip_T2+=reward.T2; save.RecordChip_T3+=reward.T3; save.RecordChip_T4+=reward.T4;
            save.ExperienceChips+=reward.TotalRecords;
            if(first) save.CommanderProtocolChips+=Math.Max(0,stage.FirstClearProtocolChips);
            if(first)
            {
                save.ClearedStages.Add(key);
                if(stage.ClearRewards!=null)foreach(var unit in stage.ClearRewards)
                    if(DeckManager.IsPlayerUnit(unit)&&!save.UnlockedUnits.Contains(unit.name)) {save.UnlockedUnits.Add(unit.name);EconomyService.Unit(save,unit.name);}
            }
            save.LastRewardedSortieId=save.ActiveSortieId; EconomyService.EndSortie(save);
            return true;
        }
    }
}
