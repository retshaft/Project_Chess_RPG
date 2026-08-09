using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Core.StatModifiers;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;
using CheckmateRPG.Components;
using UnityEngine;

namespace CheckmateRPG.Core.Effects.Processors
{
    public sealed class ElementalAuraProcessor : IEffectProcessor
    {
        private readonly Func<SimulationRuntime> _runtimeProvider;
        private readonly Action<IRuntimeMutation> _commitMutation;

        public ElementalAuraProcessor(Func<SimulationRuntime> runtimeProvider, Action<IRuntimeMutation> commitMutation)
        {
            _runtimeProvider = runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider));
            _commitMutation = commitMutation ?? throw new ArgumentNullException(nameof(commitMutation));
        }

        public bool CanProcess(IReadOnlyEffectRuntimeState effect)
        {
            if (effect == null) return false;
            return effect.EffectId == "FireAura" ||
                   effect.EffectId == "ColdAura" ||
                   effect.EffectId == "LightningAura" ||
                   effect.EffectId == "NatureAura";
        }

        public void OnApplied(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
            SimulationRuntime runtime = _runtimeProvider();
            if (runtime == null) return;

            string existingAuraId = null;
            string existingKey = null;

            // 1. 대상에게 이미 아우라가 있는지 확인 (자신 제외)
            foreach (var kvp in runtime.ActiveEffects)
            {
                var otherEffect = kvp.Value;
                if (otherEffect.TargetId == effect.TargetId &&
                    otherEffect.EffectId != effect.EffectId &&
                    IsAura(otherEffect.EffectId))
                {
                    existingAuraId = otherEffect.EffectId;
                    existingKey = kvp.Key;
                    break;
                }
            }

            // 2. 빙결(Freeze) 처리: 한기(Chill) + 냉기(ColdAura)
            if (string.IsNullOrEmpty(existingAuraId) && effect.EffectId == "ColdAura")
            {
                string chillKey = SimulationRuntime.BuildEffectKey(effect.TargetId, StatusEffectType.Chill.ToString());
                if (runtime.TryGetMutableEffect(chillKey, out var mutableChill) && mutableChill.Lifecycle != EffectLifecycle.Removed)
                {
                    TriggerFreeze(effect.TargetId, effect.SourceId);
                    mutableChill.TransitionLifecycle("AuraSystem", EffectLifecycle.Removed);
                    
                    if (runtime.TryGetMutableEffect(SimulationRuntime.BuildEffectKey(effect.TargetId, effect.EffectId), out var mutableNew))
                        mutableNew.TransitionLifecycle("AuraSystem", EffectLifecycle.Removed);
                    return;
                }
            }

            // 3. 바이러스(Virus) 처리: 중독(Poison) + 자연(NatureAura)
            if (string.IsNullOrEmpty(existingAuraId) && effect.EffectId == "NatureAura")
            {
                string poisonKey = SimulationRuntime.BuildEffectKey(effect.TargetId, StatusEffectType.Poison.ToString());
                if (runtime.TryGetMutableEffect(poisonKey, out var mutablePoison) && mutablePoison.Lifecycle != EffectLifecycle.Removed)
                {
                    TriggerVirus(effect.TargetId, effect.SourceId);
                    // 중독은 제거하지 않고 갱신/전염시키므로 Virus 로직에서 다룹니다.
                    
                    if (runtime.TryGetMutableEffect(SimulationRuntime.BuildEffectKey(effect.TargetId, effect.EffectId), out var mutableNew))
                        mutableNew.TransitionLifecycle("AuraSystem", EffectLifecycle.Removed);
                    return;
                }
            }

            // 4. 동일한 원소가 이미 있는지 확인 (자신 제외)
            if (string.IsNullOrEmpty(existingAuraId))
            {
                foreach (var kvp in runtime.ActiveEffects)
                {
                    var otherEffect = kvp.Value;
                    if (otherEffect.TargetId == effect.TargetId &&
                        otherEffect.EffectId == effect.EffectId &&
                        kvp.Key != SimulationRuntime.BuildEffectKey(effect.TargetId, effect.EffectId) && // 임시 체크
                        otherEffect.Lifecycle != EffectLifecycle.Removed)
                    {
                        existingAuraId = otherEffect.EffectId;
                        existingKey = kvp.Key;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(existingAuraId))
            {
                TriggerReaction(existingAuraId, effect.EffectId, effect.TargetId, effect.SourceId);
                
                if (runtime.TryGetMutableEffect(existingKey, out var mutableExisting))
                    mutableExisting.TransitionLifecycle("AuraSystem", EffectLifecycle.Removed);
                    
                if (runtime.TryGetMutableEffect(SimulationRuntime.BuildEffectKey(effect.TargetId, effect.EffectId), out var mutableNew))
                    mutableNew.TransitionLifecycle("AuraSystem", EffectLifecycle.Removed);
            }
        }

        public EffectProcessorResult OnTick(
            EffectSystemContext context,
            EffectMutationContext mutationContext,
            EffectMutationFactory mutationFactory,
            IReadOnlyEffectRuntimeState effect)
        {
            return EffectProcessorResult.Empty;
        }

        public void OnExpired(EffectSystemContext context, IReadOnlyEffectRuntimeState effect)
        {
        }

        private bool IsAura(string id)
        {
            return id == "FireAura" || id == "ColdAura" || id == "LightningAura" || id == "NatureAura";
        }

        private void TriggerReaction(string firstAura, string secondAura, Guid targetId, Guid sourceId)
        {
            bool HasCombo(string a, string b, string e1, string e2) => (a == e1 && b == e2) || (a == e2 && b == e1);

            int currentTick = _runtimeProvider().CurrentTick;
            var context = new MutationContext(currentTick, Guid.Empty, targetId, "ElementalReaction");
            var mutationId = SeededRandomProvider.Shared.NextGuid();

            // ─── 동일 원소 ───
            if (HasCombo(firstAura, secondAura, "FireAura", "FireAura"))
            {
                // 화상: Res 20% 감소
                var mods = new List<StatModifierOverride> { new StatModifierOverride { TargetType = StatModifierType.ResistanceMultiplier, OverriddenValue = 0.8f } };
                _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Burn.ToString(), sourceId, targetId, 60, 10, 10, 1, 1f, Context: context, StatOverrides: mods));
            }
            else if (HasCombo(firstAura, secondAura, "LightningAura", "LightningAura"))
            {
                // 감전: 튕기는 마법 피해 + SP 소각
                TriggerShock(targetId, sourceId);
            }
            else if (HasCombo(firstAura, secondAura, "ColdAura", "ColdAura"))
            {
                // 한기: AP 비용 20% 증가
                var mods = new List<StatModifierOverride> { new StatModifierOverride { TargetType = StatModifierType.APCostMultiplier, OverriddenValue = 1.2f } };
                _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Chill.ToString(), sourceId, targetId, 60, 60, 60, 1, 1f, Context: context, StatOverrides: mods));
            }
            else if (HasCombo(firstAura, secondAura, "NatureAura", "NatureAura"))
            {
                // 중독: 단순 DOT
                _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Poison.ToString(), sourceId, targetId, 60, 10, 10, 1, 1f, Context: context));
            }
            
            // ─── 교차 원소 ───
            else if (HasCombo(firstAura, secondAura, "FireAura", "ColdAura"))
            {
                // 폭발: 십자 마법피해 + 넉백(가벼운적, 스플랫 없음)
                TriggerExplosion(targetId, sourceId);
            }
            else if (HasCombo(firstAura, secondAura, "FireAura", "NatureAura"))
            {
                // 발화: 지속 마법 피해 + 장판 스폰
                _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Ignite.ToString(), sourceId, targetId, 60, 10, 10, 1, 1f, Context: context));
                TriggerIgniteTile(targetId, sourceId);
            }
            else if (HasCombo(firstAura, secondAura, "FireAura", "LightningAura"))
            {
                // 과부하: 즉시 데미지 + Atk 25% 감소
                var mods = new List<StatModifierOverride> { new StatModifierOverride { TargetType = StatModifierType.AttackDamageMultiplier, OverriddenValue = 0.75f } };
                _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Overload.ToString(), sourceId, targetId, 60, 60, 60, 1, 1f, Context: context, StatOverrides: mods));
                _commitMutation(new DamageMutation(SeededRandomProvider.Shared.NextGuid(), targetId, sourceId, 10, false, context, DamageType.Magical));
            }
            else if (HasCombo(firstAura, secondAura, "LightningAura", "ColdAura"))
            {
                // 초전도: Def 40% 감소
                var mods = new List<StatModifierOverride> { new StatModifierOverride { TargetType = StatModifierType.DefenseMultiplier, OverriddenValue = 0.6f } };
                _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Superconduct.ToString(), sourceId, targetId, 60, 60, 60, 1, 1f, Context: context, StatOverrides: mods));
            }
            else if (HasCombo(firstAura, secondAura, "LightningAura", "NatureAura"))
            {
                // 마비: 공격 완전 봉쇄, 이동 봉쇄(절반 시간)
                var mods = new List<StatModifierOverride> { 
                    new StatModifierOverride { TargetType = StatModifierType.AttackLocked, OverriddenValue = 1f },
                    new StatModifierOverride { TargetType = StatModifierType.MoveLocked, OverriddenValue = 1f }
                };
                _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Paralysis.ToString(), sourceId, targetId, 40, 40, 40, 1, 1f, Context: context, StatOverrides: mods));
            }
            else if (HasCombo(firstAura, secondAura, "ColdAura", "NatureAura"))
            {
                // 괴사: 치유 감소 60%
                var mods = new List<StatModifierOverride> { new StatModifierOverride { TargetType = StatModifierType.HealReceivedMultiplier, OverriddenValue = 0.4f } };
                _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Necrosis.ToString(), sourceId, targetId, 60, 60, 60, 1, 1f, Context: context, StatOverrides: mods));
            }
        }

        private void TriggerExplosion(Guid targetId, Guid sourceId)
        {
            int currentTick = _runtimeProvider().CurrentTick;
            var context = new MutationContext(currentTick, Guid.Empty, targetId, "ExplosionReaction");
            var mutationId = SeededRandomProvider.Shared.NextGuid();

            // 1. 대상 본체에 데미지
            _commitMutation(new DamageMutation(mutationId, targetId, sourceId, 10, false, context, DamageType.Magical));

            // 2. 십자 범위 찾아서 넉백 및 데미지 (가벼운 적만 넉백, 스플랫 무시)
            if (ActionRuntimeController.Instance == null || !ActionRuntimeController.Instance.TryGetUnitBrain(targetId, out var centerBrain))
                return;

            var movement = centerBrain.GetComponent<MovementComponent>();
            if (movement == null || GridSystem.Instance == null) return;

            Vector2Int centerGrid = movement.GridPosition;
            Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            foreach (var offset in offsets)
            {
                Vector2Int neighborCell = centerGrid + offset;
                if (!GridSystem.Instance.IsValidCell(neighborCell)) continue;

                var occupant = GridSystem.Instance.GetOccupant(neighborCell);
                if (occupant == null || !occupant.TryGetComponent<UnitBrain>(out var occupantBrain)) continue;

                var occId = occupantBrain.ActorId;
                
                // 마법 피해
                _commitMutation(new DamageMutation(SeededRandomProvider.Shared.NextGuid(), occId, sourceId, 10, false, context, DamageType.Magical));

                // 넉백 로직 (무게 1 이하만)
                var occMovement = occupant.GetComponent<MovementComponent>();
                if (occMovement != null && occMovement.Weight <= 1f)
                {
                    _commitMutation(new KnockbackMutation(
                        SeededRandomProvider.Shared.NextGuid(),
                        occId,
                        offset, // 폭발 중심에서 바깥쪽
                        1, // Force
                        ApplySplatDamage: false,
                        context
                    ));
                }
            }
        }

        private void TriggerIgniteTile(Guid targetId, Guid sourceId)
        {
            // 아직 Tile 변경 Mutation이 없으므로 직접 ActionRuntimeController나 GridSystem을 통해 타일 변경
            // 기획서: 주변 타일에 지속적으로 불 부착 장판을 깐다
            if (ActionRuntimeController.Instance == null || !ActionRuntimeController.Instance.TryGetUnitBrain(targetId, out var centerBrain))
                return;

            var movement = centerBrain.GetComponent<MovementComponent>();
            if (movement == null || GridSystem.Instance == null) return;

            // TODO: 실제 프로젝트의 Tile 시스템에 Fire 장판을 부여하는 로직 추가
            // (M8에서 TerrainInteractionProcessor가 처리하는 타일 데이터가 있으나, 현재 타일 변경 API는 미구현 상태이므로 임시 주석)
            // GridSystem.Instance.SetTileEffect(movement.GridPosition, TileEffectType.Fire, duration: 60);
        }

        private void TriggerFreeze(Guid targetId, Guid sourceId)
        {
            int currentTick = _runtimeProvider().CurrentTick;
            var context = new MutationContext(currentTick, Guid.Empty, targetId, "FreezeReaction");
            var mutationId = SeededRandomProvider.Shared.NextGuid();

            if (ActionRuntimeController.Instance != null && ActionRuntimeController.Instance.TryGetUnitBrain(targetId, out var targetBrain))
            {
                var movement = targetBrain.GetComponent<MovementComponent>();
                if (movement != null)
                {
                    if (movement.IsBoss) // 보스인 경우 얼어붙지 않고 방어/마저 25% 감소
                    {
                        var bossMods = new List<StatModifierOverride> { 
                            new StatModifierOverride { TargetType = StatModifierType.DefenseMultiplier, OverriddenValue = 0.75f },
                            new StatModifierOverride { TargetType = StatModifierType.ResistanceMultiplier, OverriddenValue = 0.75f } 
                        };
                        _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.FrozenBossDebuff.ToString(), sourceId, targetId, 40, 40, 40, 1, 1f, Context: context, StatOverrides: bossMods));
                        return;
                    }
                    
                    // 무거운 대상일수록 지속시간 반비례
                    float weightFactor = Mathf.Max(1f, movement.Weight + 1f);
                    int durationTicks = Mathf.RoundToInt(60 / weightFactor);

                    var mods = new List<StatModifierOverride> { 
                        new StatModifierOverride { TargetType = StatModifierType.AttackLocked, OverriddenValue = 1f },
                        new StatModifierOverride { TargetType = StatModifierType.MoveLocked, OverriddenValue = 1f } 
                    };
                    _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Freeze.ToString(), sourceId, targetId, durationTicks, durationTicks, durationTicks, 1, 1f, Context: context, StatOverrides: mods));
                    return;
                }
            }

            // 기본 (Fallback)
            var fallbackMods = new List<StatModifierOverride> { 
                new StatModifierOverride { TargetType = StatModifierType.AttackLocked, OverriddenValue = 1f },
                new StatModifierOverride { TargetType = StatModifierType.MoveLocked, OverriddenValue = 1f } 
            };
            _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Freeze.ToString(), sourceId, targetId, 40, 40, 40, 1, 1f, Context: context, StatOverrides: fallbackMods));
        }

        private void TriggerVirus(Guid targetId, Guid sourceId)
        {
            int currentTick = _runtimeProvider().CurrentTick;
            var context = new MutationContext(currentTick, Guid.Empty, targetId, "VirusReaction");
            var mutationId = SeededRandomProvider.Shared.NextGuid();

            _commitMutation(new ApplyEffectMutation(mutationId, StatusEffectType.Virus.ToString(), sourceId, targetId, 60, 10, 10, 1, 1f, Context: context));

            // 바이러스 확산 (십자)
            if (ActionRuntimeController.Instance == null || !ActionRuntimeController.Instance.TryGetUnitBrain(targetId, out var centerBrain))
                return;

            var movement = centerBrain.GetComponent<MovementComponent>();
            if (movement == null || GridSystem.Instance == null) return;

            Vector2Int centerGrid = movement.GridPosition;
            Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            foreach (var offset in offsets)
            {
                Vector2Int neighborCell = centerGrid + offset;
                if (!GridSystem.Instance.IsValidCell(neighborCell)) continue;

                var occupant = GridSystem.Instance.GetOccupant(neighborCell);
                if (occupant == null || !occupant.TryGetComponent<UnitBrain>(out var occupantBrain)) continue;

                var occId = occupantBrain.ActorId;
                
                // 바이러스 중복 감염 방지 (Lock)
                string virusKey = SimulationRuntime.BuildEffectKey(occId, StatusEffectType.Virus.ToString());
                if (_runtimeProvider().TryGetEffect(virusKey, out var existingVirus) && existingVirus.Lifecycle != EffectLifecycle.Removed)
                    continue;

                // 중독 전염 (Poison)
                _commitMutation(new ApplyEffectMutation(
                    SeededRandomProvider.Shared.NextGuid(), 
                    StatusEffectType.Poison.ToString(), 
                    sourceId, 
                    occId, 
                    60, 10, 10, 1, 1f, 
                    Context: context));
            }
        }

        private void TriggerShock(Guid targetId, Guid sourceId)
        {
            int currentTick = _runtimeProvider().CurrentTick;
            var context = new MutationContext(currentTick, Guid.Empty, targetId, "ShockReaction");
            
            // 본체 마법 피해 및 SP 소각
            _commitMutation(new DamageMutation(SeededRandomProvider.Shared.NextGuid(), targetId, sourceId, 10, false, context, DamageType.Magical));
            _commitMutation(new SPMutation(SeededRandomProvider.Shared.NextGuid(), targetId, -20, context));

            // 주변 십자 튕기는 마법 피해 및 SP 소각
            if (ActionRuntimeController.Instance == null || !ActionRuntimeController.Instance.TryGetUnitBrain(targetId, out var centerBrain))
                return;

            var movement = centerBrain.GetComponent<MovementComponent>();
            if (movement == null || GridSystem.Instance == null) return;

            Vector2Int centerGrid = movement.GridPosition;
            Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            foreach (var offset in offsets)
            {
                Vector2Int neighborCell = centerGrid + offset;
                if (!GridSystem.Instance.IsValidCell(neighborCell)) continue;

                var occupant = GridSystem.Instance.GetOccupant(neighborCell);
                if (occupant == null || !occupant.TryGetComponent<UnitBrain>(out var occupantBrain)) continue;

                var occId = occupantBrain.ActorId;
                _commitMutation(new DamageMutation(SeededRandomProvider.Shared.NextGuid(), occId, sourceId, 10, false, context, DamageType.Magical));
                _commitMutation(new SPMutation(SeededRandomProvider.Shared.NextGuid(), occId, -10, context));
            }
        }
    }
}
