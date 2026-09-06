using UnityEngine;

namespace CheckmateRPG.Core
{
    /// <summary>
    /// 물리 엔진 2.0의 순수 수식 규칙 (GDD Part 2 §2). 결정론적이며 외부 상태에 의존하지 않아
    /// EditMode 테스트에서 직접 검증할 수 있다.
    /// </summary>
    public static class PhysicsRules
    {
        /// <summary>
        /// 넉백 거리 = 힘(Force) − 실효 무게(Weight), 하한 0.
        /// 비틀거림(Stagger) 상태 대상은 무게를 임시 1 감소시켜 계산하며, 보스급은 무게 감소에 면역이다. (GDD Part 2 §2.3)
        /// </summary>
        public static int ComputeKnockbackDistance(int force, int weight, bool staggered, bool isBoss)
        {
            int effectiveWeight = Mathf.Clamp(weight, 0, 4);
            if (staggered && !isBoss)
                effectiveWeight = Mathf.Max(0, effectiveWeight - 1);

            return Mathf.Max(0, force - effectiveWeight);
        }
    }
}
