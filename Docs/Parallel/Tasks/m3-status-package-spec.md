# M3 상태 조합 설계 감사 (GLM-01 산출)

- 작업: GLM-01 (Docs/Parallel/GLM_TASK_QUEUE.md "즉시 배정할 조사·설계 작업")
- 기준 커밋: 77160f4 (작업 큐 기준일 2026-09-08과 동일 기준)
- 성격: 문서만 작성. 코드 수정 없음. Unity 미실행.
- 이 문서의 역할: GLM-05(M3 상태 패키지 구현)의 작업서 후보. 수치 확정은 메인·사용자 승인 필요.

## 0. 요약

- 상태 저장·조회는 `SimulationRuntime` 단일 소스로 일원화돼 있다(`Components/StatusEffectComponent.cs:161-168, 257-265`).
- 후보 4종 중 **Chill/Freeze(쇄빙 포함)**가 구현 완성도 1위, **Superconduct**가 2위(재부여→Stagger만 공백), **Stagger/Grab**은 Stagger는 넉백 연계가 완성이지만 Grab 생산 경로가 부재, **Bleed**는 GDD 규격과 구현이 구조적으로 다르다.
- 시스템 리스크: 상태 적용 경로가 5개 이상 병존하고, DOT 프로세서가 이중 등록돼 테스트 규칙과 프로덕션 규칙이 어긋나 있다(§4). 구현 작업(GLM-05)에 앞서 메인이 정리 방침을 정해야 한다.

## 1. 후보 조합별 구현·미구현·충돌 지점

기준 틱 = 100ms(`Core/ActionCommands.cs:14`). 이하 경로는 `Assets/Scripts/` 기준.

### 1.1 Chill / Freeze (GDD Part 2 §3.3)

| 항목 | 상태 | 근거 |
|---|---|---|
| Chill: 모든 행동 AP +20% | **구현** | `Effects/Processors/ElementalAuraProcessor.cs:153-158` 부여, `Components/StatusEffectComponent.cs:126` ActionCostMultiplier ×1.2 |
| Freeze: 전 행동 봉쇄 | **구현** | `ElementalAuraProcessor.cs:56-68 → TriggerFreeze(270-310)`, `StatusEffectComponent.cs:90-96` CanMove/CanAttack/CanUseSkills 차단 |
| Freeze: 무게 반비례 지속 | **구현(수치 근거 없음)** | `60/(Weight+1)`틱(`ElementalAuraProcessor.cs:291-293`) — GDD 서술과 방향은 일치하나 수치 규격 부재 |
| Freeze: 보스는 Def/Res −25% | **구현** | FrozenBossDebuff, `StatusEffectComponent.cs:107,116` |
| 쇄빙: 빙결 해제 + 방어력 연산 추가 피해 | **구현(이중 경로 충돌)** | 신규: 현재 Def×2 + 해제(`Runtime/Processors/DamageMutationProcessor.cs:37-51, 19행 상수`). 구식: 물리 피해 ×1.5(`StatusEffectComponent.cs:47,183-186`, 적용 `Components/HealthComponent.cs:158`) — **둘 다 살아 있음** |
| 원소 부여 입구 | **끊김** | 유일한 부여 API `StatusEffectComponent.ApplyElement`(`StatusEffectComponent.cs:216-240`)의 호출부가 전무 → 아우라 반응 실전 도달 불가 위험 |

### 1.2 Stagger / Grab (GDD Part 2 §2.2, §2.3)

| 항목 | 상태 | 근거 |
|---|---|---|
| Stagger: 다음 스킬 AP·SP 증가 | **부분** | AP ×1.2만(`Core/Actions/DefaultActionCostPolicy.cs:21,34-47`), SP는 무보정(45행) — GDD "AP 및 SP"와 불일치 |
| Stagger: 넉백 시 무게 −1(보스 면역) | **구현(완성)** | `Core/PhysicsRules.cs:15-22`, 물리 회귀 테스트 12개로 고정 |
| Grab: 비틀거림 대상 당기면 첫 공격 강제 치명타 + 40% | **부분** | `GrabMutation` 처리기 존재(`Runtime/Processors/PhysicsMutationProcessor.cs:59-112`), Stagger 시 GrabVulnerability 부여(83-98, 지속 10틱 하드코딩) — 그러나 **프로덕션에서 GrabMutation을 만드는 코드가 없음**(정의 `Runtime/Mutations/GrabMutation.cs` + 테스트만 존재) |
| GrabVulnerability: 강제 치명타 + 1회 소모 | **미구현** | +40% 피해 배율만(`StatusEffectComponent.cs:177-181`), 소모 처리 없음 |

