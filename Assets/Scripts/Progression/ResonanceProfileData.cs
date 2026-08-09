using System.Collections.Generic;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    [CreateAssetMenu(menuName = "CheckmateRPG/Progression/Resonance Profile", fileName = "ResonanceProfile")]
    public class ResonanceProfileData : ScriptableObject
    {
        public List<ResonanceStageBonus> StageBonuses = new();

        public IEnumerable<StatModifierEntry> GetBonusesUpToStage(int stage)
        {
            if (StageBonuses == null)
                yield break;

            for (int i = 0; i < StageBonuses.Count; i++)
            {
                ResonanceStageBonus entry = StageBonuses[i];
                if (entry == null || entry.Bonuses == null || entry.Stage > stage)
                    continue;

                for (int j = 0; j < entry.Bonuses.Count; j++)
                    yield return entry.Bonuses[j];
            }
        }
    }
}
