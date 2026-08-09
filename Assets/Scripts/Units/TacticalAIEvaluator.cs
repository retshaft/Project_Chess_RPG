using System;
using System.Collections.Generic;
using UnityEngine;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Prediction;
using CheckmateRPG.Core;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;

namespace CheckmateRPG.Units
{
    public static class TacticalAIEvaluator
    {
        private static EnemyAIProfile _defaultProfile;

        public static float GetKillValue(UnitBrain target)
        {
            if (target == null || target.UnitData == null) return 10f;
            return target.UnitData.PieceType switch
            {
                ChessPieceType.King => 1000f,
                ChessPieceType.Queen => 50f,
                ChessPieceType.Rook => 30f,
                ChessPieceType.Bishop => 25f,
                ChessPieceType.Knight => 20f,
                ChessPieceType.Pawn => 10f,
                _ => Mathf.Max(10f, target.UnitData.KillValue)
            };
        }

        public static ActionBid EvaluateBestAction(UnitBrain unit, float currentTeamAP, float maxTeamAP)
        {
            if (unit == null || unit.IsDead || unit.UnitData == null || unit.Movement == null)
                return default;

            GridSystem grid = GridSystem.Instance;
            ActionRuntimeController runtime = ActionRuntimeController.EnsureExists();
            if (grid == null || runtime == null)
                return default;

            EnemyAIProfile profile = unit.UnitData.AIProfile;
            if (profile == null)
            {
                if (_defaultProfile == null)
                    _defaultProfile = ScriptableObject.CreateInstance<EnemyAIProfile>();
                profile = _defaultProfile;
            }

            bool canAttack = unit.Combat != null && unit.Combat.CanAttack;
            Vector2Int origin = unit.Movement.GridPosition;

            float bestScore = float.MinValue;
            IActionCommand bestCommand = null;
            float bestRequiredAp = 0f;

            var targets = GetPotentialTargets(unit, grid);
            
            // 1. Evaluate Attacks from current position
            if (canAttack)
            {
                foreach (var target in targets)
                {
                    if (!unit.IsAttackRange(origin, target.Movement.GridPosition))
                        continue;

                    float apCost = Mathf.Max(0f, unit.UnitData.AttackCostAP);
                    if (apCost > currentTeamAP) continue;

                    float score = EvaluateAttackAction(unit, target, origin, profile);
                    score -= CalculateAPPenalty(apCost, currentTeamAP, maxTeamAP, profile.APConservationWeight, profile.BehaviorType);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCommand = runtime.BuildAttackPredictionCommand(unit, target.ActorId);
                        bestRequiredAp = apCost;
                    }
                }
            }

            // 2. Evaluate Moves (1-Step Lookahead)
            var reachableCells = unit.Movement.GetReachableCells();
            float moveBaseCost = Mathf.Max(0f, unit.UnitData.MoveCostAP);

            foreach (Vector2Int cell in reachableCells)
            {
                float moveCost = moveBaseCost * grid.GetMoveCostMultiplier(cell);
                if (moveCost > currentTeamAP) continue;
                if (cell == origin) continue; // Skip standing still here

                // Threat Aversion (Danger Map)
                float dangerScore = ThreatMap.GetThreatLevel(cell, unit.GetComponent<CheckmateRPG.Components.TeamComponent>().IsEnemy);
                float threatPenalty = dangerScore * profile.ThreatAversionWeight * GetKillValue(unit) * 0.5f;

                // Development & Positional Bonus
                float positionalScore = ScorePositionalValue(unit, cell);

                // Lookahead: What can I attack from here?
                float maxLookaheadScore = 0f;
                if (canAttack)
                {
                    foreach (var target in targets)
                    {
                        if (unit.IsAttackRange(cell, target.Movement.GridPosition))
                        {
                            float lookaheadScore = EvaluateAttackAction(unit, target, cell, profile) * 0.8f; // 약간의 할인율
                            maxLookaheadScore = Mathf.Max(maxLookaheadScore, lookaheadScore);
                        }
                    }
                }

                // Anticipation Move (Moving towards high value targets even if out of range)
                float anticipationScore = 0f;
                if (maxLookaheadScore == 0f)
                {
                    anticipationScore = EvaluateAnticipationMove(unit, cell, targets, profile);
                }

                float score = positionalScore + maxLookaheadScore + anticipationScore - threatPenalty;
                score -= CalculateAPPenalty(moveCost, currentTeamAP, maxTeamAP, profile.APConservationWeight, profile.BehaviorType);

                // King Protection (Bonus for moving near King if threatened)
                score += EvaluateKingProtection(unit, cell, profile);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCommand = runtime.BuildMovePredictionCommand(unit, cell);
                    bestRequiredAp = moveCost;
                }
            }

            if (bestCommand != null && bestScore > -9999f) // threshold
                return new ActionBid(unit, bestCommand, bestRequiredAp, bestScore);

