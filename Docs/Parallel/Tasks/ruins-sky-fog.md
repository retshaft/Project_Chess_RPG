# 고해상도 하늘과 원경 안개

- Status: Review (BattleScene 적용, 4K 렌더, 컴파일/셰이더와 Play 진입·종료 확인)
- Owner: Codex main / codex/integration
- Workspace: C:/Users/VR13/Downloads/Graduation_Project_v01
- Base: ef130428defd3076024644c5addc0b9bd4d8dcf5
- Scope: 사용자 요청에 따른 BattleScene 원경 안개와 고화질 스카이박스 제작/적용.
- Allowed: Assets/Environment/CombatRuins/Sky/** 및 meta; Assets/Scripts/Editor/StylizedBackground/CombatRuinsAtmosphere.cs 및 meta; CombatRuinsJob.cs의 요청/검증/캡처 확장; BattleScene.unity의 RenderSettings와 새 원경 전용 그룹; Captures/CombatRuins/sky*; 이 문서, Docs/CombatRuinsAtmosphere.md, BOARD.md.
- Preserve: 기존 미커밋/미저장 편집, 아트 배치, 물 및 캐릭터 머티리얼, 기존 스카이박스 원본, 전투 로직과 공용 렌더러.
- Approach: 기존 안개 경로의 Exp2 감쇠 조정, 텍스처 해상도 제한과 파노라마 이음새가 없는 방향 기반 절차적 HDR 하늘/구름. 원경 구성은 사용자 응답 반영.
- Validation: 씬 사본, 같은 구도 전후/4K 캡처, 셰이더 컴파일, Unity 콘솔, Play 진입/종료.
- User clarification: 하늘·구름 중심. 에셋 부족을 고려해 가장 유효한 원경 도시 구현을 선택하고 여의치 않을 경우 스카이박스 실루엣 허용. 가벼운 원경 전용 통합 메시를 생성해 실제 3D 안개와 깊이 활용.
