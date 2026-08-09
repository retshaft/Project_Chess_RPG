using UnityEngine;

namespace CheckmateRPG.Progression
{
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        public StageData CurrentStage { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetCurrentStage(StageData stageData)
        {
            CurrentStage = stageData;
            Debug.Log($"[StageManager] Current Stage set to: {stageData?.StageName}");
        }
    }
}