            return default;
        }

        private static List<UnitBrain> GetPotentialTargets(UnitBrain unit, GridSystem grid)
        {
            var targets = new List<UnitBrain>();
            bool isEnemy = unit.GetComponent<CheckmateRPG.Components.TeamComponent>().IsEnemy;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    GameObject occupant = grid.GetOccupant(x, y);
                    if (occupant != null && occupant.TryGetComponent(out UnitBrain targetBrain))
                    {
                        if (!targetBrain.IsDead && targetBrain.TryGetComponent(out CheckmateRPG.Components.TeamComponent targetTeam))
                        {
                            if (targetTeam.IsEnemy != isEnemy)
                            {
                                if (targetBrain.StatusEffects != null && targetBrain.StatusEffects.IsStealthed)
                                    continue; // Stealth Override

                                targets.Add(targetBrain);
                            }
                        }
                    }
                }
            }

            // Taunt Override
            if (unit.StatusEffects != null && unit.StatusEffects.IsTaunted && targets.Count > 0)
            {
                targets.Sort((a, b) => Vector2Int.Distance(unit.Movement.GridPosition, a.Movement.GridPosition)
                                      .CompareTo(Vector2Int.Distance(unit.Movement.GridPosition, b.Movement.GridPosition)));
                var tauntTarget = targets[0];
                targets.Clear();
                targets.Add(tauntTarget);
            }

            return targets;
        }

        private static float EvaluateAttackAction(UnitBrain attacker, UnitBrain target, Vector2Int attackerPos, EnemyAIProfile profile)
        {
            float score = 0f;
            float killVal = GetKillValue(target);
            
            // Checkmate Override
            if (target.UnitData.PieceType == ChessPieceType.King)
            {
                score += killVal * 2f;
                if (attacker.UnitData.AttackDamage >= target.Health.CurrentHealth)
                {
                    score += 100000f; // Checkmate! 1순위 바이패스
                }
            }
            else
            {
                score += killVal * 10f;
            }

            // Weak Enemy Focus
            float hpPercent = target.Health.CurrentHealth / Mathf.Max(1f, target.Health.MaxHealth);
            if (hpPercent < 0.4f)
            {
                score += 50f * profile.WeakEnemyFocusWeight;
            }
            if (profile.BehaviorType == AIBehaviorType.Assassin)
            {
                if (target.UnitData.PieceType == ChessPieceType.Queen || target.UnitData.PieceType == ChessPieceType.Bishop)
                    score *= profile.AssassinTargetBonus;
            }

            // Setup Kill (Splat Prediction) - 낙사/벽 충돌 유도 각 계산
            int force = attacker.UnitData.Weight * 2;
            int targetWeight = target.UnitData.Weight;
            if (target.StatusEffects != null && target.StatusEffects.HasStatus(StatusEffectType.Stagger) && !target.UnitData.IsBoss)
                targetWeight = Mathf.Max(0, targetWeight - 1);
                
            int knockbackDist = Mathf.Max(0, force - targetWeight);
            if (knockbackDist > 0)
            {
                Vector2Int dir = GetPushDirection(attackerPos, target.Movement.GridPosition);
                Vector2Int predictedEnd = target.Movement.GridPosition + dir * knockbackDist;
                if (!GridSystem.Instance.IsValidCell(predictedEnd) || GridSystem.Instance.GetOccupant(predictedEnd.x, predictedEnd.y) != null)
                {
                    // Splat predicted! 강제 충돌/낙사 가중치 대폭 상향
                    score += 300f * profile.SetupKillWeight;
                }
            }

            return score;
        }

        private static float EvaluateAnticipationMove(UnitBrain unit, Vector2Int candidateCell, List<UnitBrain> targets, EnemyAIProfile profile)
        {
            // 수비형(Defensive) 패턴: 니가와 전술. 사거리 외 적에게 불필요하게 접근하려 하지 않음!
            if (profile.BehaviorType == AIBehaviorType.Defensive)
            {
                return -50f;
            }

            float bestAnticipation = 0f;
            foreach (var target in targets)
            {
                float targetValue = GetKillValue(target);
                float dist = Vector2Int.Distance(candidateCell, target.Movement.GridPosition);
                float score = targetValue - (dist * profile.DistancePenaltyWeight * 5f);
                
                float pieceMultiplier = 1f;
                if (unit.UnitData.PieceType == ChessPieceType.Queen) pieceMultiplier = 5f;
                else if (unit.UnitData.PieceType == ChessPieceType.Rook || unit.UnitData.PieceType == ChessPieceType.Bishop) pieceMultiplier = 3f;
                else if (unit.UnitData.PieceType == ChessPieceType.Knight) pieceMultiplier = 2f;

                bestAnticipation = Mathf.Max(bestAnticipation, score * pieceMultiplier);
            }
            return bestAnticipation;
        }

        private static float ScorePositionalValue(UnitBrain unit, Vector2Int cell)
        {
            // 중앙 전개 보너스 (Development Bonus)
            Vector2 center = new Vector2(GridSystem.GridWidth / 2f, GridSystem.GridHeight / 2f);
            float distToCenter = Vector2.Distance(cell, center);
            float maxDist = Vector2.Distance(Vector2.zero, center);
            float centralization = 1f - (distToCenter / maxDist);
            
            float pieceMultiplier = 1f;
            if (unit.UnitData.PieceType == ChessPieceType.Knight || unit.UnitData.PieceType == ChessPieceType.Bishop) pieceMultiplier = 3f;
            if (unit.UnitData.PieceType == ChessPieceType.Queen) pieceMultiplier = 2f;

            return centralization * 20f * pieceMultiplier;
        }

        private static float EvaluateKingProtection(UnitBrain unit, Vector2Int cell, EnemyAIProfile profile)
        {
            var commander = GameObject.FindObjectOfType<AITeamCommander>();
            if (commander == null) return 0f;

            UnitBrain allyKing = commander.GetAllyKing();
            if (allyKing == null) return 0f;

            // Danger Map
            float kingThreat = ThreatMap.GetThreatLevel(allyKing.Movement.GridPosition, allyKing.GetComponent<CheckmateRPG.Components.TeamComponent>().IsEnemy);
            if (kingThreat > 0)
            {
                // Danger Override: King evades danger
                if (allyKing == unit)
                {
                    float cellThreat = ThreatMap.GetThreatLevel(cell, unit.GetComponent<CheckmateRPG.Components.TeamComponent>().IsEnemy);
                    if (cellThreat == 0) return 50000f;
                    return 0f;
                }

                // Danger Override: Bodyguard
                float distToKing = Vector2Int.Distance(cell, allyKing.Movement.GridPosition);
                if (distToKing <= 1.5f)
                {
                    return 500f * profile.KingProtectionWeight;
                }
            }
            return 0f;
        }

        public static float CalculateAPPenalty(float cost, float currentAP, float maxAP, float conservationWeight, AIBehaviorType behavior)
        {
            if (cost == 0) return 0f;

            // 광전사 (Berserker) 패턴: AP가 모이는 즉시 스킬/행동을 시전하므로 보존 페널티 0
            if (behavior == AIBehaviorType.Berserker)
                return 0f;

            float apRatio = currentAP / Mathf.Max(1f, maxAP);
            float urgency = 1f - apRatio;

            // 전략가 (Tactician) 패턴: 고비용/고화력 일격을 위해 AP를 보존하려 함 (AP가 70% 이하일 때 페널티 증폭)
            float multiplier = 1f;
            if (behavior == AIBehaviorType.Tactician && apRatio < 0.7f)
            {
                multiplier = 2.5f;
            }
            
            float nonlinearPenalty = Mathf.Pow(urgency, 2f) * 100f * conservationWeight * cost * multiplier;
            return nonlinearPenalty;
        }

        private static Vector2Int GetPushDirection(Vector2Int attacker, Vector2Int target)
        {
            Vector2Int diff = target - attacker;
            if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y))
                return new Vector2Int(Mathf.Clamp(diff.x, -1, 1), 0);
            else
                return new Vector2Int(0, Mathf.Clamp(diff.y, -1, 1));
        }
    }

    public static class ThreatMap
    {
        private static readonly Dictionary<Vector2Int, float> _playerThreats = new Dictionary<Vector2Int, float>();

        public static float GetThreatLevel(Vector2Int cell, bool forEnemyTeam)
        {
            if (forEnemyTeam && _playerThreats.TryGetValue(cell, out float threat))
            {
                return threat;
            }
            return 0f;
        }
        
        public static void UpdateThreatMap()
        {
            _playerThreats.Clear();
            var grid = GridSystem.Instance;
            if (grid == null) return;

            for (int x = 0; x < GridSystem.GridWidth; x++)
            {
                for (int y = 0; y < GridSystem.GridHeight; y++)
                {
                    GameObject occupant = grid.GetOccupant(x, y);
                    if (occupant != null && occupant.TryGetComponent(out UnitBrain brain))
                    {
                        if (!brain.IsDead && brain.TryGetComponent(out CheckmateRPG.Components.TeamComponent team) && !team.IsEnemy)
                        {
                            // 플레이어 유닛이 맵에 가하는 위협 맵핑
                            float unitThreat = TacticalAIEvaluator.GetKillValue(brain);
                            
                            // 이 유닛이 공격 가능한 모든 타일을 구함
                            // 임시: Movement.GetReachableCells()와는 다르게 공격 사거리를 구해야 하지만
                            // 현재 UnitBrain에는 특정 타겟이 인레인지(IsAttackRange)인지 확인하는 함수만 존재함.
                            // 모든 타일에 대해 사거리 판정.
                            for (int tx = 0; tx < GridSystem.GridWidth; tx++)
                            {
                                for (int ty = 0; ty < GridSystem.GridHeight; ty++)
                                {
                                    Vector2Int targetCell = new Vector2Int(tx, ty);
                                    if (brain.IsAttackRange(brain.Movement.GridPosition, targetCell))
                                    {
                                        if (!_playerThreats.ContainsKey(targetCell))
                                            _playerThreats[targetCell] = 0f;
                                        _playerThreats[targetCell] += unitThreat;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
