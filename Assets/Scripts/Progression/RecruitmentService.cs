using System;
using System.Collections.Generic;
using System.Linq;
using CheckmateRPG.Data;

namespace CheckmateRPG.Progression
{
    public static class RecruitmentService
    {
        public static double FiveStarChance(int misses) => misses>=79 ? 1 : .02+Math.Max(0,misses-49)*.02;
        public static bool Validate(RecruitBannerData banner, DateTime now, out string reason)
        {
            reason="소집 대상·등급 설정이 필요합니다. 재화는 차감되지 않습니다.";
            if(banner==null || string.IsNullOrWhiteSpace(banner.BannerId) || banner.Entries==null)return false;
            if(!string.IsNullOrEmpty(banner.EndUtc))
            {
                if(!DateTime.TryParse(banner.EndUtc,null,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out var end))return false;
                if(now>=end){reason="종료된 소집 채널입니다.";return false;}
            }
            var entries=banner.Entries;
            if(entries.Any(e=>e==null||!DeckManager.IsPlayerUnit(e.Unit)||e.Unit.PieceType==ChessPieceType.King||e.Stars<3||e.Stars>5))return false;
            if(entries.Select(e=>e.Unit.name).Distinct().Count()!=entries.Length)return false;
            for(int stars=3;stars<=5;stars++)if(!entries.Any(e=>e.Stars==stars))return false;
            if(banner.IsPickup && (banner.PickupUnit==null || !entries.Any(e=>e.Stars==5&&e.Unit==banner.PickupUnit) || !entries.Any(e=>e.Stars==5&&e.Unit!=banner.PickupUnit)))return false;
            reason="";return true;
        }
        public static void GrantUnit(SaveData save,string id,int stars)
        {
            bool owned=save.UnlockedUnits.Contains(id)||save.CurrentDeck.Contains(id);
            if(!save.UnlockedUnits.Contains(id))save.UnlockedUnits.Add(id);
            var unit=EconomyService.Unit(save,id);
            if(owned)unit.OperatorCertificates=(int)Math.Min(int.MaxValue,(long)unit.OperatorCertificates+(stars==5?15:stars==4?5:1));
        }
        public static bool TryRecruit(SaveData save,RecruitBannerData banner,int count,DateTime now,Func<double> random,out List<RecruitEntry> results,out string reason)
        {
            results=new List<RecruitEntry>();
            if(!Validate(banner,now,out reason))return false;
            if(count!=1&&count!=10){reason="1회 또는 10회 소집만 가능합니다.";return false;}
            if(banner.IsPickup && !string.IsNullOrEmpty(save.MileageBannerId)&&save.MileageBannerId!=banner.BannerId&&save.HeadhuntingMileage>0)
            {reason="이전 픽업의 종료 정산이 필요합니다.";return false;}
            int tickets=Math.Min(count,Math.Max(0,banner.IsPickup?save.SpecialPermits:save.StandardPermits));
            int crystals=(count-tickets)*150;
            if(save.ResonanceCrystals<crystals){reason=$"허가증 {tickets}장 + 공명석 {crystals:N0}개가 필요합니다.";return false;}
            if((banner.IsPickup?save.HeadhuntingMileage:save.StandardHeadhuntingMileage)>int.MaxValue-count){reason="증명서 저장 상한입니다.";return false;}
            int pity=save.PityCounter; bool high=false;
            // Resolve all draws before changing any balances.
            for(int i=0;i<count;i++)
            {
                double roll=random();
                if(double.IsNaN(roll)||roll<0||roll>=1){reason="난수 값 오류";return false;}
                double five=FiveStarChance(pity);
                int stars=roll<five?5:roll<Math.Min(1,five+.08)?4:3;
                if(count==10 && i==9 && !high && stars==3)stars=4;
                var pool=banner.Entries.Where(e=>e.Stars==stars).ToArray();
                RecruitEntry selected;
                if(stars==5&&banner.IsPickup&&random()<.5)selected=pool.First(e=>e.Unit==banner.PickupUnit);
                else
                {
                    if(stars==5&&banner.IsPickup)pool=pool.Where(e=>e.Unit!=banner.PickupUnit).ToArray();
                    selected=pool[Math.Clamp((int)(random()*pool.Length),0,pool.Length-1)];
                }
                results.Add(selected);high|=stars>=4;pity=stars==5?0:pity+1;
            }
            if(banner.IsPickup)save.SpecialPermits-=tickets;else save.StandardPermits-=tickets;
            save.ResonanceCrystals-=crystals; save.PityCounter=pity;
            if(banner.IsPickup)save.HeadhuntingMileage+=count;else save.StandardHeadhuntingMileage+=count;
            if(banner.IsPickup)save.MileageBannerId=banner.BannerId;
            foreach(var result in results)GrantUnit(save,result.Unit.name,result.Stars);
            reason="";return true;
        }
        public static bool TryExchangePickup(SaveData save,RecruitBannerData banner,DateTime now,out string reason)
        {
            if(!Validate(banner,now,out reason))return false;
            if(!banner.IsPickup||save.MileageBannerId!=banner.BannerId||save.HeadhuntingMileage<300){reason="해당 픽업 증명서 300개가 필요합니다.";return false;}
            save.HeadhuntingMileage-=300;GrantUnit(save,banner.PickupUnit.name,5);return true;
        }
        public static bool SettleExpiredBanner(SaveData save,RecruitBannerData banner,DateTime now)
        {
            if(banner==null||save.MileageBannerId!=banner.BannerId||string.IsNullOrEmpty(banner.EndUtc)||!DateTime.TryParse(banner.EndUtc,null,System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out var end)||now<end)return false;
            if((long)save.GeneralExchangeCertificates+save.HeadhuntingMileage>int.MaxValue)return false;
            save.GeneralExchangeCertificates+=save.HeadhuntingMileage;save.HeadhuntingMileage=0;save.MileageBannerId="";return true;
        }
    }
}
