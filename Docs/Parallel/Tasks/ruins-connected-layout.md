# 이어지는 유적 프리팹 배치
- Status: Verified (배경 배치 / 전체 전투 미검증)
- Owner: Codex main / codex/integration / C:/Users/VR13/Downloads/Graduation_Project_v01
- Base: ef130428defd3076024644c5addc0b9bd4d8dcf5
- Scope: 현재 BattleScene을 보존하고 RuinsBackgrounds의 Path, LowWall 2/1/0 기둥형, Platform, Stairs, SmallPillar, Pillar로 이어지는 침수 유적 배경 구성.
- Allowed: BattleScene.unity 신규 연결 유적 그룹; Assets/Scripts/Editor/StylizedBackground/CombatRuinsConnections.cs 및 meta; CombatRuinsJob.cs의 명시적 요청과 검증 라우팅; Captures/CombatRuins/connections*; 이 문서, Docs/CombatRuinsConnections.md, BOARD.md.
- Preserve: 기존 배치/미저장 사용자 편집, 원본 프리팹/재질/GUID, 전투판 공간, 카메라와 전투 로직, 하늘과 안개.
- Validate: live prefab bounds/rotation, 연결부 높이 및 간격, 전후 렌더 및 top view, 컴파일/콘솔, Play 종료.
