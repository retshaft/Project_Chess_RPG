using UnityEngine;

namespace CheckmateRPG.Progression
{
    public static class PolarityCalculator
    {
        /// <summary>
        /// Calculates the final Sync Capacity based on the King's Innate Polarity and the equipped Absolute Edict.
        /// Matches double the base capacity.
        /// </summary>
        public static int CalculateCapacity(int baseCapacity, PolarityType innatePolarity, EdictData absoluteEdict)
        {
            if (absoluteEdict == null || absoluteEdict.Kind != EdictKind.Absolute)
                return baseCapacity;

            if (absoluteEdict.Polarity != PolarityType.Neutral && absoluteEdict.Polarity == innatePolarity)
                return baseCapacity * 2;

            return baseCapacity;
        }

        /// <summary>
        /// 마스터 기획서 Part 4 명세 공식:
        /// 최대 싱크로 수용량 = (킹 슈트 기본 수용량) + (인게임 플레이 획득 TP 총합) + (절대 칙령 보너스 수용량, 극성 일치 시 2배 증폭!)
        /// </summary>
        public static int CalculateMaxSyncCapacity(int baseSuitCapacity, int playedTP, EdictData absoluteEdict, PolarityType kingSlotPolarity)
        {
            int totalCapacity = baseSuitCapacity + playedTP;
            if (absoluteEdict != null && absoluteEdict.Kind == EdictKind.Absolute)
            {
                // 절대 칙령은 수용량을 늘려주는 보너스 역할을 수행함
                int bonus = Mathf.Max(0, absoluteEdict.SyncCost);
                if (absoluteEdict.Polarity != PolarityType.Neutral && absoluteEdict.Polarity == kingSlotPolarity)
                {
                    bonus *= 2; // 극성 일치 시 보너스 수용량 2배 증폭!
                }
                totalCapacity += bonus;
            }
            return totalCapacity;
        }

        // Slot 0 is absolute; slots 1..N are general. Old saves have neutral general slots.
        public static PolarityType GetSlotPolarity(KingProgressionSaveData save, int slot, PolarityType absoluteFallback)
        {
            if (save?.SlotPolarities != null && slot >= 0 && slot < save.SlotPolarities.Count)
                return save.SlotPolarities[slot];
            return slot == 0 ? absoluteFallback : PolarityType.Neutral;
        }

        public static int CalculateSlotCost(EdictData edict, PolarityType slotPolarity)
        {
            if (edict == null || edict.Kind == EdictKind.Absolute) return 0;
            int cost = Mathf.Max(0, edict.SyncCost);
            if (edict.Polarity == PolarityType.Neutral || slotPolarity == PolarityType.Neutral) return cost;
            // Conventional half-up rounding: 13 / 2 -> 7, 15 / 2 -> 8.
            return Mathf.FloorToInt(cost * (edict.Polarity == slotPolarity ? 0.5f : 1.2f) + 0.5f);
        }

        /// <summary>
        /// Calculates the final Sync Cost of a General Edict based on the polarity of the equipped Absolute Edict.
        /// Matches reduce cost by 50% (rounded). Mismatches increase cost by 20% (rounded).
        /// </summary>
        public static int CalculateEdictCost(EdictData generalEdict, EdictData absoluteEdict)
        {
            if (generalEdict == null)
                return 0;

            if (generalEdict.Polarity == PolarityType.Neutral || absoluteEdict == null || absoluteEdict.Polarity == PolarityType.Neutral)
                return generalEdict.SyncCost;

            if (generalEdict.Polarity == absoluteEdict.Polarity)
            {
                return Mathf.RoundToInt(generalEdict.SyncCost * 0.5f);
            }
            else
            {
                return Mathf.RoundToInt(generalEdict.SyncCost * 1.2f);
            }
        }
    }
}
