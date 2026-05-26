# Checkmate RPG Milestones V2

> 목표: 졸업작품 제출 품질의 AP 기반 실시간 체스 액션 전투 완성
>
> 범위: 모든 GDD 기능의 전면 구현이 아니라, 시연 가능한 전투 루프와 핵심 전술 경험 완성

## Guiding Priority

개발 우선순위는 다음 순서로 둔다.

1. AP 위치전
2. 넉백 콤보
3. 원소/상태 다양성
4. 성장 메타

초기 마일스톤은 **AP 기반 실시간 위치전**을 먼저 잠그고, 넉백/상태/원소는 그 위에 얹는 방식으로 진행한다.

## M1 Current Baseline & Playability Lock

**Goal:** 현재 프로젝트를 안정적인 기준선으로 고정한다.

### Deliverables

- `BattleTest`가 Fresh Clone 환경에서 열리고 Play 가능한 상태
- 현재 `.meta` 파일 추적 상태 점검 및 누락 위험 목록화
- 현재 구현 현황과 과거 문서의 차이를 팀이 이해할 수 있는 기준 문서
- 시연 중 켜고 끌 수 있는 최소 디버그/진단 경로

### Acceptance Criteria

- Unity 6000.3.10f1에서 프로젝트가 Safe Mode 없이 열리는지 확인한다.
- `BattleTest`에서 기본 유닛 배치, AP UI, 이동/공격 흐름이 확인된다.
- 누락된 `.meta` 또는 씬 참조 위험이 별도 작업 항목으로 정리된다.
- 기존 `MILESTONES.md`와 `DEV_QUICKSTART.md`가 과거 기준 문서임을 팀이 인지한다.

## M2 AP Chess Core

**Goal:** 체스식 이동과 AP 경제를 하나의 플레이 가능한 전투 루프로 묶는다.

### Deliverables

- 전원 배치 시작 구조 확정
- Pawn, Knight, Bishop, Rook, Queen, King 이동 규칙 검증
- 이동/공격/스킬의 AP 소모, 자연 회복, 부족 피드백 정리
- 킹 사망 또는 킹 위협을 중심으로 한 승패 조건 초안
- PC 조작 기준 선택, 이동 가능 구역, 타겟 지정 흐름 정리

### Acceptance Criteria

- 플레이어가 기물을 선택하고, AP를 보고, 유효 행동을 판단할 수 있다.
- AP가 부족하면 중앙 AP 피드백으로 즉시 이해할 수 있다.
- 각 기물 이동 규칙이 8x8 보드에서 일관되게 동작한다.
- 한 판의 기본 목적이 명확하다: 킹 보호/제압 또는 이에 준하는 시연 목표.

## M3 Tactical Physics Layer

**Goal:** AP 위치전 위에 넉백, 무게, 충돌 전술을 얹는다.

### Deliverables

- Weight 0~4 등급 기준 정리
- Knockback 거리 계산과 Stagger 연계 규칙 정리
- Splat 처리 확정: 벽/기물 충돌 시 Max HP 10% True Damage
- Grab 처리 확정: Stagger 대상 후속 공격 치명타 연계
- Swamp, Spikes, Sanctuary의 전투 영향 연결

### Acceptance Criteria

- 적을 벽 또는 다른 기물 쪽으로 밀어 Splat 피해를 유도할 수 있다.
- 연쇄 충돌은 발생하지 않는다.
- Swamp는 이동 AP 비용 2배, Spikes는 Max HP 3% True Damage, Sanctuary는 아군 방어력 15% 증가와 초당 HP 2% 회복을 따른다.
- 넉백 콤보가 AP 위치전의 보상으로 느껴진다.

## M4 Status & Element Readability

**Goal:** 상태효과와 원소 반응을 시연 가능한 핵심 조합 중심으로 정리한다.

### Deliverables

- 졸업작품 범위의 핵심 상태/원소 조합 2~3개 선정
- Chill, Freeze, Superconduct, Stagger 등 AP/물리 전술과 직접 연결되는 상태 우선 정리
- 상태 효과 지속 시간, 중첩, 갱신, 제거 기준 문서화
- 머리 위 상태 표시, 모델 오버레이, 타일/범위 피드백 기준

### Acceptance Criteria

- 플레이어가 0.1초 안에 중요한 상태를 읽을 수 있다.
- 상태/원소 반응이 AP 비용, 행동 가능 여부, 넉백 콤보와 연결된다.
- 조합 수가 과도하게 늘어나지 않고 시연 루트 안에서 반복 검증 가능하다.

## M5 Enemy AI & Encounter Slice

**Goal:** 시연용 encounter에서 AI가 전술적으로 보이게 만든다.

### Deliverables

- AP 보존/소모 성향을 반영한 AI scoring
- 킹 위험 회피 `Danger` override
- Splat 각을 보는 `Setup Kill` override
- 도발 대상 강제 공격 `Taunt` override
- 졸업작품 시연용 적 배치와 행동 패턴

### Acceptance Criteria

- AI가 단순 추격이 아니라 AP와 전황을 고려하는 것처럼 보인다.
- 킹이 위험하면 회피하거나 방어 행동을 우선한다.
- 넉백으로 벽몰이/Splat을 노리는 대표 상황이 최소 1개 이상 나온다.
- 시연 encounter는 과도한 랜덤성 없이 반복 재현 가능하다.

## M6 Graduation Polish

**Goal:** 제출과 시연에 필요한 안정성, 가독성, 문서 품질을 마감한다.

### Deliverables

- AP HUD, 이동 오버레이, prediction ghosting, 상태 가독성 UI 정리
- 튜토리얼성 안내 또는 시연 가이드
- 디버그 토글과 로그 노이즈 정리
- 핵심 회귀 테스트 보강
- 최종 문서 정리: 실행 방법, 구현 현황, 시연 루트, 알려진 제한사항

### Acceptance Criteria

- 처음 보는 사람이 짧은 안내만으로 시연 루트를 따라갈 수 있다.
- 시연 중 AP, 이동 가능 구역, 넉백 결과, 주요 상태효과가 즉시 읽힌다.
- 제출 빌드 또는 Play Mode 시연에서 치명적인 콘솔 오류가 없다.
- 문서와 실제 구현 상태가 서로 모순되지 않는다.

## Out of Scope for Graduation Slice

- BM/과금 중심 성장 설계
- 전체 캐릭터/직군 데이터 완성
- 모든 원소 반응과 모든 CC의 완전 구현
- 장기 라이브 서비스 밸런스
- 최종 아트/연출 리소스 완성

## Immediate Next Actions

1. `CURRENT_STATUS.md`를 기준으로 현재 위험 항목을 작업 티켓화한다.
2. `.meta` 파일 누락과 `BattleTest` 씬 참조를 먼저 검증한다.
3. M2 범위의 AP 체스 코어를 실제 플레이 루프로 재확인한다.
4. M3에서 시연할 대표 넉백/Splat 콤보를 하나 고른다.
5. M4에서 사용할 핵심 상태/원소 조합을 2~3개로 제한한다.
