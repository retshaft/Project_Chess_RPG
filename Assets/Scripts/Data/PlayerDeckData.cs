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
        [HideInInspector] public int MaxCostCap = 20; // Legacy serialized field; the shared formation rule owns the cap.

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
                    total += unit.FormationCost;
                }
            }
            return total;
        }

        /// <summary>
        /// Returns true if the deck's total cost is within the allowed cap.
        /// </summary>
        public bool IsWithinCostCap()
        {
            return GetTotalCost() <= CheckmateRPG.Progression.DeckManager.MAX_FORMATION_COST;
        }

        private void OnValidate()
        {
            if (!IsWithinCostCap())
            {
                Debug.LogWarning($"[PlayerDeckData] '{name}' exceeds formation budget! Total: {GetTotalCost()} / {CheckmateRPG.Progression.DeckManager.MAX_FORMATION_COST}");
            }
        }
    }
}
