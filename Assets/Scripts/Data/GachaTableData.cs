using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Data
{
    [Serializable]
    public class GachaDrop
    {
        [Tooltip("획득 가능한 유닛 데이터")]
        public UnitData Unit;
        
        [Tooltip("가중치 (확률 = 가중치 / 총 가중치)")]
        [Min(0.01f)]
        public float Weight = 1f;
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Gacha Table", fileName = "NewGachaTable")]
    public class GachaTableData : ScriptableObject
    {
        [Header("Drop Table Configuration")]
        public List<GachaDrop> Drops = new List<GachaDrop>();

        /// <summary>
        /// 설정된 가중치를 기반으로 무작위 유닛을 하나 뽑습니다.
        /// </summary>
        public UnitData PullRandom()
        {
            if (Drops == null || Drops.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var drop in Drops)
            {
                if (drop.Unit != null) totalWeight += drop.Weight;
            }

            if (totalWeight <= 0f) return null;

            float rand = UnityEngine.Random.Range(0f, totalWeight);
            float current = 0f;

            foreach (var drop in Drops)
            {
                if (drop.Unit == null) continue;
                
                current += drop.Weight;
                if (rand <= current)
                {
                    return drop.Unit;
                }
            }
            
            // Fallback
            return Drops[Drops.Count - 1].Unit;
        }
    }
}
