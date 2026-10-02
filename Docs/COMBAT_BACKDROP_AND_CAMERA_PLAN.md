# 컴뱃 씬 배경 데코 & 전투 중 카메라 연출 계획서

- 작성일: 2026-09-29
- 상태: **계획 (구현 전)**
- 목표: `Assets/Scenes/Test/BattleScene.unity`을 첨부된 무드 이미지(잠긴 기계 유지 위의 체스보드, 청록빛 물, 안개 낀 원경) 분위기로 꾸미고, **전투 중** 카메라가 천천히 움직여 배경이 조금씩 시야에 들어오게 한다. 인트로는 짧게(1~1.5초)만.

---

## 1. 현재 상태 (조사 완료)

| 항목 | 상태 |
| --- | --- |
| BattleScene 구성 | Main Camera(고정), Directional Light, GridSystem, 타일 64개(`Tile_x_y` + TileRock/TileMetal 자식), `WaterPlane`, 반사 프로브. **배경 오브젝트 0개** |
| 카메라 | 위치 (8, 16, −3), 회전 X 60°, FOV 60, 단색 클리어. 컨트롤러 없음(정적). `CheckmateCinematicUI`의 FOV 트윈 패턴만 존재 |
| 렌더 설정 | 안개 꺼짐, 기본 스카이박스, 앰비언트 기본값 |
| 보드 타일 머티리얼 | `Assets/Resources/Materials/RuinTiles.mat` — `Checkmate/Environment/StylizedBackground` 셰이더. 거리 헤이즈(Start/End) 파라미터 내장, 현재 Haze 0 |
| 물 | `Assets/Resources/Prefabs/ShallowWaterPortable.prefab` + `Checkmate/Water/Shallow Water` 셰이더(투명, 굴절, 카우스틱). **카메라에 URP Depth+Opaque 텍스처 필요**(`ShallowWaterSceneSupport.ConfigureCameras` 참고) |
| 카메라 소비처 | `BattleManager._inputCamera`, `Input/GridRaycaster.cs`(Camera.main + ScreenPointToRay 픽킹) → **연출 후 원래 시점 복귀 필수** |

### 자산 인벤토리 (전부 기존 것, 신규 제작 불필요)

| 용도 | 경로 |
| --- | --- |
| 유적 프리팹 13종 (권장 소스) | `Assets/Environment/MechanicalRuins/Architecture/ReferenceVariant/Prefabs/` — 스톤/이끼 스톤/청동/잎 4재질 완비 |
| 유적 FBX 원본 | `Assets/Resources/Models/Ruin/` (BigGear, BrokenBigGear, FortressPillar(+Head), LowWall, NarrowConnecter, Path, Pillar, Platform, RuinCenter, SmallPillar, Stairs) |
| 체스 기물 FBX | `Assets/Resources/Models/ChessUnits/` (MidKing 등 6종) — 이번 작업 범위 외(기물 교체는 별도) |
| 블록아웃 청사진 | `Assets/Environment/SunkenCourt/SunkenCourt_Blockout.unity` + 빌더 `Assets/Scripts/Editor/StylizedBackground/SunkenCourtSetup.cs` — 레이어 구성(보드/플랫폼/파일런/실루엣/잠긴 잔해/물)을 그대로 참조 |
| 유적 머티리얼 | `Assets/Environment/SunkenCourt/Materials/` (AgedLimestone, CourtWater, OldBronze, SubmergedFloor, WetStone), `Assets/Environment/MechanicalRuins/Architecture/Materials/` |
| 식생 키 | `MechanicalRuins/Prefabs/`의 MossPatch, GrassCluster(+(MechanicalFloor)) |
| 타일 백업 | `Assets/Environment/StylizedBackground/RuinTilesOriginal.mat` |

**핵심 주의**: 유적 FBX는 블렌더 원본 크기가 거대(Crank ~183m, BigGear 지름 ~49.5m). 프리팹 배치 시 반드시 스케일 보정. UV 리빌드 작업은 **기각 상태**이므로 기존 프리팹/머티리얼을 그대로 쓰고 재텍스처링은 하지 않는다.

---

## 2단계 구성 — 1단계: 배경 데코 (정적 씬)

