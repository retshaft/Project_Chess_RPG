# UI art direction 인계

- 담당: Codex 메인 직접 구현
- 브랜치/워크트리: codex/integration / C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01
- 기준 및 현재 HEAD: c8f5bda23ab5f917177097ebbd82a7d3e2f68319
- 코드 커밋: 없음. 검토 가능한 미커밋 변경으로 보존.
- 상태: Review — UI 적용 및 검증 수행, 프로젝트 전체 오류 0건 기준은 기존 오류로 미충족.

## 변경 결과

차콜·회백색·황토색 중심으로 팔레트, 타이틀, 로비, 공통 헤더, 작전 브리핑, 8인 편성, 대원 관리 4개 탭, 칙령 회로/선택 모달, 채용, 슈트, 기록 화면을 정리했다. 전투 배치/행동 버튼과 결과 화면도 같은 색상 체계로 맞췄다. 배치 비용 표시의 최소 높이를 보장했다.

로비 편성 메뉴가 대원 관리로 열리던 연결, 화면 이동 뒤 남는 모달 뒤로가기 처리, 칙령 선택창 ESC 처리, 빈 칙령 슬롯의 잘못된 장착 수 표시를 수정했다. 채용은 실제 지급 기능이 없는 현재 구현에 맞춰 시험 통신임을 명시했다. 전투 규칙·씬·프리팹·저장 구조는 변경하지 않았다.

부관은 사용자가 직접 선택한다는 최신 기획을 문서에 명시했다. 현재 원화는 임시 리소스이며 최신 지시 이후 수정하지 않았다. 직접 설정 UI/데이터 연결은 보류했다. 현재 실제 편성은 8칸이며 과거 문서의 6칸에 맞춰 규칙을 변경하지 않았다.

## 검증

| 항목 | 결과와 근거 |
|---|---|
| 컴파일 | Unity 6000.3.10f1에서 컴파일 오류 없음. 마지막 배치 비용 레이아웃 수정 후 재컴파일 및 Play 확인 |
| Edit Mode | CheckmateRPG.Tests 122/122 통과. job 74030fac45b74b5f836f54a18076ca0c. 그 이후 변경은 배치 비용 Text 최소 높이/레이캐스트 설정 2줄 |
| 아웃게임 | 로비 7개 메뉴와 부관 영역 레이캐스트, 대원 4개 탭, 편성/인벤토리, 칙령 모달 ESC, 채용 피드백, 슈트/기록 확인 |
| 정식 출격 | OutgameScene → STG 1-1 브리핑 → 편성 → Test/BattleScene → 킹 드래그 핸들러 배치 → 전투 개시/공격 버튼 → 결과 미리보기 작전 복귀 확인 |
| 결과 | 실패 결과 실제 표시 및 별도 런타임 미리보기 확인. 승리 보상 지급 전 과정은 이번 UI 검증 대상으로 실행하지 않음 |
| 콘솔 | 새 UI 예외는 관찰되지 않음. 아래 기존 에셋/렌더링 오류가 남아 전체 통과라고 보고하지 않음 |
| 범위 | git diff --check 통과. 기존 폰트 fallback 및 Assets/Screenshots 변경 보존 |

도구 코드에서 아직 생성되지 않은 BattleResultUI를 조회한 1회 NullReferenceException은 검증 도구 실행 실패였다. 실제 UI에서 발생한 예외와 구분한다. 이후 런타임 미리보기 컴포넌트로 결과 표시와 복귀를 확인했다. 전투 HUD 정지 화면은 렌더 확인을 위해 일시적으로 timeScale=0으로 촬영했고 종료 전 1로 복구했다.

### 남은 문제와 확인 범위

- KiaraActive1/2/3 AbilityDefinition의 직렬화된 ApplyBuffAbilityEffect / DealAreaDamageAbilityEffect 타입 참조 오류 3종.
- ShallowWaterSceneSupport.cs:26 카메라 컴포넌트 생성 시 SendMessage 오류.
- 물 셰이더 Enum material drawer 생성 오류. 씬을 다시 열면 동일 오류가 반복될 수 있다.
- 전투의 물/보드/캡슐 캐릭터 원화와 Enemy AP 디버그 표시는 이번 화면 UI 변경과 별개로 남아 있다.
- 검증 해상도는 1920×1080. 다른 종횡비 및 실제 장치 입력은 미검증.
- 정식 작전을 선택하지 않고 편성에서 바로 출격하면 스테이지가 없는 기존 경로로 연결된다. 이번 최종 전투 확인은 STG 1-1을 먼저 선택했다.

## 확인 방법

OutgameScene을 실행해 접속 시작 → 작전 출격 → STG 1-1 → 작전 출격 준비 → 출격 확정 순서로 이동한다. 전투 배치에서 킹을 아래쪽 칸으로 드래그한 뒤 전투를 시작한다. 흰색 선택 상태, 황토색 확정 버튼, 배치 비용 숫자 및 선택 유닛 카드가 표시되는지 확인한다.

## 캡처와 임시 원화

- Captures/UIReview/final-lobby.png
- Captures/UIReview/final-formation.png
- Captures/UIReview/final-operators.png
- Captures/UIReview/final-synchro.png
- Captures/UIReview/final-edicts.png
- Captures/UIReview/final-battle-deployment.png
- Captures/UIReview/final-battle-hud.png
- Captures/UIReview/final-battle-result.png

임시 원화: Assets/Resources/UI/CommandCenter.png. Built-in imagegen으로 생성. 현재는 사용자 요청으로 변경 보류.

생성 프롬프트: Create production background illustration for an original chess tactical anime RPG command-center lobby. Wide 16:9 1920x1080 composition. Premium Arknights-inspired restrained industrial anime art direction, sophisticated confident ink drawing and painterly shading, NOT an interface screenshot. An original adult female tactical liaison, silver bob haircut with charcoal hair clip, calm serious face, dark graphite oversized technical coat with off-white structured inner jacket, black gloves, small muted amber identification tab. Three-quarter body from head to knees occupying left third / left 45 percent, face at x32% y28%, holding thin tactical dossier, fabric wind movement. Behind her a brutalist concrete observation deck and misty distant industrial city, diagonal architectural beams and soft daylight. Desaturated warm grayscale, ink black, stone white, subtle warm ochre accents only, paper grain and architectural depth. Right 48 percent is quiet pale gray concrete wall / fog with very low detail so real game menu buttons can be overlaid. Bottom softly darker. No cyan, no neon, no purple, no holograms, no glowing edges, no text, no letters, no logos, no UI widgets. Beautiful strong silhouette and excellent face/hands. This is background artwork not a mockup.

## 종료 확인

- 허용 범위 변경 및 신규 .meta 확인. BattleManager는 UI 생성부에만 변경.
- 다른 워크트리, main, 공유 설정 변경 없음.
- Play Mode 종료 확인. Unity 실행권 반납은 최종 도구 결과 기준.
- 기존 변경, 미커밋 상태와 미검증 항목을 위에 명시.
