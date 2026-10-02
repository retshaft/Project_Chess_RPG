# 이어지는 침수 유적 배치

2026-10-01, BattleScene 아트 배치.

기존 Path의 열린 끝점과 높이를 측정한 뒤 `Combat Ruins - Connected Terraces` 그룹에 프리팹 인스턴스 198개를 추가했다. 원본 RuinsBackgrounds 프리팹과 재질의 연결은 유지한다.

- Northern cloister: 서쪽 통로와 북쪽 통로를 연결하고, 계단으로 한 단 내려가 뒤쪽 회랑을 완성.
- Eastern water gallery: 동남쪽 기존 통로에서 출발하여 두 광장을 지나 북쪽 회랑까지 연결.
- Sunken southern terraces: 기존 남서쪽 계단의 양쪽 끝을 확장하고 수면에 가까운 하부 광장 추가.
- Outer colonnade: 보드 바깥쪽에 높이가 다른 기둥 6개 배치.

Path, Platform, Stairs, LowWall 2/1/0 기둥형, SmallPillar, Pillar를 사용했다. 난간의 결손 구간과 높이 차이로 폐허 느낌을 유지했다. 통로 아래에는 수중 받침 기둥을 배치했다. 배경 전용이며 추가 Collider 및 런타임 스크립트는 0개다.

## 보존과 편집

- 작업 직전 미저장 씬 포함 백업: `Captures/CombatRuins/connections-backup-20261001-151941.unity`.
- 기존 직렬화 문서 1,278개 비교: 기존 요소 삭제/수정 없음. SceneRoots 목록에 신규 그룹만 추가.
- 기존 보드, 카메라, 대형 기어, 하늘/안개, 원경 도시, 원본 프리팹은 유지.
- 추가 그룹의 4개 하위 그룹에서 개별 인스턴스를 조정할 수 있다.
- `Tools > Checkmate > Background > Add Connected Ruins`는 그룹이 없는 BattleScene에만 적용되며 중복 배치를 거부한다. 이미 적용된 씬에서는 다시 실행할 필요가 없다.

## 검증

- Unity 에디터 컴파일 성공, 컴파일 오류 0.
- 전체 원근/상부/메인 카메라 렌더 확인. 보드 가림 없음, 통로 이음새 보정.
- BattleScene Play 진입과 종료, 런타임 오류/예외 0: `Captures/CombatRuins/connections-validation.txt`.
- 기존 DefaultTestDeck 예산 초과(21/20) 경고 때문에 캐릭터 배치 및 전체 전투 흐름은 이번 검증 범위에 포함하지 않음.
- 배경 배치 변경으로 전투 규칙 테스트는 실행하지 않음. 성능 벤치마크는 수행하지 않음.
- 확인 이미지: `Captures/CombatRuins/connections-after.png`, `connections-top-after.png`, `connections-game.png`, `connections-play.png`.
