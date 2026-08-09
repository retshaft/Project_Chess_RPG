using System;
using System.IO;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }
        
        public SaveData CurrentData { get; private set; }

        private string SavePath => Path.Combine(Application.persistentDataPath, "saveData.json");

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadGame();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static SaveManager EnsureInstance()
        {
            if (Instance != null)
            {
                if (Instance.CurrentData == null) Instance.LoadGame();
                return Instance;
            }
            var existing = UnityEngine.Object.FindFirstObjectByType<SaveManager>();
            if (existing != null)
            {
                Instance = existing;
                if (Instance.CurrentData == null) Instance.LoadGame();
                return Instance;
            }
            var smGo = new GameObject("SaveManager");
            Instance = smGo.AddComponent<SaveManager>();
            if (Instance.CurrentData == null) Instance.LoadGame();
            return Instance;
        }

        public void LoadGame()
        {
            if (File.Exists(SavePath))
            {
                try
                {
                    string json = File.ReadAllText(SavePath);
                    CurrentData = JsonUtility.FromJson<SaveData>(json);
                    Debug.Log($"[SaveManager] Loaded save data from {SavePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveManager] Failed to load save data: {e.Message}");
                    CurrentData = new SaveData();
                }
            }
            else
            {
                Debug.Log("[SaveManager] No save data found. Creating new.");
                CurrentData = new SaveData();
                SaveGame();
            }
        }

        public void SaveGame()
        {
            if (CurrentData == null) return;

            try
            {
                string json = JsonUtility.ToJson(CurrentData, true);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[SaveManager] Saved data to {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to save data: {e.Message}");
            }
        }

        public void ResetSave()
        {
            CurrentData = new SaveData();
            SaveGame();
            Debug.Log("[SaveManager] Save data reset.");
        }
    }
}
