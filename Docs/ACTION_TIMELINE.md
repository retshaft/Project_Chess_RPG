# Action Timeline 정의

`Action Timeline`은 모든 행동을 100ms 단위 Tick으로 표현하는 공통 규격이다.

## Tick 기준

- `1 Tick = 100ms`
- `10 Tick = 1.0초`

## 행동 속도 → Tick(100ms) 변환 공식

| Action Speed Tier | ActionDuration (Ticks) | 실시간 기준 |
|---|---:|---:|
| VeryFast | 7 | 0.7초 |
| Fast | 10 | 1.0초 |
| Normal | 14 | 1.4초 |
| Slow | 20 | 2.0초 |
| VerySlow | 25 | 2.5초 |

> 공식: `ActionDuration = ToActionDurationTicks(ActionSpeedTier)`

## 공식 필드 정의

모든 필드는 Tick(100ms) 단위를 사용한다.

- `ActionDuration`
  - 행동 하나의 총 길이(시작부터 종료까지).
- `ResolveTiming`
  - 행동 시작 후 실제 판정(피해/이동 확정/효과 적용)이 발생하는 시점 오프셋.
- `RecoveryTiming`
  - Resolve 이후 후딜 구간 길이.
  - `ResolveTiming + RecoveryTiming <= ActionDuration` 조건을 만족해야 한다.
- `InterruptWindow`
  - 행동 시작 이후 인터럽트가 허용되는 최대 구간 길이.
  - `InterruptWindow <= ActionDuration` 조건을 만족해야 한다.

## 코드 기준 위치

- `Assets/Scripts/Core/ActionCommands.cs`
  - `ActionSpeedTier`
  - `ActionTimelineFormula`
  - `ActionTimelineDefinition`
