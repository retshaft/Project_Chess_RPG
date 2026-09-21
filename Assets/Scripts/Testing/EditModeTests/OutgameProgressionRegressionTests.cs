using System.Collections.Generic;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    public sealed class OutgameProgressionRegressionTests
    {
        private readonly List<Object> _objects = new();
        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _objects.Add(asset);
            return asset;
        }

        [TearDown] public void Cleanup()
        {
            foreach (var obj in _objects) Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        [TestCase(13, PolarityType.Order, 7)]
        [TestCase(15, PolarityType.Order, 8)]
        [TestCase(15, PolarityType.Chaos, 18)]
        [TestCase(15, PolarityType.Neutral, 15)]
        public void SlotCostUsesSlotPolarityAndHalfUpRounding(int cost, PolarityType slot, int expected)
        {
            var edict = Asset<EdictData>();
            edict.Polarity = PolarityType.Order;
            edict.SyncCost = cost;
            Assert.That(PolarityCalculator.CalculateSlotCost(edict, slot), Is.EqualTo(expected));
        }

        [Test] public void ZeroBonusAbsoluteDoesNotInventCapacity()
        {
            var absolute = Asset<EdictData>();
            absolute.Kind = EdictKind.Absolute;
            absolute.SyncCost = 0;
            Assert.That(PolarityCalculator.CalculateMaxSyncCapacity(10, 5, absolute, PolarityType.Order), Is.EqualTo(15));
        }

        [Test] public void KingLoadoutRetainsEmptySlotPolarityAndAppliesValidatedDiscountOnce()
        {
            var capacity = Asset<SyncCapacityData>();
            capacity.BaseCapacity = 0;
            var absolute = Asset<EdictData>();
            absolute.Kind = EdictKind.Absolute;
            absolute.Polarity = PolarityType.Chaos;
            absolute.SyncCost = 1;
            var general = Asset<EdictData>();
            general.Polarity = PolarityType.Order;
            general.SyncCost = 10;
            general.Bonuses.Add(new StatModifierEntry { Stat = MetaStatType.AttackDamage, FlatBonus = 7 });
            var save = new KingProgressionSaveData { PlayTP = 3,
                SlotPolarities = new List<PolarityType> { PolarityType.Chaos, PolarityType.Chaos, PolarityType.Order } };
            var loadout = MetaProgressionBuilder.BuildKingLoadout(save, capacity, absolute,
                new List<EdictData> { null, general });
            Assert.That(loadout.ActiveEdicts, Does.Contain(general));
            Assert.That(loadout.BonusSyncCapacity, Is.EqualTo(5));
            var unit = Asset<UnitData>();
            unit.AttackDamage = 10;
            var modified = MetaProgressionCalculator.CreateModifiedUnitData(unit, loadout);
            _objects.Add(modified);
            Assert.That(modified.AttackDamage, Is.EqualTo(17));
            Assert.That(unit.AttackDamage, Is.EqualTo(10));
        }

        [Test] public void OldSaveGeneralSlotDefaultsToNeutral()
        {
            var save = new KingProgressionSaveData();
            Assert.That(PolarityCalculator.GetSlotPolarity(save, 0, PolarityType.Order), Is.EqualTo(PolarityType.Order));
            Assert.That(PolarityCalculator.GetSlotPolarity(save, 1, PolarityType.Order), Is.EqualTo(PolarityType.Neutral));
        }

        [Test] public void InventoryBlocksEnemyEvenIfOwnedAndPreservesEmptyInventoryStarter()
        {
            var player = Asset<UnitData>();
            player.name = "StarterPawn";
            player.PieceType = ChessPieceType.Pawn;
            var enemy = Asset<UnitData>();
            enemy.name = "Enemy_BlackPawn";
            var save = new SaveData();
            Assert.That(DeckManager.IsAvailableToPlayer(player, save), Is.True);
            save.UnlockedUnits.Add(enemy.name);
            Assert.That(DeckManager.IsAvailableToPlayer(enemy, save), Is.False);
            Assert.That(DeckManager.IsAvailableToPlayer(player, save), Is.False);
            Assert.That(save.UnlockedUnits.Count, Is.EqualTo(1));
            player.AIProfile = Asset<EnemyAIProfile>();
            save.UnlockedUnits.Add(player.name);
            Assert.That(DeckManager.IsAvailableToPlayer(player, save), Is.False);
        }

        [Test] public void PopulatedInventoryAllowsOwnedAndLegalLegacyDeckButNotUnknownUnit()
        {
            var owned = Asset<UnitData>();
            owned.name = "Owned";
            owned.PieceType = ChessPieceType.Pawn;
            var legacy = Asset<UnitData>();
            legacy.name = "Legacy";
            legacy.PieceType = ChessPieceType.Knight;
            var unknown = Asset<UnitData>();
            unknown.name = "Unknown";
            unknown.PieceType = ChessPieceType.Bishop;
            var king = Asset<UnitData>();
            king.name = "PlayerKing";
            king.PieceType = ChessPieceType.King;
            var save = new SaveData { UnlockedUnits = new List<string> { owned.name },
                CurrentDeck = new List<string> { unknown.name, legacy.name } };
            Assert.That(DeckManager.IsAvailableToPlayer(owned, save), Is.True);
            Assert.That(DeckManager.IsAvailableToPlayer(legacy, save), Is.True);
            Assert.That(DeckManager.IsAvailableToPlayer(unknown, save), Is.False);
            Assert.That(DeckManager.IsAvailableToPlayer(king, save), Is.True);
        }

        [Test] public void ProgressionClonePreservesSkillsAndAssetIdentityWithoutMutatingOriginal()
        {
            var unit = Asset<UnitData>();
            unit.name = "OriginalOperator";
            unit.AttackDamage = 10;
            unit.SelectedSkillIndex = 1;
            var first = Asset<AbilityDefinition>();
            var second = Asset<AbilityDefinition>();
            unit.BasicAttackAbility = first;
            unit.Abilities.Add(first);
            unit.SelectableActiveSkills.Add(first);
            unit.SelectableActiveSkills.Add(second);
            var modified = MetaProgressionCalculator.CreateModifiedUnitData(unit, new MetaProgressionLoadout());
            _objects.Add(modified);
            Assert.That(modified.name, Is.EqualTo(unit.name));
            Assert.That(modified.BasicAttackAbility, Is.SameAs(first));
            Assert.That(modified.GetSelectedActiveSkill(), Is.SameAs(second));
            modified.SelectableActiveSkills.Clear();
            modified.AttackDamage = 99;
            Assert.That(unit.SelectableActiveSkills.Count, Is.EqualTo(2));
            Assert.That(unit.AttackDamage, Is.EqualTo(10));
        }
    }
}
