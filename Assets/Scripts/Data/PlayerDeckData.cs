// PlayerDeckData.cs
// Holds a player's customized deck of units for deployment.

using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Data
{
    [CreateAssetMenu(menuName = "CheckmateRPG/Player Deck Data", fileName = "NewPlayerDeck")]
    public class PlayerDeckData : ScriptableObject
    {
        [Header("Deck Configuration")]
        [Tooltip("The maximum allowed deployment cost for this deck.")]
        [Min(1)] public int MaxCostCap = 15;

        [Tooltip("The list of units included in this deck.")]
        public List<UnitData> Roster = new List<UnitData>();

        /// <summary>
        /// Calculates the total deployment cost of the current roster.
        /// </summary>
        public int GetTotalCost()
        {
            int total = 0;
            if (Roster == null) return 0;
            
            foreach (var unit in Roster)
            {
                if (unit != null)
                {
                    total += unit.DeploymentCost;
                }
            }
            return total;
        }

        /// <summary>
        /// Returns true if the deck's total cost is within the allowed cap.
        /// </summary>
        public bool IsWithinCostCap()
        {
            return GetTotalCost() <= MaxCostCap;
        }

        private void OnValidate()
        {
            if (!IsWithinCostCap())
            {
                Debug.LogWarning($"[PlayerDeckData] '{name}' exceeds MaxCostCap! Total: {GetTotalCost()} / {MaxCostCap}");
            }
        }
    }
}