### 방식
손으로 씬 YAML을 편집하지 않고 **에디터 빌더 스크립트**로 배치한다(실행할 때마다 재현 가능, 실수 시 재생성).

- 신규: `Assets/Scripts/Editor/StylizedBackground/BattleSceneBackdropBuilder.cs`
- 메뉴: `Tools/Checkmate/Background/Dress Battle Scene Backdrop`
- 동작: 씬에 `Backdrop` 루트를 만들고 하위 레이어를 배치. **이미 `Backdrop`이 있으면 삭제 후 재배치(멱등)**. 끝나면 씬 저장.
- `BattleScene.unity`를 열어 작업하고, 종료 시 원래 열려 있던 씬(OutgameScene)으로 복구.

### 배치 레이어 (보드: 8×8, 셀 2m, 월드 X/Z 0~16, 중심 (8, 0, 8))

| 레이어 | 내용 | 배치 가이드 |
| --- | --- | --- |
| L1 근경 (반경 ~11–13m) | 코너 파일런 4(FortressPillar+FortressPillarHead), 대각 코너 LowWall 4, 남쪽 Stairs+Platform(보드 접속 인도 시작), MossPatch 6–8, GrassCluster 3–4 | 보드 가장자리에서 1.5–3m 이격, 타일 위 그림자 범위 고려 |
| L2 중경 (반경 ~16–22m) | BrokenBigGear 반파(지면에 기울여 반쯤 잠긴 느낌), SmallPillar 군 2–3곳, Path/NarrowConnecter로 남쪽 잠긴 인도(Causeway) 라인 완성 | 보드에서 보이는 방향(카메라 반대편 남쪽) 위주 |
| L3 원경 (반경 ~26–40m) | FortressPillar 6–8(높이 스케일 1.5–2×), RuinCenter 1, BigGear 1(반매설), Platform 실루엣 | 크게 + 안개로 실루엣화. 배치만 하면 안개가 나머지 처리 |
| L0 분위기 | 안개/스카이박스/앰비언트/물/헤이즈 (아래 표) | RenderSettings + 카메라 + 머티리얼 설정 |

### 분위기 파라미터 (무드 이미지 기준 시작값, 플레이 보며 미세 조정)

