// UnitData.cs
// ScriptableObject that holds design-time data for a unit archetype.
// Create instances via Assets > Create > CheckmateRPG > Unit Data.
// Units reference this asset at runtime to avoid hardcoded values.

using UnityEngine;

namespace CheckmateRPG.Data
{
    [CreateAssetMenu(menuName = "CheckmateRPG/Unit Data", fileName = "NewUnitData")]
    public class UnitData : ScriptableObject
    {
        // ─── Identity ─────────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Display name of the unit archetype (e.g. \"Knight\", \"Archer\").")]
        public string UnitName = "Unit";

        // ─── Health ───────────────────────────────────────────────────────────────

        [Header("Health")]
        [Tooltip("Maximum hit points.")]
        [Min(1f)] public float MaxHealth = 100f;

        // ─── Defense ──────────────────────────────────────────────────────────────

        [Header("Defense")]
        [Tooltip("Physical damage reduction (0 = none, 1 = immune).")]
        [Range(0f, 1f)] public float Defense = 0.1f;

        [Tooltip("Magical damage reduction (0 = none, 1 = immune).")]
        [Range(0f, 1f)] public float Resistance = 0.1f;

        // ─── Combat ───────────────────────────────────────────────────────────────

        [Header("Combat")]
        [Tooltip("Damage dealt per attack.")]
        [Min(0f)] public float AttackDamage = 10f;

        [Tooltip("Minimum seconds between two attacks.")]
        [Min(0.1f)] public float AttackCooldown = 1f;

        [Tooltip("Maximum grid distance at which the unit can attack (Chebyshev distance).")]
        [Min(1)] public int AttackRange = 1;

        [Tooltip("AI scoring value awarded when this unit is defeated.")]
        [Min(0f)] public float KillValue = 10f;

        // ─── Resources ───────────────────────────────────────────────────────────

        [Header("Resources")]
        [Tooltip("Maximum SP (used for elemental and skill interactions).")]
        [Min(0f)] public float MaxSP = 100f;

        // ─── Movement ─────────────────────────────────────────────────────────────

        [Header("Movement")]
        [Tooltip("Maximum grid cells moved per action.")]
        [Min(1)] public int MoveRange = 3;

        [Tooltip("World-units per second used when lerping to the target cell.")]
        [Min(0.1f)] public float MoveSpeed = 5f;

        // ─── Physics ─────────────────────────────────────────────────────────────

        [Header("Physics")]
        [Tooltip("Weight grade used for knockback calculations (0 = light, 4 = heavy).")]
        [Range(0, 4)] public int Weight = 1;

        [Tooltip("Bosses ignore weight reduction from stagger and resist freeze.")]
        public bool IsBoss = false;

        // ─── Future Extensions ────────────────────────────────────────────────────
        // AP cost, faction, visual prefab reference, etc. can be added here
        // without touching runtime component code.
    }
}
