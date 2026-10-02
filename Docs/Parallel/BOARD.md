# 병렬 작업 보드

## 2026-10-01 원경 안개와 하늘
- ruins-sky-fog | Codex | Review | 절차적 HDR 하늘·구름, Exp2 안개, 원경 도시 통합 메시. 4K 렌더 및 Play 진입/종료 확인. `Tasks/ruins-sky-fog.md`, `../CombatRuinsAtmosphere.md`.

## 2026-10-01 침수 유적 무드 볼륨
- ruins-mood-volume | Codex | Review | BattleScene 전용 URP 볼륨 적용, 컴파일 및 배경 Play 확인/종료. 현재 편성 부재로 캐릭터 색감 확인 잔여. `Tasks/ruins-mood-volume.md`, `../CombatRuinsVolume.md`.

## 2026-09-09 C01 리뷰 후 배정

- 기존 attack-move-grace: Review 유지. 인계 기준 67/68 테스트 통과, Play Mode 미실행. 메인 정적 리뷰에서 실전/예측 사거리 규칙 불일치 및 강제 이동 뒤 기존 유예 기록 잔존 확인.
- C01 리뷰 수정: ZCode / GLM-5.3-Flash에 자동 호출 시작. 작업서 `Tasks/c01-attack-grace-review-fixes.md`, 전용 워크트리 `.worktrees/zcode-c01-grace-review-fixes`, 기준 `01a5417` (코드 기준 aa0637a + 작업서).
- 첫 실행 `20260908T173622Z-c3fe3630`은 미지원 CLI 옵션으로 Failed, 변경 0건. 연결기 수정 및 단위 테스트 9개 통과 후 재배정: `20260908T173804Z-cd1d276e`. 상태는 `.parallel/runs/<실행ID>/result.json` 확인. 실행 시작은 기능 완료나 검증 통과를 뜻하지 않는다.
- 해당 작업서의 전투 코어·테스트 파일은 ZCode 단독 소유. 원본 코드·물 리소스·씬 및 기존 사용자 변경은 이번 배정에서 수정하지 않는다. Unity 최종 검증은 메인 담당.

메인 개발자만 갱신한다. 기준일: 2026-09-08.

회의 후 총괄 지시: [개발 명령서](DEVELOPMENT_ORDERS.md). 제안 16 보류, 18 조사 전용, 나머지 유지. 개별 작업은 Draft / 미배정이며 이 문서 작성으로 AI 호출·자동 실행을 시작하지 않았다.

| 작업 | 담당 | 상태 | 소유 파일 / 다음 단계 |
|---|---|---|---|
| 침수 유적 보드·씬 준비 | Codex | Verified (아트 초안) | 별도 BaseMap 편집본 실제 보드 적용, 단상·계단·기둥·아치·청록 수면 블록아웃. Battle/신규 씬 Play와 콘솔 확인. 최종 건축/식생/전투 통합은 후속. `Tasks/sunken-court.md`, `../SunkenCourt.md` |
| 서브컬쳐 풍 배경 셰이더 | Codex | Verified (체스보드 적용) | 실제 RuinTiles/TileRock/TileMetal 64칸 적용. 기존 atlas/normal/mask 유지, 금속칸 색 분리. BattleScene 기물·이동 테두리 Play 렌더/콘솔 검증. 기존 테스트 109개 중 별개 Poison 1건 실패 기록 유지. `Tasks/stylized-background.md`, `../StylizedBackground.md` |
| 얕은 물 셰이더 | Codex | Verified | 레퍼런스 후속 및 씬 이동 투명도 보정 완료: 4중 변위/굴절, Beach/Lagoon, portable prefab, 카메라 Depth/Opaque 자동 설정, 컴파일/Play 확인. 이전 전투 회귀 67/68(별개 Poison 내림 실패). `Tasks/shallow-water.md`, `../Water.md` |
| 병렬 개발 기반 | Codex | Verified | 도구 단위 테스트 8개 + 워크트리/모델 연결 검증. 게임 코드 변경 없음 |
| GLM 작업 큐 | Codex | Ready | `Docs/Parallel/GLM_TASK_QUEUE.md`; 우선순위·병렬 가능 여부·복사 가능한 지시문 정리 |
| M1 정식 Play Mode 검증 | 사용자 | Verified | 적 MaxAP 100 확인. 정식 진입·전투·승패·복귀 정상, 콘솔 오류 보고 없음 |
| M2 대표 넉백/Splat 콤보 셋업 | ZCode / GLM-5.3-Flash | Review | `codex/zcode/bootstrap`에 Editor 파일 1개(+.meta)와 인계서 배치 완료. 에디터 빌드 통과, Play Mode 확인 대기 |
| M3 상태 효과 회귀 테스트 | Antigravity / Gemini 3.1 Pro (High) | Review | `codex/antigravity/bootstrap`에 테스트 1개(+.meta)와 인계서 배치 완료. Unity Test Runner 확인 대기 |
| antigravity-bootstrap | Antigravity / Gemini | Draft | 연결 확인용. 기능 작업 미배정 |