| 항목 | 값 |
| --- | --- |
| 안개 (RenderSettings) | Linear, Start 25 / End 120, 색 청록–하늘색 계열(예: #7FA8B8 근방) |
| 스카이박스 | 절차적(Procedural) — 옅은 하늘색(#A8C4D4 계열), 태양 고도는 Directional Light와 일치 |
| 앰비언트 | Trilight(하늘=하늘색, 지면=청록 물빛 반사톤) |
| 물 | WaterPlane이 ShallowWater 머티리얼인지 확인(이미 적용돼 있으면 재사용). Main Camera에 Depth Texture + Opaque Texture ON |
| 보드 헤이즈 | `RuinTiles.mat` Distance Haze Start 30 / End 130 — 원경과의 거리감 부여(백업 mat 존재) |
| 라이팅 | Directional Light를 이미지의 오후 사광으로(고도 ~35°, 약간 웜톤). 실시간만 유지(베이크는 후순위) |

후순위(v2): 이미지 속 깃발/현수막(모델 없음 — 천 쿼드로 신규 제작 필요), 라이트 프로브/베이크, LOD.

---

## 2단계 구성 — 2단계: 전투 중 카메라 연출 (런타임)

### 신규 1개 컴포넌트
`Assets/Scripts/Core/BattleCameraDirector.cs` (MonoBehaviour, 코루틴 트윈 기반 — Cinemachine 등 외부 의존성 없음, `CheckmateCinematicUI`의 unscaled-time 트윈 패턴 준용)

### 상태 머신

| 상태 | 동작 |
| --- | --- |
| Deployment | 정지 — 기존 게임플레이 시점 고정(입력 방해 없음) |
| Intro (전투 시작, 1.2초) | 살짝 높은/옆 시점에서 게임플레이 시점으로 ease-out 안착. **클릭 시 즉시 스킵** |
| Battle (전투 중) | **메인 연출 — 느린 궤도 드리프트** (아래 파라미터) |
| Ended | 드리프트 정지 — 기존 `CheckmateCinematicUI` FOV 줌 연출과 충돌하지 않게 카메라를 안정화 |

### 드리프트 파라미터 (초기값)

- 피벗: 보드 중심 (8, 0, 8). 기준 포즈 = 현재 게임플레이 시점(카메라 오프셋)을 피벗 기준 극좌표로 환산.
- 궤도 회전: 각속도 ω ≈ 2π/180rad/s(바퀴 1회 ~3분) — 보드 양옆·뒤 유적이 시야에 번갈아 흘러 들어옴.
- 높이 부유: sin ±0.35m, 주기 ~20s. 시선은 피벗 근처(8, 1, 8) 고정.
- 진폭 제한 이유: `GridRaycaster` 픽킹은 매 프레임 살아있는 카메라 포즈로 레이캐스트하므로 **회전 중에도 클릭은 그대로 동작**하지만, 가독성 보호를 위해 이동/기울기 폭을 소폭으로 제한.

### 통합 지점
- `BattleManager.StartBattle()` 끝에서 `director.PlayIntro()` 호출(1줄 훅). 배치→전투 전환과 함께.
- `BattleManager._inputCamera`는 그대로 Main Camera(디렉터가 같은 카메라 트랜스폼만 구동).
- 드리프트는 `BattlePhase.Playing`에서만 활성화. 배치/종료 상태에서는 정지.

### 안전 규칙 (검증 필수)
1. 인트로 종료 후 게임플레이 시점과 **정확히 동일한 포즈 복귀**(픽킹 보장).
2. `Camera.main` 태그 유지, 카메라 오브젝트를 새로 만들지 않고 기존 Main Camera 구동.
3. 인트로 중 타일 입력 무시(클릭 = 스킵으로만 소비).
4. 체크메이트 연출(기존 FOV 줌) 재사용 — 디렉터는 Ended에서 손 뗌.

---

## 3단계 구성 — 검증 체크리스트

- [ ] 컴파일 에러 0건 (Unity 콘솔 확인)
- [ ] Play Mode: 배치 화면 = 정지 뷰, 드리프트 없음
- [ ] 전투 시작: 인트로 1.2초 재생 → 클릭으로 스킵 가능
- [ ] 전투 중: 30초 이상 두면 배경이 시야 가장자리로 흐르는지 확인(스크린샷 2장 이상 비교)
- [ ] **드리프트 중 타일 클릭 → 이동/공격 정상**(픽킹 회귀 확인)
- [ ] 적 킹 처치 → 체크메이트 연출 + 결과창 정상(드리프트 정지 확인)
- [ ] 콘솔 런타임 예외 0건 → Play Mode 종료 → 에디터 씬 OutgameScene 복구
- [ ] 씬 파일 git diff가 `Backdrop` 루트 + RenderSettings + 카메라 설정 범위인지 확인

## 파일 변경 목록 (예상)

| 구분 | 파일 |
| --- | --- |
| 신규 (에디터) | `Assets/Scripts/Editor/StylizedBackground/BattleSceneBackdropBuilder.cs` (+.meta) |
| 신규 (런타임) | `Assets/Scripts/Core/BattleCameraDirector.cs` (+.meta) |
| 수정 (런타임, 소량) | `Assets/Scripts/Core/BattleManager.cs` — StartBattle에 인트로 훅 1줄 |
| 수정 (씬) | `Assets/Scenes/Test/BattleScene.unity` — Backdrop 루트, RenderSettings, 카메라 Depth/Opaque 텍스처 |

게임 규칙·예측·리플레이 경로는 건드리지 않는다(카메라는 시각 계층만).

## 리스크

1. **FBX 스케일** — 원본이 수십~수백 m급. 빌더에 모듈별 목표 스케일 상수를 두고, 첫 배치 후 씬 뷰에서 1회 눈대중 캘리브레이션 필요.
2. **물 셰이저 카메라 요구사항** — Depth/Opaque 텍스처 끄면 물이 깨져 보임. 빌더가 카메라 설정을 강제로 켜도록 처리.
3. **드리프트 + 픽킹** — 진폭이 크면 조작감 해침. 초기 진폭은 보수적으로 잡고 플레이 감으로 조정.
4. **체크메이트 연출 충돌** — Ended 상태에서 드리프트를 즉시 정지해 FOV 줌과 겹치지 않게 함.
