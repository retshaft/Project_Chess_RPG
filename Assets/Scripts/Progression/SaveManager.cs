using System;
using System.IO;
using UnityEngine;

namespace CheckmateRPG.Progression
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public SaveData CurrentData { get; private set; }
        public event Action OnDataChanged;

        private void Start() => InvokeRepeating(nameof(RecoverSanity), 1f, 30f);
        private void RecoverSanity()
        {
            if (CurrentData != null && EconomyService.Recover(CurrentData, DateTime.UtcNow)) SaveGame();
        }
        private void OnApplicationFocus(bool focused) { if (focused) RecoverSanity(); }

        private string SavePath => Path.Combine(Application.persistentDataPath, "saveData.json");

        // 백업·임시·손상 보존 파일은 본체와 같은 폴더에 둔다(형식은 기존 JSON과 동일 — 호환 유지).
        private string BackupPath => SavePath + ".bak";
        private string TempPath => SavePath + ".tmp";

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
            CurrentData = LoadSaveWithRecovery(SavePath, BackupPath);
            if (CurrentData != null)
            {
                // A battle cannot resume across a process restart. Its entry cost remains spent.
                EconomyService.EndSortie(CurrentData);
                SaveGame();
                return;
            }

            // 파일이 없거나 본체·백업이 모두 복구 불가인 경우에만 신규 데이터다.
            // 본체가 손상됐다면 LoadSaveWithRecovery가 이미 .corrupt로 보존했으므로 덮어써도 안전하다.
            Debug.Log("[SaveManager] No recoverable save data. Creating new.");
            CurrentData = new SaveData();
            EconomyService.Migrate(CurrentData, DateTime.UtcNow);
            SaveGame();
        }

        public void SaveGame()
        {
            if (CurrentData == null) return;

            try
            {
                WriteSaveAtomic(SavePath, BackupPath, TempPath, CurrentData);
                OnDataChanged?.Invoke();
                Debug.Log($"[SaveManager] Saved data to {SavePath}");
            }
            catch (Exception e)
            {
                // 실패 시 기존 파일과 백업이 그대로 유지된다 — 다음 저장이 재시도한다.
                Debug.LogError($"[SaveManager] Failed to save data: {e.Message}");
            }
        }

        public void ResetSave()
        {
            CurrentData = new SaveData();
            EconomyService.Migrate(CurrentData, DateTime.UtcNow);
            SaveGame();
            Debug.Log("[SaveManager] Save data reset.");
        }

        // ── 저장·복구 코어 (경로 주입 — EditMode 테스트 가능, 싱글턴 불필요) ──

        /// <summary>
        /// 임시 파일에 온전히 기록한 뒤 원자적으로 본체를 교체하고, 교체된 이전 본체를 백업으로 남긴다.
        /// 쓰기 도중 실패해도 기존 본체는 깨지지 않는다.
        /// </summary>
        internal static void WriteSaveAtomic(string path, string backupPath, string tempPath, SaveData data)
        {
            if (data == null) return;

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(tempPath, json);

            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                // 원자적 교체: temp → path 전환과 동시에 이전 path가 backupPath로 밀려난다.
                File.Replace(tempPath, path, backupPath);
            }
            catch (Exception e) when (e is IOException || e is PlatformNotSupportedException)
            {
                // 폴백(Replace 불가 파일시스템 대비): 백업 복사 후 덮어쓰기 이동.
                File.Copy(path, backupPath, true);
                File.Delete(path);
                File.Move(tempPath, path);
            }
        }

        /// <summary>
        /// 본체 → 백업 순으로 읽고, 본체가 읽기 불가능하면 .corrupt로 보존한 뒤 백업에서 복구한다.
        /// 둘 다 없거나 복구 불가면 null을 반환한다(호출자가 신규 데이터 생성을 판단).
        /// 손상 파일을 절대 덮어쓰지 않는다 — 무음 리셋 방지.
        /// </summary>
        internal static SaveData LoadSaveWithRecovery(string path, string backupPath)
        {
            SaveData main = TryReadSave(path);
            if (main != null)
                return main;

            if (File.Exists(path))
                PreserveCorrupt(path, path + ".corrupt");

            SaveData backup = TryReadSave(backupPath);
            if (backup != null)
            {
                Debug.Log("[SaveManager] Main save unusable; recovered from backup.");
                return backup;
            }

            if (File.Exists(backupPath))
                PreserveCorrupt(backupPath, backupPath + ".corrupt");

            return null;
        }

        private static SaveData TryReadSave(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                // 형식 마커 검사 — JsonUtility가 잘못된 JSON에서 예외 대신 기본값 객체를
                // 돌려주는 환경에서도 "저장 파일이 아닌 내용"을 손상으로 판정하게 한다.
                if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"TacticalPoints\""))
                {
                    Debug.LogWarning($"[SaveManager] Save file is not a save: {Path.GetFileName(path)}");
                    return null;
                }

                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.KingProgression == null)
                {
                    Debug.LogWarning($"[SaveManager] Save file has unusable content: {Path.GetFileName(path)}");
                    return null;
                }
                // An economy-v1 save already contains real player balances. New fields are
                // additive, so only files without an economy marker use the prototype reset path.
                bool legacy = !json.Contains("\"EconomyVersion\"");
                if (legacy && !File.Exists(path + ".economy-v0.bak")) File.Copy(path, path + ".economy-v0.bak");
                EconomyService.Migrate(data, DateTime.UtcNow, legacy);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Save file unusable ({Path.GetFileName(path)}): {e.Message}");
                return null;
            }
        }

        private static void PreserveCorrupt(string source, string corruptPath)
        {
            try
            {
                File.Copy(source, corruptPath, true);
                Debug.LogWarning($"[SaveManager] Corrupt save preserved at {corruptPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Could not preserve corrupt save copy: {e.Message}");
            }
        }
    }
}