### 1.3 Bleed (GDD Part 2 §2.3)

| 항목 | 상태 | 근거 |
|---|---|---|
| 중첩 n → 다음 n회 행동마다 고정 피해 | **미구현(구조 불일치)** | 틱 피해 없음이 테스트로 고정(`Testing/EditModeTests/StatusEffectRegressionTests.cs:187-202`), 행동 훅은 빈 구현(`StatusEffectComponent.cs:170-173`) |
| 부여 경로 | **3종 혼재(충돌)** | 10초(`Components/SubclassComponent.cs:88`), 2틱+Magnitude 300(`Core/Actions/Resolvers/AttackActionResolver.cs:63-77`), 2틱(`Editor/KiaraAssetGenerator.cs:103`) |

### 1.4 Superconduct (GDD Part 2 §3.2)

| 항목 | 상태 | 근거 |
|---|---|---|
| Def −40% | **구현** | `ElementalAuraProcessor.cs:184-189`, `StatusEffectComponent.cs:106` ×0.6 |
| 초전도 상태 재전기 시 Stagger 추가 | **미구현** | 반응 시 아우라가 소모되어(`ElementalAuraProcessor.cs:107-111`) 재부여 판정 로직 부재 |

## 2. 코드가 이미 보장하는 값 (구현 작업에서 재사용)

| 규칙 | 값 | 위치 |
|---|---|---|
| 지속시간 기본값 | Stagger 4초, Burn/Ignite/Overload/Superconduct/Chill/Necrosis/Poison 6초, Paralysis/Virus 4초, CC 3~5초 | `StatusEffectComponent.cs:15-39` |
| 스택 정책 4종 | Refresh(스택 합산+지속 max 연장) / Replace / Independent / MaxStackCap, 캡 클램프 | `Core/Effects/EffectStackPolicy.cs:3-15`, `Effects/EffectRuntimeState.cs:132-158, 219-227` — `StatusEffectRegressionTests.cs:59-129`로 고정 |
| 틱당 적용 상한 | 기본 4회 | `EffectStackPolicy.cs:15`, `EffectSystem.cs:418-443` |
| DOT 비율 | Burn 2% / Ignite 3% / Poison 2% MaxHP | `Core/ActionRuntimeController.cs:721-725` |
| 넉백/비틀거림 | 거리=힘−실효무게, Stagger 무게−1, 보스 면역 | `PhysicsRules.cs:15-22` |
| 쇄빙(신규) | 현재 Def×2 추가 피해 + 빙결 해제 | `DamageMutationProcessor.cs:19, 37-51` |
| 반응 안전장치 | 깊이 5, 체인 128회, 재귀 시그니처 차단 | `Core/ReactionSystem.cs:17-18, 182-202` |

## 3. 기획 결정이 필요한 값 (사용자/메인 결정 사항 — 임의 확정하지 않음)

1. **시연 조합 최종 선택** (아래 §4 제안 참조)
2. Freeze 지속 공식 수치: `60/(Weight+1)`틱 유지 여부 (`ElementalAuraProcessor.cs:291-293`)
3. Stagger 지속 통일: 디버그 8초(`BattleManager.cs:527`) vs 기본 4초, SP 비용 증가치 (GDD만 있고 수치 없음)
4. GrabVulnerability: 지속(현 10틱), 강제 치명타 구현 방식, 1회 소모 여부
5. Bleed: 부여 지속 통일값(현 3종 혼재), "행동당 고정 피해"량
6. Poison 피해 타입 3중 모순 정리: GDD "마법"(`StatusEffectProcessor.cs:76` 테스트는 트루뎀, 프로덕션 `DotEffectProcessor.cs:45`는 마법)
7. Superconduct 재부여→Stagger 채택 여부
8. 시연 밖 상태(Stun/Root/Silence/Disarm/Taunt/Stealth) 지속시간(현 3~5초 임의) — 명시적 제외할지 수치 확정할지
9. Haste: GDD 규격 없음 + 미구현 데드 enum 값(`StatusEnums.cs:42`) — enum에서 제외할지
10. 성역 회복: GDD 초당 2% vs 코드 1.5%/20틱(=초당 0.75%) (`GridSystem.cs:22`, `EffectSystem.cs:223`)

