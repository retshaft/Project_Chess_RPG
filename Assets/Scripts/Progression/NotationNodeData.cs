using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    public enum NotationNodeType
    {
        BasicStat,         // 일반 전술 스탯 (공격력, 방어력 등)
        TacticalPassive,   // 전술 패시브 (초기 SP, CC 저항 등)
        UltimateCheckmate  // 궁극 체크메이트 재능 (치명타/관통 극대화)
    }

    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Notation Node", fileName = "NotationNode")]
    public class NotationNodeData : ScriptableObject
    {
        [Tooltip("Stable id for this notation node.")]
        public string NodeId = "N-001";

        [Tooltip("Chess algebraic coordinate (e.g., e4, Nf3, Qxf7#)")]
        public string NotationCoord = "e4";

        [Tooltip("Type of notation node")]
        public NotationNodeType NodeType = NotationNodeType.BasicStat;

        [Tooltip("Human-readable description of what this notation represents")]
        public string Description = "Basic chess notation step.";

        [Min(0)]
        [Tooltip("TP consumed when this node is unlocked.")]
        public int TPCost = 1;

        [Tooltip("Nodes that must be unlocked first.")]
        public List<NotationNodeData> Prerequisites = new();

        [Tooltip("Stat bonuses granted by this node.")]
        public List<StatModifierEntry> Bonuses = new();
    }
}
