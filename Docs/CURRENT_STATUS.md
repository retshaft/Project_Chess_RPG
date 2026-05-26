# Checkmate RPG Current Status

> 기준일: 2026-05-27
>
> 이 문서는 현재 코드베이스 기준의 현황 문서입니다. 기존 `Docs/MILESTONES.md`와
> `Docs/DEV_QUICKSTART.md`는 과거 기준의 참고 문서로 남겨둡니다.

## Project Direction

Checkmate RPG의 현재 개발 방향은 **체스를 명일방주식으로 해석한 AP 기반 실시간 위치전**입니다.

- 전투는 8x8 보드 위에서 전원 배치 상태로 시작한다.
- 모든 핵심 행동은 AP 경제 안에서 판단된다.
- 체스 기물별 이동 규칙은 전술 판단의 1차 언어다.
- 넉백, 충돌, 상태효과, 원소 반응은 AP 위치전을 풍부하게 만드는 2차 전술 레이어다.
- 졸업작품 기준에서는 모든 GDD 기능 구현보다, 시연 가능한 전투 루프와 가독성이 우선이다.

## Status Legend

| Status | Meaning |
|---|---|
| Implemented | 코드 구조와 기본 동작이 존재함 |
| Partial | 뼈대 또는 일부 기능은 있으나 완성/연결/연출이 부족함 |
| Needs Verification | 코드상 존재하나 Unity Play Mode, 씬 참조, 시연 흐름 검증이 필요함 |
| Not Started | 현재 코드 기준 명확한 구현을 확인하지 못함 |

## Implementation Status

| Area | Status | Notes |
|---|---|---|
| Battle Loop | Partial | `BattleTest` 계열 테스트 씬과 런타임 부트스트랩 구조가 있다. 전원 배치 시작은 가능하지만, 승패/재시작/시연 흐름은 별도 검증이 필요하다. |
| AP Economy | Implemented | 글로벌 AP 풀, 자연 회복, 소모, 부족 이벤트, 디버그 UI/피드백이 있다. 액션 예약 비용과도 연결되어 있다. |
| Chess Movement | Implemented | Pawn, Knight, King, Bishop/Rook/Queen용 슬라이딩 이동 패턴이 존재한다. Pawn promotion은 로그/TODO 수준이다. |
| Action Runtime | Partial | 액션 스케줄러, 비용 예약, 인터럽트, 해석 파이프라인, SP 자동 회복 구조가 있다. 전체 전투 UX와의 결합 검증이 필요하다. |
| Knockback & Splat | Partial | Knockback, Grab, Splat 고정 피해, 벽/기물 충돌 처리 구조가 존재한다. GDD 기준 무게 계산과 시각 예측은 더 다듬어야 한다. |
| Status & Element | Partial | Burn, Chill, Freeze, Superconduct, Poison, Virus, Stagger 등 상태/원소 반응 구조가 존재한다. 핵심 조합 선별과 UI 가독성 작업이 필요하다. |
| AI | Partial | 단순 추격을 넘어 ActionBid, AP 고려, 킹 가치, 일부 점수화 흐름이 존재한다. GDD의 Danger, Setup Kill, Taunt override는 시연 기준으로 재정렬해야 한다. |
| Prediction/UI | Partial | Prediction 관련 런타임/어댑터 구조와 Battle selection overlay가 있다. GDD의 prediction ghosting과 상태 가독성 UI는 아직 시연 품질이 필요하다. |
| Replay/Debug | Implemented | Snapshot, replay verification, divergence detection, diagnostics/debug overlay 계열 코드가 있다. 개발 안정화에 활용 가능하다. |
| Testing | Partial | `SimulationDivergenceTests` 등 일부 테스트가 있다. AP, 체스 이동, Splat, 상태 반응의 회귀 테스트는 더 보강해야 한다. |

## GDD Alignment

| GDD Source | Current Interpretation |
|---|---|
| Part 1 | 전원 배치 시작, AP 경제, PC 와이드 보드 인지를 프로젝트의 기본 전투 방향으로 반영한다. |
| Part 2 | Swamp, Spikes, Sanctuary, Weight, Knockback, Splat, Physical Status, Elemental Synergy, CC Matrix를 전술 레이어로 반영한다. |
| Part 3 | 체스 직군별 AP 비용과 이동/역할 차이를 AP 위치전의 밸런스 기준으로 삼는다. |
| Part 4 | AI scoring/override, prediction ghosting, AP 부족 피드백, 상태 가독성 UI를 졸업작품 시연 품질의 핵심 UX로 삼는다. |

## Key Design Numbers

| Rule | Target |
|---|---|
| Swamp | 이동 AP 비용 2배 |
| Spikes | 매 1초마다 Max HP 3% True Damage |
| Sanctuary | 아군 방어력 15% 증가, 초당 HP 2% 회복 |
| Splat | Max HP 10% True Damage |

## Known Risks

- Unity `.meta` 파일 미추적 상태가 많아 씬 참조와 ScriptableObject GUID 안정성 확인이 필요하다.
- 기존 문서 일부는 과거 구현 상태를 기준으로 작성되어 현재 코드와 맞지 않는다.
- `BattleTest`가 Fresh Clone 환경에서 곧바로 열리고 Play 가능한지 검증해야 한다.
- 상태/원소 반응은 종류가 많으므로 졸업작품 범위에서는 핵심 조합을 먼저 고정해야 한다.
- AI는 장기적으로 복잡해질 수 있으므로, 시연용 encounter 기준의 행동 품질을 먼저 잡는 편이 안전하다.

## Recommended Next Focus

1. `BattleTest` 실행 안정성, `.meta` 추적 상태, 씬 참조를 먼저 잠근다.
2. AP 위치전의 기본 루프를 명확히 만든다: 선택, 이동, 공격, AP 부족 피드백, 킹 패배 조건.
3. 넉백/Splat은 AP 위치전 위의 대표 콤보로 시연 가능하게 만든다.
4. 상태/원소 반응은 2~3개 핵심 조합만 먼저 선택해 가독성을 검증한다.
5. 최종 제출 전에는 디버그 토글, 테스트, 문서를 시연 루트에 맞춰 정리한다.