## 4. 시연용 조합 2~3개 제안 (권장순)

| 순위 | 조합 | 근거 | 남은 구현량 |
|---|---|---|---|
| 1 | **Chill/Freeze + 쇄빙** | 구현 완성도 최고. 행동 봉쇄·해제가 화면에서 즉시 읽힘(M3 완료 기준 "즉시 이해" 부합). 물리 회귀·상태 회귀 테스트 이미 존재 | 쇄빙 이중 경로 정리, 원소 부여 입구 연결, Freeze 지속 수치 확정 |
| 2 | **Stagger/Grab (넉백 연계)** | Stagger는 M2 넉백/Splat 도구와 자연 연결(비틀거림 무게 −1). M2 콤보 시연과 스토리가 이어짐 | Grab 생산 경로 신설(능력→GrabMutation), 강제 치명타·1회 소모 |
| 3 | Superconduct | Def×0.6만으로도 데미지 포커스 시연 가능. 쇄빙과 함께 방어력 기반 테마 형성 | 재부여→Stagger 구현 |

Bleed는 규격 자체가 틱 피해와 다른 "행동 훅" 방식이라 구현량이 가장 크고 기존 테스트와 충돌하므로, 시연 3종에 넣지 않고 명시적 제외를 권장한다(사용자 결정). 1+2 조합(냉기 넉백 테마) 또는 1+3(방어력 무너뜨리기 테마)이 구현 비용 대비 시연 가치가 높다.

## 5. 구현(GLM-05) 전 메인이 정리해야 할 시스템 결정

1. **상태 적용 경로 단일화 방침**: 현 5개 경로 병존 — ①`StatusEffectComponent.ApplyStatusEffect→ApplyEffectMutation`(`StatusEffectComponent.cs:191-209`) ②`ElementalAuraProcessor→CommitMutation`(16-21) ③`AttackActionResolver` 직접 생성(66-77) ④`PhysicsMutationProcessor` 직접 생성(87-97) ⑤`EffectSystem.ApplyTerrainTileEffects` 직접 호출(뮤테이션 우회, `EffectSystem.cs:205-249`)
2. **DOT 이중 프로세서 정리**: `DotEffectProcessor`가 `StatusEffectProcessor`보다 먼저 등록돼(`ActionRuntimeController.cs:720-730`) Burn/Ignite/Poison은 Dot이 독점, StatusEffectProcessor 쪽은 데드코드 — 그런데 회귀 테스트는 StatusEffectProcessor를 직접 검증 → 테스트와 프로덕션 규칙 불일치(Poison 타입 모순 포함)
3. **StatModifierEffectProcessor 미등록**: `ApplyEffectMutation.StatOverrides`(Chill/Overload 등 수치 오버라이드) 전체가 무효 → `StatusEffectComponent` 폴링 getter가 별도 커버하는 이중 표현 상태
4. **ElementType 이중 정의**: `StatusEnums.cs:10-17`(Fire/Lightning/Cold/Nature) vs `Core/Abilities/EffectDataSO.cs:5-12`(Physical/Frost/Nature/Magic)
5. **Paralysis 반쯤 가림 판정**: Refresh 갱신 시 `RemainingDuration` 미갱신(`EffectRuntimeState.cs:132-158`)으로 봉쇄 비율 왜곡 가능(`StatusEffectComponent.cs:90-93`)
6. Stagger/Freeze 등에 의한 **캐스팅 인터럽트 연동 부재**, **Stun 시 AP 회복 정지 미연결**(`APManager.SetRegenPaused`, `APTypes.cs:36`에 사유 enum 존재하나 호출자 없음)

## 6. 시연 범위 밖 명시 제외 후보 (MILESTONES "확장성은 검증 뒤에" 원칙)

Bleed 규격 재설계, Ignite 장판(`ElementalAuraProcessor.cs:255-268` TODO), Virus 전염 심화, 은신 해제 조건, Haste, CC 6종 수치 확정.
