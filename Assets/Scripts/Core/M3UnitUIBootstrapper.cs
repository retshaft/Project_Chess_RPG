using UnityEngine;
using CheckmateRPG.Units;
using CheckmateRPG.UI;

namespace CheckmateRPG.Core
{
    public class M3UnitUIBootstrapper : MonoBehaviour
    {
        private void Update()
        {
            // Naive approach: find all UnitBrains in the scene and ensure they have UI.
            // In a real game, this would be hooked into a spawn event.
            var units = FindObjectsByType<UnitBrain>(FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                if (unit == null || unit.IsDead) continue;

                if (unit.GetComponentInChildren<UnitStatusBar>() == null)
                {
                    var statusBarGo = new GameObject("UnitStatusBar");
                    statusBarGo.transform.SetParent(unit.transform, false);
                    var statusBar = statusBarGo.AddComponent<UnitStatusBar>();
                    statusBar.Initialize(unit.transform);
                }

                if (unit.GetComponentInChildren<StatusReadabilityUI>() == null)
                {
                    var readGo = new GameObject("StatusReadabilityUI");
                    readGo.transform.SetParent(unit.transform, false);
                    var readUI = readGo.AddComponent<StatusReadabilityUI>();
                    readUI.Initialize(unit.transform);
                }

                if (unit.GetComponent<CheckmateRPG.Components.CCVisualOverlay>() == null)
                {
                    unit.gameObject.AddComponent<CheckmateRPG.Components.CCVisualOverlay>();
                }
            }
        }
    }
}
