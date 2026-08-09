using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CheckmateRPG.Data;
using CheckmateRPG.Progression;

namespace CheckmateRPG.UI
{
    public class RecruitUI : OutgameViewBase
    {
        [Header("UI References")]
        [SerializeField] private Button recruit1TimeButton;
        [SerializeField] private Button recruit10TimesButton;
        [SerializeField] private Button backToLobbyButton;

        [Header("Gacha Configuration")]
        [SerializeField] private CheckmateRPG.Data.GachaTableData gachaTable;
        [SerializeField] private RecruitResultUI resultUI;

        private void Awake()
        {
            if (recruit1TimeButton) recruit1TimeButton.onClick.AddListener(() => DoRecruit(1));
            if (recruit10TimesButton) recruit10TimesButton.onClick.AddListener(() => DoRecruit(10));
            
            if (backToLobbyButton) backToLobbyButton.onClick.AddListener(() => OutgameUIManager.Instance.ChangeView(OutgameViewType.MainLobby));
        }

        private void DoRecruit(int count)
        {
            if (gachaTable == null || gachaTable.Drops.Count == 0)
            {
                Debug.LogWarning("[RecruitUI] Cannot recruit: GachaTable is not assigned or empty.");
                return;
            }

            Debug.Log($"[RecruitUI] {count}회 모집 실행!");
            
            List<UnitData> pulledList = new List<UnitData>();

            for (int i = 0; i < count; i++)
            {
                var pulledUnit = gachaTable.PullRandom();
                if (pulledUnit != null)
                {
                    ResonanceSystem.AddUnitResonance(pulledUnit.name);
                    pulledList.Add(pulledUnit);
                }
                else
                {
                    Debug.LogWarning("[RecruitUI] Failed to pull a valid unit from GachaTable.");
                }
            }

            if (resultUI != null && pulledList.Count > 0)
            {
                resultUI.ShowResults(pulledList);
            }
        }
    }
}
