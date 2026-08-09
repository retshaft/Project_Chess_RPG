#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CheckmateRPG.Core;
using CheckmateRPG.Core.StatModifiers;

namespace CheckmateRPG.Editor
{
    public static class KiaraAssetGenerator
    {
        [MenuItem("CheckmateRPG/Generate Kiara Assets")]
        public static void Generate()
        {
            // ─── StatModifierProfileSO ───
            CreateProfile("KiaraActive1_Buff", "KiaraActive1_Buff", new[]
            {
                new StatModifierEntry { Type = StatModifierType.AttackDamageMultiplier, Value = 1.5f, ConsumptionPolicy = ModifierConsumptionPolicy.ConsumeOnAttack },
                new StatModifierEntry { Type = StatModifierType.AttackCountOverride, Value = 3f, ConsumptionPolicy = ModifierConsumptionPolicy.ConsumeOnAttack }
            });

            CreateProfile("KiaraActive2_Buff", "KiaraActive2_Buff", new[]
            {
                new StatModifierEntry { Type = StatModifierType.APCostFlat, Value = -4f, ConsumptionPolicy = ModifierConsumptionPolicy.ConsumeOnAction },
                new StatModifierEntry { Type = StatModifierType.AttackDamageMultiplier, Value = 1.3f, ConsumptionPolicy = ModifierConsumptionPolicy.ConsumeOnAction }
            });

            CreateProfile("KiaraActive3_Buff", "KiaraActive3_Buff", new[]
            {
                new StatModifierEntry { Type = StatModifierType.AttackCooldownMultiplier, Value = 0.4f, ConsumptionPolicy = ModifierConsumptionPolicy.Duration },
                new StatModifierEntry { Type = StatModifierType.APCostMultiplier, Value = 0.5f, ConsumptionPolicy = ModifierConsumptionPolicy.Duration },
                new StatModifierEntry { Type = StatModifierType.AttackCountOverride, Value = 1f, ConsumptionPolicy = ModifierConsumptionPolicy.Duration },
                new StatModifierEntry { Type = StatModifierType.AttackDamageRatioOverride, Value = 0.7f, ConsumptionPolicy = ModifierConsumptionPolicy.Duration },
                new StatModifierEntry { Type = StatModifierType.OnHitApplyBleed, Value = 1f, ConsumptionPolicy = ModifierConsumptionPolicy.Duration } // 1 스택 부여
            });

            // ─── AbilityDefinition ───
            // 액티브 1
            var act1 = ScriptableObject.CreateInstance<AbilityDefinition>();
            act1.AbilityId = "KiaraActive1";
            var act1Level1 = new AbilityLevelData
            {
                Cost = 4f,
                Cooldown = 15,
                TargetingRule = AbilityTargetingRule.Self
            };
            act1Level1.Effects.Add(new CheckmateRPG.Core.Abilities.ApplyBuffAbilityEffect 
            { 
                StatModifierProfileId = "KiaraActive1_Buff", 
                ApplyToCaster = true 
            });
            act1.Levels.Add(act1Level1);
            SaveAsset(act1, "Assets/Resources/StatModifiers/KiaraActive1.asset");

            // 액티브 2
            var act2 = ScriptableObject.CreateInstance<AbilityDefinition>();
            act2.AbilityId = "KiaraActive2";
            var act2Level1 = new AbilityLevelData
            {
                Cost = 3f,
                Cooldown = 20,
                TargetingRule = AbilityTargetingRule.Self
            };
            act2Level1.Effects.Add(new CheckmateRPG.Core.Abilities.ApplyBuffAbilityEffect 
            { 
                StatModifierProfileId = "KiaraActive2_Buff", 
                ApplyToCaster = true 
            });
            act2.Levels.Add(act2Level1);
            SaveAsset(act2, "Assets/Resources/StatModifiers/KiaraActive2.asset");

            // 액티브 3
            var act3 = ScriptableObject.CreateInstance<AbilityDefinition>();
            act3.AbilityId = "KiaraActive3";
            var act3Level1 = new AbilityLevelData
            {
                Cost = 5f,
                SPCost = 100, // 궁극기
                Cooldown = 30,
                TargetingRule = AbilityTargetingRule.SingleTarget // 타겟 중심 범위 공격을 위해 타겟 지정
            };
            act3Level1.Effects.Add(new CheckmateRPG.Core.Abilities.DealAreaDamageAbilityEffect 
            { 
                Shape = AreaTargetShape.TargetAroundCross, 
                DamageRatio = 0.15f 
            });
            act3Level1.Effects.Add(new CheckmateRPG.Core.Abilities.ApplyBuffAbilityEffect 
            { 
                StatModifierProfileId = "KiaraActive3_Buff", 
                ApplyToCaster = true 
            });
            act3.Levels.Add(act3Level1);
            SaveAsset(act3, "Assets/Resources/StatModifiers/KiaraActive3.asset");

            // ─── Passive ───
            // 패시브 1: 평타 3회당 출혈 부여 및 쿨다운 감소
            var passive1 = ScriptableObject.CreateInstance<CheckmateRPG.Core.Passives.PassiveDefinitionSO>();
            passive1.PassiveId = "KiaraPassive1_Bleed";
            var passive1Level1 = new CheckmateRPG.Core.Passives.PassiveLevelData
            {
                Trigger = CheckmateRPG.Core.Passives.PassiveTriggerType.OnDamageDealt,
                ConditionExpression = "HitCount == 3"
            };
            passive1Level1.Effects.Add(new CheckmateRPG.Core.Passives.ApplyStatusPassiveEffect { EffectId = "Bleed", DurationTicks = 2, StackCount = 1, Magnitude = 300f });
            passive1Level1.Effects.Add(new CheckmateRPG.Core.Passives.ReduceCooldownPassiveEffect { CooldownSeconds = 12f });
            passive1.Levels.Add(passive1Level1);
            SaveAsset(passive1, "Assets/Resources/StatModifiers/KiaraPassive1.asset");

            // 패시브 2: 대상이 출혈 시 방어 무시 15%
            CreateProfile("KiaraPassive_DefPen", "KiaraPassive_DefPen", new[]
            {
                new StatModifierEntry { Type = StatModifierType.DefPenetrationFlat, Value = 0.15f, ConsumptionPolicy = ModifierConsumptionPolicy.ConsumeOnAttack }
            });

            var passive2 = ScriptableObject.CreateInstance<CheckmateRPG.Core.Passives.PassiveDefinitionSO>();
            passive2.PassiveId = "KiaraPassive2_DefPen";
            var passive2Level1 = new CheckmateRPG.Core.Passives.PassiveLevelData
            {
                Trigger = CheckmateRPG.Core.Passives.PassiveTriggerType.OnAttackStart,
                ConditionExpression = "TargetIsBleeding"
            };
            passive2Level1.Effects.Add(new CheckmateRPG.Core.Passives.ApplyBuffPassiveEffect { StatModifierProfileId = "KiaraPassive_DefPen" });
            passive2.Levels.Add(passive2Level1);
            SaveAsset(passive2, "Assets/Resources/StatModifiers/KiaraPassive2.asset");

            // ─── UnitData (키아라) ───
            var kiaraUnit = ScriptableObject.CreateInstance<CheckmateRPG.Data.UnitData>();
            kiaraUnit.UnitName = "키아라 (Kiara)";
            kiaraUnit.PieceType = CheckmateRPG.Data.ChessPieceType.Knight;
            kiaraUnit.Subclass = CheckmateRPG.Data.UnitSubclassType.Swordmaster;
            kiaraUnit.BaseRole = "검객";
            kiaraUnit.MaxHealth = 700f;
            kiaraUnit.Defense = 32f;
            kiaraUnit.Resistance = 0.05f;
            kiaraUnit.AttackDamage = 155f;
            kiaraUnit.AttackCooldown = 4.0f;
            kiaraUnit.AttackCount = 2;
            kiaraUnit.AttackDamageRatio = 0.65f;
            kiaraUnit.MoveCostAP = 24f;
            kiaraUnit.AttackCostAP = 24f;
            kiaraUnit.SpeedLevel = 5;
            kiaraUnit.Weight = 2;
            kiaraUnit.UpdateMoveSpeed();

            act1.SkillName = "공격 회복 - 3타 연속 공격";
            act2.SkillName = "자동 회복 - 전술 보존 (AP 단축)";
            act3.SkillName = "황후의 심판 (광역 물리 피해 + 광속 폼 전환)";

            kiaraUnit.SelectableActiveSkills.Clear();
            kiaraUnit.SelectableActiveSkills.Add(act1);
            kiaraUnit.SelectableActiveSkills.Add(act2);
            kiaraUnit.SelectableActiveSkills.Add(act3);
            kiaraUnit.SelectedSkillIndex = 2; // 기본 3스킬 장착

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Units"))
                AssetDatabase.CreateFolder("Assets/Resources", "Units");
            SaveAsset(kiaraUnit, "Assets/Resources/Units/Kiara_UnitData.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Kiara assets (Actives + Passives + Kiara_UnitData) generated successfully!");
        }

        private static void CreateProfile(string fileName, string effectId, StatModifierEntry[] entries)
        {
            var profile = ScriptableObject.CreateInstance<StatModifierProfileSO>();
            profile.EffectId = effectId;
            profile.Modifiers.AddRange(entries);
            SaveAsset(profile, $"Assets/Resources/StatModifiers/{fileName}.asset");
        }

        private static void SaveAsset(ScriptableObject asset, string path)
        {
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
#endif
