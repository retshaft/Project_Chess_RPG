using System;
using System.Reflection;
using CheckmateRPG.Data;
using CheckmateRPG.Progression;
using CheckmateRPG.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    /// <summary>
    /// 아웃게임 규칙 회귀(작업서 outgame-ux-main — 내부 edict 작업자 범위).
    /// 외부에서 관찰 가능한 규칙만 검증한다: 보유 유닛 게이팅, 칙령 극성 비용/수용량,
    /// 메타 성장 복제의 원본 보존, 칙령 화면의 런타임 자기 구성.
    /// SaveManager 싱글턴(실제 저장 파일 I/O)을 건드리지 않는 규칙만 다룬다.
    /// </summary>
    [TestFixture]
    public sealed class OutgameRuleRegressionTests
    {
        // ── 덱 게이팅: 적 유닛·AI 전용 UnitData는 플레이어 덱에 들어갈 수 없다 ──

        private static UnitData MakeUnit(string assetName, ChessPieceType piece, EnemyAIProfile aiProfile = null)
        {
            var unit = ScriptableObject.CreateInstance<UnitData>();
            unit.name = assetName;
            unit.PieceType = piece;
            unit.AIProfile = aiProfile;
            return unit;
        }

        private static SaveData MakeSave(params string[] unlockedUnits)
        {
            var save = new SaveData();
            foreach (string id in unlockedUnits)
                save.UnlockedUnits.Add(id);
            return save;
        }

        [Test]
        public void IsAvailableToPlayer_RejectsEnemyNamedUnit_EvenIfUnlocked()
        {
            var enemy = MakeUnit("Enemy_BlackPawn", ChessPieceType.Pawn);
            var save = MakeSave("Enemy_BlackPawn");

            Assert.IsFalse(DeckManager.IsPlayerUnit(enemy), "Enemy_ 접두 유닛은 플레이어 유닛이 아니다.");
            Assert.IsFalse(DeckManager.IsAvailableToPlayer(enemy, save), "해금 목록에 있어도 적 유닛은 사용할 수 없다.");
        }

        [Test]
        public void IsAvailableToPlayer_RejectsAIProfileUnit()
        {
            var aiUnit = MakeUnit("AI_Dummy", ChessPieceType.Pawn, ScriptableObject.CreateInstance<EnemyAIProfile>());

            Assert.IsFalse(DeckManager.IsPlayerUnit(aiUnit), "AI 전용 프로필을 가진 유닛은 플레이어 유닛이 아니다.");
        }

        [Test]
        public void IsAvailableToPlayer_KingAlwaysUsable_AndStarterUnitsWhenInventoryEmpty()
        {
            var king = MakeUnit("PlayerKing", ChessPieceType.King);
            var starter = MakeUnit("Kiara", ChessPieceType.Queen);

            Assert.IsTrue(DeckManager.IsAvailableToPlayer(king, MakeSave()), "킹은 해금 여부와 무관하게 사용 가능하다.");
            Assert.IsTrue(DeckManager.IsAvailableToPlayer(starter, MakeSave()), "보유 목록이 비어 있으면 초기 오퍼레이터를 사용할 수 있다.");
        }

        [Test]
        public void IsAvailableToPlayer_LockedUnitRequiresUnlockOrDeckEntry()
        {
            var locked = MakeUnit("Vesta", ChessPieceType.Bishop);
            var notOwned = MakeSave("Kiara");
            var owned = MakeSave("Kiara", "Vesta");
            var inDeck = MakeSave("Kiara");
            for (int i = 0; i < 8; i++)
                inDeck.CurrentDeck.Add("");
            inDeck.CurrentDeck[3] = "Vesta";

            Assert.IsFalse(DeckManager.IsAvailableToPlayer(locked, notOwned), "미보유 오퍼레이터는 사용할 수 없다.");
            Assert.IsTrue(DeckManager.IsAvailableToPlayer(locked, owned), "해금된 오퍼레이터는 사용할 수 있다.");
            Assert.IsTrue(DeckManager.IsAvailableToPlayer(locked, inDeck), "현재 덱에 편성된 오퍼레이터는 사용할 수 있다.");
        }

        // ── 칙령 극성 비용·수용량(GDD Part 4 §1~3 공식) ──

        private static EdictData MakeEdict(EdictKind kind, PolarityType polarity, int cost)
        {
            var edict = ScriptableObject.CreateInstance<EdictData>();
            edict.EdictName = "TestEdict";
            edict.Kind = kind;
            edict.Polarity = polarity;
            edict.SyncCost = cost;
            return edict;
        }

        [Test]
        public void SlotCost_MatchHalved_MismatchUp20_NeutralFull()
        {
            var order = MakeEdict(EdictKind.General, PolarityType.Order, 7);
            var neutral = MakeEdict(EdictKind.General, PolarityType.Neutral, 7);

            Assert.AreEqual(4, PolarityCalculator.CalculateSlotCost(order, PolarityType.Order), "극성 일치 시 비용 절반(7→4, 반올림).");
            Assert.AreEqual(8, PolarityCalculator.CalculateSlotCost(order, PolarityType.Chaos), "극성 불일치 시 비용 20% 증가(7→8).");
            Assert.AreEqual(7, PolarityCalculator.CalculateSlotCost(neutral, PolarityType.Order), "중립 카드는 슬롯 극성과 무관하게 정가.");
        }

        [Test]
        public void MaxSyncCapacity_BasePlusTP_PlusAbsoluteBonus_DoubledOnPolarityMatch()
        {
            var absoluteOrder = MakeEdict(EdictKind.Absolute, PolarityType.Order, 5);

            Assert.AreEqual(7, PolarityCalculator.CalculateMaxSyncCapacity(2, 5, null, PolarityType.Neutral),
                "절대 칙령이 없으면 기본 수용량 + 플레이 TP.");
            Assert.AreEqual(12, PolarityCalculator.CalculateMaxSyncCapacity(2, 5, absoluteOrder, PolarityType.Neutral),
                "극성 불일치 절대 칙령은 보너스 1배(2+5+5).");
            Assert.AreEqual(17, PolarityCalculator.CalculateMaxSyncCapacity(2, 5, absoluteOrder, PolarityType.Order),
                "극성 일치 절대 칙령은 보너스 2배(2+5+10).");
        }

        [Test]
        public void GetSlotPolarity_UsesSave_ThenFallsBack()
        {
            var save = new SaveData();
            save.KingProgression.SlotPolarities.Add(PolarityType.Chaos); // slot 0만 지정

            Assert.AreEqual(PolarityType.Chaos, PolarityCalculator.GetSlotPolarity(save.KingProgression, 0, PolarityType.Order));
            Assert.AreEqual(PolarityType.Neutral, PolarityCalculator.GetSlotPolarity(save.KingProgression, 1, PolarityType.Order), "기록 없는 일반 슬롯은 중립.");
            Assert.AreEqual(PolarityType.Neutral, PolarityCalculator.GetSlotPolarity(null, 1, PolarityType.Order), "세이브가 없으면 중립 폴백.");
        }

        [Test]
        public void SyncCapacityState_RejectsEdictOverCapacity()
        {
            var state = new SyncCapacityState(6);
            var cheap = MakeEdict(EdictKind.General, PolarityType.Neutral, 4);
            var expensive = MakeEdict(EdictKind.General, PolarityType.Neutral, 4);

            Assert.IsTrue(state.TryAssign(cheap), "첫 칙령(비용 4)은 수용량 6 안에서 장착된다.");
            Assert.IsFalse(state.TryAssign(expensive), "잔여 수용량 2로는 비용 4 칙령을 장착할 수 없다.");
        }

        // ── 메타 성장 복제: 원본 UnitData를 훼손하지 않는다 ──

        [Test]
        public void CreateModifiedUnitData_DoesNotMutateSource()
        {
            var source = MakeUnit("Kiara", ChessPieceType.Queen);
            source.MaxHealth = 100f;
            source.AttackDamage = 20f;

            var node = ScriptableObject.CreateInstance<NotationNodeData>();
            node.NodeId = "N-test";
            node.Bonuses.Add(new StatModifierEntry { Stat = MetaStatType.MaxHealth, FlatBonus = 50f });

            var loadout = new MetaProgressionLoadout();
            loadout.UnlockedNotationNodes.Add(node);

            UnitData modified = MetaProgressionCalculator.CreateModifiedUnitData(source, loadout, "_Modified");

            Assert.IsNotNull(modified);
            Assert.AreNotSame(source, modified, "성장 결과는 복제본이다.");
            Assert.AreEqual(150f, modified.MaxHealth, "기보 보너스가 복제본에 반영된다.");
            Assert.AreEqual(100f, source.MaxHealth, "원본 UnitData는 훼손되지 않는다.");
            Assert.AreEqual(20f, source.AttackDamage, "원본의 다른 스탯도 불변이다.");

            UnityEngine.Object.DestroyImmediate(node);
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(modified);
        }

        // ── 칙령 화면 런타임 자기 구성(P0 — 씬 참조가 비어도 스스로 구성된다) ──
        // 비고: 공명 단계 효과(초기 SP 등)는 사용자 결정(2026-09-11)에 따라 미구현 유지 —
        // 단계별 내용이 부정확하다는 지적으로, 정확한 기획 확정 전까지 연결하지 않는다.

        [Test]
        public void SynchroBoardUI_SelfConfigures_WhenSceneReferencesMissing()
        {
            var go = new GameObject("SynchroBoardView");
            var board = go.AddComponent<SynchroBoardUI>();

            // EditMode에서는 Awake가 실행되지 않으므로(ExecuteAlways 미사용) 구성 메서드를 직접 호출한다.
            // 플레이 중에는 Awake가 이 메서드를 호출한다.
            typeof(SynchroBoardUI).GetMethod("EnsureBoardUI", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(board, null);

            Assert.IsNotNull(board.GetComponentInChildren<TextMeshProUGUI>(true), "수용량/슬롯 텍스트가 자동 생성된다.");

            var buttons = go.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            Assert.GreaterOrEqual(buttons.Length, 4, "절대 슬롯 1 + 일반 슬롯 3 + 닫기 버튼이 존재한다.");

            var modalField = typeof(SynchroBoardUI).GetField("selectorModal", BindingFlags.NonPublic | BindingFlags.Instance);
            var modal = modalField?.GetValue(board) as EdictSelectorModal;
            Assert.IsNotNull(modal, "선택 모달이 런타임에 생성되어 연결된다.");

            var panelField = typeof(EdictSelectorModal).GetField("modalPanel", BindingFlags.NonPublic | BindingFlags.Instance);
            var panel = panelField?.GetValue(modal) as GameObject;
            Assert.IsNotNull(panel, "모달 패널이 자기 구성된다.");
            Assert.IsFalse(panel.activeSelf, "모달은 생성 직후 숨겨진다.");

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void SynchroBoardUI_LoadAndDisplay_SurviveWithoutSave()
        {
            var go = new GameObject("SynchroBoardView");
            var board = go.AddComponent<SynchroBoardUI>();
            typeof(SynchroBoardUI).GetMethod("EnsureBoardUI", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(board, null);

            // SaveManager 싱글턴 없이 Show 경로의 핵심 단계가 예외 없이 동작해야 한다.
            Assert.DoesNotThrow(() =>
            {
                typeof(SynchroBoardUI).GetMethod("LoadBoardFromSave", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(board, null);
                typeof(SynchroBoardUI).GetMethod("UpdateBoardDisplay", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(board, null);
            }, "세이브가 없어도 칙령 화면 표시는 안전해야 한다.");

            var meter = go.GetComponentInChildren<TextMeshProUGUI>(true);
            Assert.IsNotNull(meter, "수용량 미터가 자동 생성된다.");
            Assert.IsTrue(meter.text.Contains("수용량"), "수용량 미터가 표시된다.");

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
