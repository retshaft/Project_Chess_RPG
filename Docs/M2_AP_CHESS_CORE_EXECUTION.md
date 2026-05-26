# M2 AP Chess Core Execution

> Started: 2026-05-27
>
> Scope: AP 기반 체스 전투 코어를 실제 플레이 루프로 고정

## Goal

기물 선택 이후 `Move / Attack / Skill` 모드를 통해 입력이 분리되고,  
AP 소모/부족 피드백과 기물별 공격 규칙이 일관되게 동작하는 상태를 만든다.

## Checklist

| Item | Status | Notes |
|---|---|---|
| 전원 배치 시작 루프 (`BattleTest`) | Done | Blue/Red 팀 스폰 및 킹 포함 배치 확인 |
| AP 소모/회복/부족 피드백 루프 | Done | AP UI/부족 경고/공격 실패 로그 확인 |
| 체스 기물 이동 규칙 검증 | Done | 기존 이동 패턴 유지, 입력 모드 분리 후 정상 |
| 입력-행동-자원 소모 연결 (Move/Attack/Skill) | Done | 모드 버튼 기반 분리 완료, Skill은 placeholder 엔트리 연결 |
| 킹 중심 승패 조건 | Done | `King Defeat` 우선 승패 로직 동작 확인 |
| 공격 규칙/오버레이/실행 판정 일치 | Done | 나이트 L자, 룩/비숍/퀸 경로 판정 공통화 |
| M2 회귀 체크 항목 고정 | Done | 아래 3개 시나리오 고정 |

## Regression Scenarios (M2 Lock)

1. `Move` 모드 기본 루프
   선택한 아군 기물에서 파란 셀이 표시되고, 빈 칸 클릭 시 이동 큐가 정상 등록된다.

2. `Attack` 모드 규칙 루프
   `Knight`는 L자 위치에서만, `Rook`은 직선 + 경로 비차단 상태에서만 공격 가능하다.  
   사거리 밖 또는 부적합 타겟 클릭 시 실패 로그가 명확히 출력된다.

3. `Skill` 모드 엔트리 루프
   `Skill` 버튼 클릭 후 셀 클릭 시 placeholder 스킬이 런타임 큐에 등록되고, 큐 로그가 출력된다.

## Implementation References

- 입력/모드/UI: `Assets/Scripts/Testing/TestSceneBattleManager.cs`
- 선택 오버레이: `Assets/Scripts/Testing/BattleSelectionOverlayController.cs`
- 공격 판정 공통 규칙: `Assets/Scripts/Core/CombatPatternRules.cs`
- 실제 공격 실행/로그: `Assets/Scripts/Components/CombatComponent.cs`
- 런타임 판정: `Assets/Scripts/Core/ActionRuntimeController.cs`

## Next Focus

1. M3 진입: 넉백/충돌(Splat)/무게 상호작용 고도화
2. Skill placeholder를 실제 기물별 스킬 데이터 파이프라인으로 교체
