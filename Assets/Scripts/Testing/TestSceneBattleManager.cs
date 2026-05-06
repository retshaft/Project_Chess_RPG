using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Components;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;

namespace CheckmateRPG.Testing
{
    public class TestSceneBattleManager : MonoBehaviour
    {
        [Header("Unit Data")]
        [SerializeField] private UnitData _pawnData;
        [SerializeField] private UnitData _knightData;

        [Header("Debug")]
        [SerializeField] private bool _enableAPDebugLogger = true;
        [SerializeField] private bool _spawnOnAwake = true;

        private readonly Dictionary<Team, List<UnitRecord>> _teams = new();
        private readonly List<UnitRecord> _allUnits = new();
        private bool _battleEnded;

        private enum Team
        {
            Blue,
            Red
        }

        private sealed class UnitRecord
        {
            public UnitBrain Brain;
            public Team Team;
        }

        private void Awake()
        {
            if (!_spawnOnAwake)
                return;

            BootstrapBattle();
        }

        private void Update()
        {
            if (_battleEnded)
                return;

            UpdateTargets();
            EvaluateBattleState();
        }

        private void BootstrapBattle()
        {
            EnsureGridSystem();
            EnsureAPManager(_enableAPDebugLogger);
            SpawnUnits();
        }

        private void SpawnUnits()
        {
            if (_pawnData == null || _knightData == null)
            {
                Debug.LogError("[TestSceneBattleManager] Pawn/Knight UnitData assets are not assigned.");
                return;
            }

            SpawnUnit("Blue Pawn", _pawnData, new Vector2Int(1, 0), Team.Blue);
            SpawnUnit("Blue Knight", _knightData, new Vector2Int(3, 0), Team.Blue);
            SpawnUnit("Red Pawn", _pawnData, new Vector2Int(6, 7), Team.Red);
            SpawnUnit("Red Knight", _knightData, new Vector2Int(4, 7), Team.Red);
        }

        private void UpdateTargets()
        {
            foreach (UnitRecord record in _allUnits)
            {
                if (record.Brain == null || record.Brain.IsDead)
                    continue;

                UnitBrain target = FindNearestEnemy(record);
                if (target != null)
                    record.Brain.SetTarget(target.gameObject);
                else
                    record.Brain.ClearTarget();
            }
        }

        private void EvaluateBattleState()
        {
            bool blueAlive = HasLivingUnits(Team.Blue);
            bool redAlive = HasLivingUnits(Team.Red);

            if (!blueAlive || !redAlive)
            {
                _battleEnded = true;
                string result = blueAlive == redAlive
                    ? "Draw"
                    : (blueAlive ? "Blue Victory" : "Red Victory");
                Debug.Log($"[TestSceneBattleManager] Battle ended: {result}");
            }
        }

        private bool HasLivingUnits(Team team)
        {
            if (!_teams.TryGetValue(team, out List<UnitRecord> roster))
                return false;

            foreach (UnitRecord record in roster)
            {
                if (record.Brain != null && !record.Brain.IsDead)
                    return true;
            }

            return false;
        }

        private UnitBrain FindNearestEnemy(UnitRecord source)
        {
            if (source.Brain == null || source.Brain.Movement == null)
                return null;

            Team enemyTeam = source.Team == Team.Blue ? Team.Red : Team.Blue;
            if (!_teams.TryGetValue(enemyTeam, out List<UnitRecord> roster))
                return null;

            Vector2Int origin = source.Brain.Movement.GridPosition;
            int bestDistance = int.MaxValue;
            UnitBrain bestTarget = null;

            foreach (UnitRecord candidate in roster)
            {
                if (candidate.Brain == null || candidate.Brain.IsDead || candidate.Brain.Movement == null)
                    continue;

                int distance = ManhattanDistance(origin, candidate.Brain.Movement.GridPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = candidate.Brain;
                }
            }

            return bestTarget;
        }

        private void RegisterUnit(UnitBrain brain, Team team)
        {
            if (brain == null)
                return;

            if (!_teams.TryGetValue(team, out List<UnitRecord> roster))
            {
                roster = new List<UnitRecord>();
                _teams[team] = roster;
            }

            var record = new UnitRecord { Brain = brain, Team = team };
            roster.Add(record);
            _allUnits.Add(record);
        }

        private void HandleUnitDeath(UnitBrain brain)
        {
            if (brain == null)
                return;

            if (GridSystem.Instance != null)
                GridSystem.Instance.ClearOccupant(brain.gameObject);

            brain.gameObject.SetActive(false);
        }

        private static void EnsureGridSystem()
        {
            if (GridSystem.Instance != null)
                return;

            var gridGO = new GameObject("GridSystem");
            gridGO.AddComponent<GridSystem>();
        }

        private static APManager EnsureAPManager(bool enableDebugLogging)
        {
            if (APManager.Instance != null)
                return APManager.Instance;

            var apGO = new GameObject("APManager");
            var manager = apGO.AddComponent<APManager>();

            if (enableDebugLogging)
                apGO.AddComponent<APDebugLogger>();

            return manager;
        }

        private void SpawnUnit(string unitName, UnitData data, Vector2Int cell, Team team)
        {
            var go = new GameObject(unitName);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            visual.transform.localScale = new Vector3(0.65f, 0.5f, 0.65f);

            Destroy(visual.GetComponent<Collider>());

            var rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(rend.sharedMaterial);
                var color = team == Team.Red
                    ? new Color(0.90f, 0.20f, 0.20f)
                    : new Color(0.20f, 0.45f, 0.90f);
                mat.color = color;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                rend.material = mat;
            }

            var health = go.AddComponent<HealthComponent>();
            go.AddComponent<MovementComponent>();
            go.AddComponent<CombatComponent>();
            go.AddComponent<StatusEffectComponent>();
            var teamComponent = go.AddComponent<TeamComponent>();
            teamComponent.SetIsEnemy(team == Team.Red);

            var brain = go.AddComponent<UnitBrain>();
            brain.Prepare(data, cell);

            health.OnDeath += () => HandleUnitDeath(brain);

            RegisterUnit(brain, team);
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
