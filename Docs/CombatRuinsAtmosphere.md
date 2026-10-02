# 침수 유적 원경 안개와 고해상도 하늘

2026-10-01. BattleScene에 적용. 사용자 요청은 자연스러운 원경 안개, 고화질 하늘·구름, 부족한 원경 도시 에셋의 효과적인 보완이다.

## 제작 방식

기존 스카이박스의 원본은 1376×768 JPG이며, 이를 전체 하늘에 펼치면 확대 흐림과 건축물의 파노라마 왜곡이 생긴다. 이번에는 이미지 업스케일 대신 **픽셀별로 그리는 절차적 HDR 스카이 셰이더**를 만들었다. 하늘의 색, 구름의 큰 형태와 세부, 태양 주변 빛을 방향 벡터로 계산하므로 저해상도 원본과 UV 이음새가 없다. 3840×2160 실제 렌더를 저장했다. AI로 생성한 4K 이미지나 초해상도 복원이라고 주장하는 방식이 아니다.

구름은 정적인 다중 주파수 노이즈 기반 하늘 표현이며, 물리적 볼류메트릭 구름이나 날씨 시뮬레이션은 아니다. GPU 성능은 별도 벤치마크하지 않았다.

먼 도시는 별도 텍스처를 확대하지 않고 54개 탑과 끊어진 고리 다리를 에디터에서 생성한 하나의 메시로 표현했다. 약 220m/365m의 두 거리층을 사용하고, 탑의 높이·굵기·상부를 변형했다. 원경 전용 단색 PBR 재질, MeshRenderer 1개, 약 41,552 삼각형이며 런타임 생성 스크립트·콜라이더·실시간 그림자는 없다. 근접 관찰용 완성 건축 에셋이 아닌 원경 실루엣이다.

## 조절 위치

- 하늘 재질: `Assets/Environment/CombatRuins/Sky/RuinsSky.mat`
- 셰이더: 같은 폴더의 `RuinsAtmosphereSky.shader`
- 도시: Hierarchy의 `Combat Ruins - Distant City`
- 도시 메시/재질: 같은 폴더의 `DistantCity.asset`, `DistantStone.mat`
- 안개: Lighting → Environment → Fog. Exponential Squared, Density **0.0035**, Color **(0.64, 0.76, 0.82)**.
- 하늘의 Horizon 색과 Fog 색을 함께 조절하면 원경과 하늘이 자연스럽게 이어진다.
- 하늘의 Coverage는 구름 양, Zenith는 높은 하늘색, Cloud Light/Shade는 구름의 밝고 어두운 색이다.
- `Tools/Checkmate/Background/Apply Ruins Sky and Distance Fog`: 하늘/안개 기본값 적용. 기존 도시 그룹은 보존한다.
- `Tools/Checkmate/Background/Rebuild Distant Ruins Mesh`: 전용 도시 메시를 다시 생성한다. 도시 메시를 직접 편집했다면 이 메뉴가 그 편집을 덮어쓴다.
- Scene View에서 안개가 안 보이면 Effects의 Fog 표시를 켜고 Game View와 비교한다. Scene View 표시 여부와 게임의 RenderSettings 안개는 별개다.

## 검증

- Unity 6000.3.10f1의 연결 프로젝트 경로 및 BattleScene 확인.
- 적용 전 미저장 씬까지 `Captures/CombatRuins/sky-before.unity`에 사본 보존.
- 씬 변경은 안개 모드/농도/색, 스카이박스 연결, Sun 연결과 새 원경 그룹 추가. 기존 모델 배치, 물·캐릭터 머티리얼, 전투 로직은 보존.
- Unity C# 컴파일 성공, 하늘 셰이더 오류 없음.
- 적용 전: `Captures/CombatRuins/sky-before.png`.
- 적용 후 4K: `sky-after-4k.png`, 하늘 단독 4K: `sky-clouds-4k.png`. `*-preview.png`는 공유용 축소 미리보기다.
- Play Mode 렌더 및 자동 종료 확인. Error/Exception/Assert 기록 없음: `sky-validation.txt`. 게임 화면: `sky-play.png`.
- 기존 편성 문제 때문에 씬 직접 실행에서 킹 배치 실패 및 AI 대상 없음 안내가 남아 있다. 캐릭터·전체 전투 검증 결과로 간주하지 않는다.
- 기존 물 셰이더의 Enum Inspector 표시 경고, DefaultTestDeck 21/20 편성 예산 경고는 작업 범위 밖이다.
- 전투 규칙을 변경하지 않아 전투 Edit Mode 회귀 테스트는 실행하지 않았다.
