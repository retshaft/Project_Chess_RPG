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

        // ─── Movement ─────────────────────────────────────────────────────────────

        [Header("Movement")]
        [Tooltip("Maximum grid cells moved per action.")]
        [Min(1)] public int MoveRange = 3;

        [Tooltip("World-units per second used when lerping to the target cell.")]
        [Min(0.1f)] public float MoveSpeed = 5f;

        // ─── Future Extensions ────────────────────────────────────────────────────
        // AP cost, weight, faction, visual prefab reference, etc. can be added here
        // without touching runtime component code.
    }
}
