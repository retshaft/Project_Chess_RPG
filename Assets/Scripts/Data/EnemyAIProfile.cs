using UnityEngine;

namespace CheckmateRPG.Data
{
    public enum AIBehaviorType
    {
        Aggressive, // Seeks closest/highest value target
        Defensive,  // 수비형: 니가와(대기) 전술, 사거리 내 진입 시 카운터
        Assassin,   // Specifically hunts lowest HP or highest value targets bypassing tanks
        Berserker,  // 광전사: AP가 모이는 즉시 공격 및 전진 압박 (AP 보존 가중치 0)
        Tactician   // 전략가: 퀸 등 고비용/고화력 일격 가동을 위해 AP 저축
    }

    [CreateAssetMenu(fileName = "NewEnemyAIProfile", menuName = "CheckmateRPG/Enemy AI Profile", order = 2)]
    public class EnemyAIProfile : ScriptableObject
    {
        [Tooltip("The core behavior archetype of this AI.")]
        public AIBehaviorType BehaviorType = AIBehaviorType.Aggressive;

        [Tooltip("Bonus score multiplier when evaluating a target that matches the Assassin's preferred criteria (e.g., low HP).")]
        public float AssassinTargetBonus = 2.0f;

        [Tooltip("How much the distance penalty affects the AI's decision. Higher means AI strongly prefers closer targets.")]
        public float DistancePenaltyWeight = 1.0f;

        [Header("M10 Utility Weights")]
        [Tooltip("Weight for preserving AP when Team AP is low. High value makes the unit skip turns to save AP.")]
        public float APConservationWeight = 1.0f;

        [Tooltip("Bonus for moves that result in an enemy taking Splat damage (wall or unit collision).")]
        public float SetupKillWeight = 1.5f;

        [Tooltip("Bonus for moving adjacent to the Ally King if the King is threatened.")]
        public float KingProtectionWeight = 2.0f;

        [Tooltip("Priority for attacking enemies with low HP.")]
        public float WeakEnemyFocusWeight = 1.5f;

        [Tooltip("Penalty for moving into a tile that is threatened by enemy attacks (Danger Map).")]
        public float ThreatAversionWeight = 1.0f;
    }
}
