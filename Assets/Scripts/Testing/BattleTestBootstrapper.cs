// BattleTestBootstrapper.cs
// Self-contained battle-test setup for Assets/Scenes/Test/BattleTest.unity.
//
// Attach this MonoBehaviour to the "BattleTestBootstrapper" GameObject in the scene.
// On Awake it:
//   1. Creates a GridSystem instance if none exists yet.
//   2. Spawns 6 units (3 blue / 3 red) on the grid and registers their occupancy.
//
// No prefabs or ScriptableObject assets are required – everything is built at runtime
// so the scene works immediately after a fresh clone.

using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;

namespace CheckmateRPG.Testing
{
    /// <summary>
    /// Creates a minimal playable battle scenario at runtime.
    /// </summary>
    public class BattleTestBootstrapper : MonoBehaviour
    {
        // ─── Spawn Table ──────────────────────────────────────────────────────────

        private static readonly (string UnitName, Vector2Int Cell, bool IsEnemy)[] SpawnTable =
        {
            ("Player_Warrior", new Vector2Int(1, 0), false),
            ("Player_Archer",  new Vector2Int(3, 0), false),
            ("Player_Knight",  new Vector2Int(5, 0), false),
            ("Enemy_Warrior",  new Vector2Int(2, 7), true),
            ("Enemy_Archer",   new Vector2Int(4, 7), true),
            ("Enemy_Knight",   new Vector2Int(6, 7), true),
        };

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureGridSystem();
            SpawnAllUnits();
        }

        // ─── Setup Helpers ────────────────────────────────────────────────────────

        private static void EnsureGridSystem()
        {
            if (GridSystem.Instance != null)
                return;

            var gridGO = new GameObject("GridSystem");
            gridGO.AddComponent<GridSystem>();
            Debug.Log("[BattleTestBootstrapper] GridSystem created.");
        }

        private static void SpawnAllUnits()
        {
            foreach (var (unitName, cell, isEnemy) in SpawnTable)
            {
                UnitData data = CreateUnitData(unitName, isEnemy);
                SpawnUnit(unitName, cell, data, isEnemy);
            }

            Debug.Log("[BattleTestBootstrapper] All 6 units spawned and registered on grid.");
        }

        private static UnitData CreateUnitData(string unitName, bool isEnemy)
        {
            var data       = ScriptableObject.CreateInstance<UnitData>();
            data.name      = unitName + "_Data";
            data.UnitName  = unitName.Replace("_", " ");
            data.MaxHealth = isEnemy ? 80f : 100f;
            data.Defense   = 0.1f;
            data.Resistance = 0.1f;
            data.AttackDamage   = isEnemy ? 12f : 15f;
            data.AttackCooldown = 1.2f;
            data.AttackRange    = 1;
            data.KillValue      = 10f;
            data.MaxSP          = 100f;
            data.MoveRange      = 3;
            data.MoveSpeed      = 5f;
            data.Weight         = 1;
            data.IsBoss         = false;
            return data;
        }

        private static void SpawnUnit(string unitName, Vector2Int cell, UnitData data, bool isEnemy)
        {
            // Parent GameObject – holds game-logic components
            var go = new GameObject(unitName);

            // Visual child: a simple primitive so the unit is visible in the Game view
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale    = new Vector3(0.65f, 0.5f, 0.65f);

            // Remove the collider – physics are grid-based, not physics-engine-based
            Destroy(visual.GetComponent<Collider>());

            // Team colour (works with both Built-in and URP pipelines)
            var rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat   = new Material(rend.sharedMaterial);
                var color = isEnemy
                    ? new Color(0.90f, 0.20f, 0.20f)
                    : new Color(0.20f, 0.45f, 0.90f);
                mat.color = color;
                // URP uses _BaseColor; setting both is harmless
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                rend.material = mat;
            }

            // Add logic components before UnitBrain so Awake() can cache them
            go.AddComponent<HealthComponent>();
            go.AddComponent<MovementComponent>();
            go.AddComponent<CombatComponent>();
            go.AddComponent<StatusEffectComponent>();

            // UnitBrain.Awake() runs here and caches the components added above
            var brain = go.AddComponent<UnitBrain>();

            // Set data fields before Start() runs and initialises the components
            brain.Prepare(data, cell);

            Debug.Log($"[BattleTestBootstrapper] Spawned {unitName} at {cell}.");
        }
    }
}