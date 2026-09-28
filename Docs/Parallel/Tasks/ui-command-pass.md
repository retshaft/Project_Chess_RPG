# Command UI follow-up

- 상태: Review
- 담당: Codex 메인, 원본 codex/integration에서 순차 구현
- 기준: 6e0e3d7c609649a1ec073ac9263444cddddf2604 및 이전 UI 패스 미커밋 변경 보존
- 작업 경로: C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01
- 허용 파일: Assets/Scripts/UI/BattleHudView.cs, SynchroBoardUI.cs, EdictSelectorModal.cs; Assets/Scripts/Core/BattleManager.cs는 명령 버튼 표시/배치만; Docs/Parallel 작업 기록; Captures/UICommand.
- 목적: 칙령 장착 보드의 수용량/카드/효과 가독성, 전투 선택 유닛의 초상/기본 능력/HP/SP/스킬 정보 구획.
- 금지: 전투/SP 규칙, 저장, 에셋 데이터, 씬, 패키지 변경. 부관 복원 금지.
- 완료: Unity 컴파일, 관련 회귀 테스트, OutgameScene 및 BattleScene Play 검증 후 종료.
- 사용자 후속 요청: 배치 코스트 표시 제거, 편성에서 대원별/합계 코스트 확인. DeckBuilderUI.cs와 RosterSlotUI.cs 표시 수정 허용. 상한이나 전투 규칙 추가 금지.
