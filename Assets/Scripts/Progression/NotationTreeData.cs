using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Notation Tree", fileName = "NotationTree")]
    public class NotationTreeData : ScriptableObject
    {
        [Tooltip("Target operator unit id (e.g., Kiara, Elena, Vesta)")]
        public string TargetUnitId = "Kiara";

        [Tooltip("Title of this tactical chess notation (e.g., Queen's Gambit, Sicilian Defense)")]
        public string NotationTitle = "Queen's Gambit";

        [Tooltip("Base TP provided to this notation tree without resonance bonuses. Designed to be intentionally 3~4 TP short of total completion!")]
        public int BaseNotationTP = 10;

        public NotationNodeData RootNode;
        public List<NotationNodeData> Nodes = new();
    }
}
