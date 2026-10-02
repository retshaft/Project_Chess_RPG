# UI 레이어 / Cinemachine 조작 — 2026-09-30

기준 ef13042, codex/integration 원본 순차 작업. 기존 폰트 변경과 미추적 배경 계획 문서 보존. 씬/패키지 버전/사용자 저장 데이터 변경 없음.

## 변경
- 설치된 Cinemachine 3.1.7을 런타임 어셈블리에 연결. BattleManager가 BattleCameraDirector와 Tactical/Cinematic 가상 카메라를 구성한다. 물리 Main Camera는 렌더/픽킹에 그대로 사용한다.
- 휠 줌(.75~1.4), 중간 버튼 팬, Q/E 회전(±65도), F 선택 기물 포커스, Home 기본 시점. 팬은 보드 중심 주변 4m 제한. 시작 연출 약 1초, 클릭/ESC 스킵. 연출 중 Tick/AP 회복 정지, 이전 상태 복원, 스킵 입력 프레임 소비.
- 체크메이트 FOV 연출을 Cinemachine 렌즈에 연결. 종료/중단 시 렌즈 및 시간 배속 복원. 체크메이트/킹 싱크로 중복 캔버스 생성 방지. 컷인 취소 정리 추가.
- UI 순서: HUD 20, 킹 싱크로 21, 모달 100, 대화상자 200, 연출 300, 결과 400. 편성/브리핑/칙령 팝업은 열 때 nested canvas override를 적용한다(비활성 시 초기값이 풀리는 실제 동작 확인 후 보완).
- 새 GraphicRaycaster 기반 포인터 검사로 팝업/명령/정보 패널 입력 관통 차단. 드래그 고스트 이름표 raycast 해제. BattleManager/레거시 PlayerInputController 중복 입력 방지.
- 전투 1/2/3 이동/공격/스킬 전환, 우클릭/ESC 선택 취소. 배치 중 명령 패널 노출 방지.

## 검증
- 전체 CheckmateRPG.Tests 171/171 통과: 063fe2be3a07420f993c1e82bebc31f3. 새 테스트 4개(모달 멱등/체크메이트 단일 캔버스/킹 싱크로 단일 캔버스/카메라 제한과 복귀).
- 최종 HUD raycast 및 종료 시 scheduler 생성 방지 보완 후 컴파일 별도 확인.
- OutgameScene Play: 편성 모달 order=100, overrideSorting=true, 헤더 위 표시 및 배경 클릭 차단. Captures/CameraUI/modal-layer-final.png.
- 정식 BattleScene: 실제 배치 콜백으로 킹 배치 성공, CinemachineBrain/가상 카메라 활성 확인.
- 시작 연출 자연 종료 및 스킵: Tick/AP 정지/재개, 스킵 프레임 BlocksCommands=true 확인.
- 회전 25도/팬/줌0.9 적용 후 물리 카메라 이동 확인. 셀 (4,3)을 투영하고 레이캐스트한 결과 (4,3) 일치. Captures/CameraUI/orbit-view.png.
- 입력 이벤트 큐로 Digit2 → Attack, Escape → 선택 null/Move 확인. HUD 카드/명령 영역 UI 차단 true.
- 체크메이트 표시 전용 연출 콜백 완료, 단일 캔버스, timeScale=1/FOV=60 복원 확인. 이번 검증은 승패 보상 지출 없이 화면/조작 경로만 확인했다.
- 이번 최종 Play 구간 콘솔 오류 0건. 기존 다른 스킬/셰이더 자산 전체가 복구됐다는 의미는 아님.
- Play 종료 후 OutgameScene 상태 복구 및 Unity 실행권 반납.

## 남은 판단
- 자동 배경 궤도 회전을 기본 동작으로 할지, 직접 카메라 조작만 유지할지 사용자 확인 항목이다. 현재는 고정 기본 시점과 직접 조작을 제공한다.
- 개별 액티브 스킬의 클로즈업/히트 타이밍 연출은 SP/스킬 발동 규칙 정리 이후 연결한다. 이번에는 전투 시작/체크메이트 전환을 구현했다.
- 배경 데코 계획의 프리팹/조명/물 아트 수정은 실행하지 않았다.