## 첫 배정 전 확인

- 원본 프로젝트의 적 유닛 데이터 8개는 기존 미커밋 변경으로 보존한다. `Assets/Data/Units/Enemies/Enemy_BlackKnight.asset`, `Enemy_BlackPawn.asset` 및 `Assets/Resources/Units/Enemies/Enemy_Black{Bishop,King,Knight,Pawn,Queen,Rook}.asset`이다.
- 이 데이터는 bootstrap 워크트리에 포함되지 않는다. 관련 전투·밸런스 과제를 배정하기 전에 메인이 변경을 검토하여 기준에 포함할지 결정한다.
- 기능 과제는 `Docs/MILESTONES.md`의 현재 우선순위에서 선정한다. 이 보드의 대기 행은 구현 지시가 아니다.

## 2026-09-27 UI 아트 디렉션
- camera-ui-pass | Codex | Review | Cinemachine 조작/시작·승리 연출, UI 레이어·입력 관통·중복 캔버스 수정. 171/171 및 Play 확인. Handoffs/camera-ui-pass.md.
- ui-command-pass | Codex | Review | 칙령 보드/전투 정보창, 배치 비용 제거 및 편성 비용 표시. 144/144 테스트 및 후속 컴파일/Play 확인. Handoffs/ui-command-pass.md.
- ui-reference-pass | Codex | Review | 대원 카드 갤러리/상세, 칙령 카드 및 스크롤, OP 선택도. 전체 144/144 및 정식 씬 Play 확인. Handoffs/ui-reference-pass.md.
- ui-art-direction | Codex | Review | Handoffs/ui-art-direction.md. UI 적용, 122/122 테스트, 정식 경로 Play 확인. 기존 스킬/물 오류 잔존.
- economy-system | Codex | Review | Handoffs/economy-system.md. 4-Tier 재화 저장·회복·충전·레벨/공명·OP-01~05 및 자원 작전 보상·40인 정규 소집 풀 적용, 144/144 통과.
- confirmed-battle-rules | Codex | Review | 기본 편성 기준 상한 20/기물 비용, 킹 자동 배치/철회, 수동·자동 SP와 5성 스킬 카메라. 최초 규칙 179/179 및 정식 경로 Play 확인, 20 상향 후 컴파일 확인. Handoffs/confirmed-battle-rules.md.
- ruins-connected-layout | Codex | Verified (배경 배치) | BattleScene 기존 배치 보존, RuinsBackgrounds 198개로 회랑/광장/계단 연결. 컴파일·렌더·Play 오류 0 및 종료 확인. 기존 덱 예산 경고로 전체 전투 미검증. ../CombatRuinsConnections.md.

## 2026-10-02 수중 유적 확장
- ruins-submerged-modules | Codex | Verified (배경 배치) | 수중 배경 확장 및 반복 구조 프리팹 모듈화. Tasks/ruins-submerged-modules.md.


## 2026-10-02 수면 디테일
- water-detail | Codex | Verified (수면 렌더) | 픽셀 파도 노멀, 잔물결 거리, 청록 투과/카우스틱, 접촉 포말. 컴파일/Play 확인 및 종료. ../WaterDetail.md.

## 2026-10-02 수면 완성도 보강
- water-polish | Codex | Verified (수면 렌더) | 밝은 수색/깊이 분리, 실제 유적 평면 반사, 여러 시점과 수명주기 검증, 179/179. ../WaterDetail.md.


## 2026-10-02 물살 제어
- water-motion | Codex | Verified | ShallowWaterMotion 적용. 공유 재질 보존, 185/185, Play/정지·재개 및 콘솔 확인. ../WaterMotion.md.

- water-polish reference follow-up | Codex | Verified | 재질의 물결/반사/접촉 포말/카우스틱 조정. 사용자 수색 보존, 물 테스트 6/6 및 정식 경로 Play/종료 확인. Docs/WaterDetail.md.

- water-terrain-cover | Codex | Verified | 수면 1064×1064m 확장, 기존 수위 유지. Terrain 전체 XZ 포함 및 정식 경로 Play/콘솔/종료 확인.
