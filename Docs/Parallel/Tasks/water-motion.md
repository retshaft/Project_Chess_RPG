# 자연스러운 물살 제어
- Status: Verified (185 tests, Play/일시정지/재개)
- Owner: Codex main / codex/integration
- Base: ef130428defd3076024644c5addc0b9bd4d8dcf5
- Workspace: C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01
- Allowed: 신규 Assets/Scripts/Water/ShallowWaterMotion.cs 및 meta; ShallowWater.shader; BattleScene 물 컴포넌트; Assets/Scripts/Testing/WaterMotionTests/ 및 meta; Docs/WaterMotion.md; 본 작업서; BOARD.md; Captures/CombatRuins/water-motion*.
- Preserve: 물색/투과/반사 재질, 기존 배치 및 사용자 미저장 변경. 공유 머티리얼 런타임 변경 금지.
- Goal: 렌더 이벤트 기반 독립 위상/연속 흐름/완만한 흐름 변화. 반사 MPB와 공존, disable fallback, pause 일관성.
- Verify: 연속성/주기/프레임 독립성 테스트, 컴파일, Play 움직임/일시정지/복구, 콘솔/종료.

