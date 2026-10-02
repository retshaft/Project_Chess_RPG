# 수중 유적 확장 및 반복 구조 모듈화
- Status: Verified (배경 배치; 전체 전투/성능 미검증)
- Owner: Codex main
- Branch: codex/integration
- Workspace: C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01
- Base: ef130428defd3076024644c5addc0b9bd4d8dcf5
- Allowed: BattleScene.unity 신규 그룹; CombatRuinsConnections.cs 기존 배치 함수 internal 공개; 신규 CombatRuinsSubmerged.cs 및 meta; RuinsBackgrounds/Modules 및 meta; Captures/CombatRuins/submerged*; 본 작업서, Docs/CombatRuinsSubmerged.md, BOARD.md.
- Preserve: 기존 미커밋 변경, 배치, 원본 프리팹, 물/재질/카메라/전투 로직.
- Goal: 기존 유적 외곽에 수면 및 수중 회랑/광장/계단 배치. 중첩 프리팹 모듈과 짧은 재사용 안내 제공.
- Validation: 씬 백업, 컴파일/콘솔, 전후 및 수중 렌더, 배경 Play 검증과 종료. 전투 규칙 변경 없음.

