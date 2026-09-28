# 플레이 흐름 완성도 개선
- 상태: Ready
- 담당: Codex 메인, 원본 codex/integration 순차 작업
- 기준: 6e0e3d7 및 기존 미커밋 변경 보존
- 허용: BattleManager.cs, StageData.cs, DeckBuilderUI.cs, StageSelectUI.cs, BattleResultUI.cs, BattleHudView.cs, Testing/EditModeTests/PlayabilityRegressionTests.cs 및 meta, Docs/Parallel 기록, Docs/PLAYABILITY_QUESTIONS.md, Captures/Playability.
- 범위: 편성 해제/선택 피드백, 출격 전 콘텐츠 검증, 킹 배치 시작 조건, 목표/배치 피드백. 기획 질문 문서화.
- 금지: 미정 수치 확정, 적 배치/스킬 규칙 임의 확장, 사용자 저장 변경, 기존 작업 폐기.
- 검증: Unity 컴파일, EditMode 회귀, 공식 씬 Play 확인/종료.
- 추가 조사 반영: OP-01~04 에셋의 EnemySpawns만 기존 STG 1-1~1-3/BOSS 1-4 진형을 재사용해 연결. 능력치/보상/목표 변경 금지. 빈 OP-05와 자원 작전은 미확정 유지.
- Play 후속: HealthComponent.cs 초기화 알림 누락 수정, AITeamCommander.cs 개발용 AP 표시는 선택 옵션으로 변경. 편성 선택 모달 스크롤/카드 배치 수정.
