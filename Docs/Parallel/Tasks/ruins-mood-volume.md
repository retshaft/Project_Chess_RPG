# 침수 유적 무드 볼륨

- Status: Review (볼륨 적용/배경 Play 확인, 편성 후 캐릭터 확인 잔여)
- Owner: Codex main, original workspace, codex/integration
- Base: ef130428defd3076024644c5addc0b9bd4d8dcf5
- Workspace: C:/Users/VR13/Downloads/Graduation_Project_v01
- Request: 현재 BattleScene을 확인하고 사용자 레퍼런스의 무드에 맞는 URP 볼륨 구성.
- Allowed: Assets/Scenes/Test/BattleScene.unity; Assets/Environment/CombatRuins/RuinsMoodVolume.asset 및 meta; Assets/Scripts/Editor/StylizedBackground/CombatRuinsVolume.cs 및 meta; 기존 CombatRuinsJob.cs의 명시적 볼륨 요청 처리; Captures/CombatRuins/volume*; 이 작업서; Docs/CombatRuinsVolume.md; BOARD.md.
- Preserve: 기존 미커밋/미저장 편집, 모델 배치, PBR 및 캐릭터 셰이더, 물 머티리얼, 전투 로직, 공용 렌더러와 기본 볼륨 프로필.
- Validation: 연결 에디터 프로젝트 경로 확인, 현재 씬 사본 보존, 같은 카메라 전후 렌더, 컴파일/콘솔 확인, BattleScene Play Mode와 종료 확인.
