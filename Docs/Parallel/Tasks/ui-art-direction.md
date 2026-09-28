# UI art direction — monochrome tactical interface

- 상태: Review (UI 검증 완료, 기존 에셋/렌더링 오류 잔존; Handoffs/ui-art-direction.md 참조)
- 담당: Codex (메인 직접 구현, 외부 AI 배정 없음)
- 브랜치: codex/integration
- 워크트리: C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01
- 코드 기준 커밋: c8f5bda23ab5f917177097ebbd82a7d3e2f68319
- 선행: UI_SPECIFICATION.md 및 명일방주풍 명세 검토. 최신 사용자 요청에 따라 기존 네온 팔레트 수정.

## 허용 파일
- Assets/Scripts/UI/*.cs 및 새 UI 컴포넌트의 .meta
- Assets/Scripts/Core/BattleManager.cs (메인 단독: UI 생성부 색상과 버튼 표시만, 전투 규칙 변경 금지)
- Assets/Resources/UI/ 및 .meta (신규 부관 원화)
- Assets/Scripts/Testing/EditModeTests/OutgameNavigationTests.cs 및 .meta
- Assets/Scripts/Testing/EditModeTests/OutgameRuleRegressionTests.cs (장식 제목 대신 실제 수용량 미터를 검증하도록 조회 대상 명시)
- Docs/UI_SPECIFICATION.md, Docs/[Checkmate RPG] 명일방주풍 UIUX 시스템 및 화면 흐름 개발 명세서.md
- Docs/Parallel/BOARD.md, 본 작업서, Docs/Parallel/Handoffs/ui-art-direction.md
- Captures/UIReview/ (검증 이미지)

## 경계
씬 재생성, 전투 규칙, 저장 구조, 데이터 에셋, 패키지·설정 변경 금지.
기존 폰트 fallback 에셋 및 Assets/Screenshots 사용자 변경 보존.

## 완료 조건과 검증
무채색 중심 색상, 로비·타이틀 비대칭 구도, 일관된 하위 화면과 모달, 명확한 선택/출격 표시.
정식 아웃게임 Play Mode에서 화면·모달·편성·출격 흐름 확인, 콘솔 및 기존 EditMode 테스트 확인.
동일 원본 프로젝트의 이전 실행권과 실행 중 프로세스/테스트 상태 확인 후 메인 실행권 재획득.
게임 규칙과 문서의 불일치는 임의로 변경하지 않고 인계서에 보고.
