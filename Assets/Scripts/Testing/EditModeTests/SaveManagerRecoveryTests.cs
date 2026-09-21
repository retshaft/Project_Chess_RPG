using System;
using System.IO;
using CheckmateRPG.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    /// <summary>
    /// R01b 회귀: SaveManager의 원자적 저장·백업·복구 코어(경로 주입 정적 함수)를 검증한다.
    /// SaveManager 싱글턴은 띄우지 않고 임시 폴더의 실제 파일 I/O로만 확인한다.
    /// </summary>
    [TestFixture]
    public sealed class SaveManagerRecoveryTests
    {
        private string _dir;
        private string _main;
        private string _backup;
        private string _temp;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "SaveManagerRecoveryTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _main = Path.Combine(_dir, "saveData.json");
            _backup = _main + ".bak";
            _temp = _main + ".tmp";
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }

        private static SaveData MakeData(int tacticalPoints, int suitLevel = 1)
        {
            var data = new SaveData();
            data.TacticalPoints = tacticalPoints;
            data.KingProgression.SuitLevel = suitLevel;
            return data;
        }

        [Test]
        public void WriteSaveAtomic_RoundTripsData_WithoutTempLeftover()
        {
            SaveManager.WriteSaveAtomic(_main, _backup, _temp, MakeData(120, 3));

            Assert.IsTrue(File.Exists(_main), "본체가 생성된다.");
            Assert.IsFalse(File.Exists(_temp), "임시 파일은 교체 후 남지 않는다.");

            SaveData loaded = SaveManager.LoadSaveWithRecovery(_main, _backup);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(120, loaded.TacticalPoints);
            Assert.AreEqual(3, loaded.KingProgression.SuitLevel);
        }

        [Test]
        public void WriteSaveAtomic_SecondSave_PushesPreviousSaveToBackup()
        {
            SaveManager.WriteSaveAtomic(_main, _backup, _temp, MakeData(50));
            SaveManager.WriteSaveAtomic(_main, _backup, _temp, MakeData(150));

            Assert.IsTrue(File.Exists(_backup), "두 번째 저장 시 이전 본체가 백업이 된다.");

            SaveData mainData = JsonUtility.FromJson<SaveData>(File.ReadAllText(_main));
            SaveData backupData = JsonUtility.FromJson<SaveData>(File.ReadAllText(_backup));
            Assert.AreEqual(150, mainData.TacticalPoints, "본체는 최신 저장이다.");
            Assert.AreEqual(50, backupData.TacticalPoints, "백업은 직전 저장이다.");
        }

        [Test]
        public void WriteSaveAtomic_FirstSave_CreatesMainWithoutBackup()
        {
            SaveManager.WriteSaveAtomic(_main, _backup, _temp, MakeData(10));

            Assert.IsTrue(File.Exists(_main));
            Assert.IsFalse(File.Exists(_backup), "교체된 이전 본체가 없으면 백업도 만들지 않는다.");
        }

        [Test]
        public void Load_FallsBackToBackup_WhenMainCorrupt_AndPreservesCorruptMain()
        {
            SaveManager.WriteSaveAtomic(_main, _backup, _temp, MakeData(50));
            SaveManager.WriteSaveAtomic(_main, _backup, _temp, MakeData(150));
            File.WriteAllText(_main, "this is not json");

            SaveData recovered = SaveManager.LoadSaveWithRecovery(_main, _backup);

            Assert.IsNotNull(recovered, "백업이 살아 있으면 복구에 성공한다.");
            Assert.AreEqual(50, recovered.TacticalPoints, "복구 원본은 백업(직전 저장)이다.");
            Assert.IsTrue(File.Exists(_main + ".corrupt"), "손상된 본체는 .corrupt로 보존된다(무음 덮어쓰기 금지).");
        }

        [Test]
        public void Load_BothFilesUnusable_ReturnsNull_AndPreservesBoth()
        {
            SaveManager.WriteSaveAtomic(_main, _backup, _temp, MakeData(50));
            File.WriteAllText(_main, "garbage main");
            File.WriteAllText(_backup, "garbage backup");

            SaveData recovered = SaveManager.LoadSaveWithRecovery(_main, _backup);

            Assert.IsNull(recovered, "복구 불가 시 호출자가 신규 생성을 판단하도록 null을 반환한다.");
            Assert.IsTrue(File.Exists(_main + ".corrupt"), "손상 본체가 보존된다.");
            Assert.IsTrue(File.Exists(_backup + ".corrupt"), "손상 백업이 보존된다.");
        }

        [Test]
        public void Load_NoFiles_ReturnsNull_WithoutCorruptFiles()
        {
            SaveData recovered = SaveManager.LoadSaveWithRecovery(_main, _backup);

            Assert.IsNull(recovered);
            Assert.IsFalse(File.Exists(_main + ".corrupt"), "파일이 없는데 .corrupt를 만들지 않는다.");
            Assert.IsFalse(File.Exists(_backup + ".corrupt"));
        }

        [Test]
        public void Load_ValidMain_IgnoresBackup_AndKeepsItIntact()
        {
            SaveManager.WriteSaveAtomic(_main, _backup, _temp, MakeData(30));
            SaveManager.WriteSaveAtomic(_main, _backup, _temp, MakeData(70));

            SaveData loaded = SaveManager.LoadSaveWithRecovery(_main, _backup);

            Assert.AreEqual(70, loaded.TacticalPoints, "본체가 정상이면 백업을 읽지 않는다.");
            Assert.IsFalse(File.Exists(_main + ".corrupt"), "정상 본체를 손상 파일로 보존하지 않는다.");
            Assert.IsTrue(File.Exists(_backup), "정상 로드는 백업을 건드리지 않는다.");
        }
    }
}
