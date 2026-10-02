# 수면 완성도 보강
- Status: Verified (레퍼런스 후속: 재질 조정, Water.Tests 6/6, 정식 경로 Play/종료/콘솔 확인)
- Owner: Codex main / codex/integration
- Base: ef130428defd3076024644c5addc0b9bd4d8dcf5
- Workspace: C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01
- Allowed: Assets/Resources/Shader/Water/ShallowWater.shader, Assets/Resources/Materials/Water/ShallowWater_Battle.mat, Assets/Scripts/Water/ShallowWaterReflection.cs 및 meta, BattleScene의 물 컴포넌트 추가, Docs/WaterDetail.md, 본 작업서, BOARD.md, Captures/CombatRuins/water-detail*.
- Goal: 깊은 바닥 차폐 유지, 더 밝은 수색, 실제 유적/하늘 반사와 자연스러운 수면 디테일. 기존 사용자 배치/크기/미저장 작업 보존.
- Verify: Unity 컴파일, 여러 시점 렌더 및 반사 on/off, Play 움직임/종료/콘솔, 자원 해제와 재진입. 반사 비용 제한과 fallback 확인.



