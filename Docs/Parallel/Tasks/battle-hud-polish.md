# 전투 HUD 가독성 개선

- 상태: Ready
- 담당: Codex (메인 직접 작업)
- 기준: codex/integration, HEAD 77160f46d11c6780484164d3167f5a7140d3135d + 기존 미커밋 상태 보존
- 요청: 인게임 완성도 향상, 명일방주 참고 UI
- 허용: BattleManager.cs의 UI 연결·결과 표시 부분, UI/PlayerAPBar.cs, UI/UnitStatusBar.cs, UI/BattleResultUI.cs, 신규 UI/BattleHudView.cs(+meta), 관련 HUD 회귀 테스트, 본 작업서·인계서·검증 이미지
- 금지: 전투 수식·AI·성장/보상 규칙, 사용자 씬·프리팹·아트·패키지·설정 수정
- 구현: 결과 중복 패널 제거, 선택 유닛 HP/SP 카드, 행동 버튼 상태 강조, AP 예상 소모와 부족량 표시, 월드 체력바 크기 보정, 결과 화면 계층·한글 가독성
- 완료 조건: 컴파일 오류 0, 관련 회귀 및 기존 테스트 확인, BattleScene HUD·실제 입력·결과 화면 확인, Play 종료와 실행권 반납
- 참고: 사용자 씬과 기존 미커밋 결과 UI 변경은 보존. 씬 재생성 도구 실행 금지.
