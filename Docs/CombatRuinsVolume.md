# BattleScene 침수 유적 무드 볼륨

2026-10-01 사용자 레퍼런스 기준. 원본 씬의 PBR 석재, 청록 수면, 따뜻한 직사광과 푸른 안개를 살리면서 하이라이트를 정돈하는 절제된 1차 룩이다.

## 적용 위치

- 씬: `Assets/Scenes/Test/BattleScene.unity`
- 오브젝트: `Combat Ruins - Mood Volume` (Global, Priority 10, Weight 1)
- 프로필: `Assets/Environment/CombatRuins/RuinsMoodVolume.asset`
- Main Camera의 Post Processing 활성화.
- 재적용 메뉴: `Tools/Checkmate/Background/Apply Ruins Mood Volume`. 현재 BattleScene에서만 실행하며 해당 전용 프로필 값을 초기값으로 다시 설정한다.

| 항목 | 값 / 의도 |
|---|---|
| Tonemapping | Neutral: 밝은 표면 압축, 기존 재질색 보존 |
| Post Exposure / Contrast / Saturation | +0.3 EV / +8 / -3 |
| White Balance | Temperature -3, Tint +1 |
| Split Toning | 그림자 RGB 0.485/0.502/0.515, 밝은 부분 0.525/0.513/0.490, Balance +5 |
| Bloom | Threshold 1.15, Intensity 0.18, Scatter 0.55, 약한 따뜻한 색 |
| Vignette | Intensity 0.10, Smoothness 0.45 |
| Depth of Field / Motion Blur / Chromatic Aberration / Film Grain | 비활성: 전투판 선명도 유지 |

배경용 아트 디렉션이지만 URP Global Volume은 카메라에 보이는 캐릭터에도 적용된다. 볼륨 레이어 마스크는 볼륨 선택용이며 배경 메시만 색 보정하는 마스크가 아니다. 캐릭터의 개별 색을 유지하도록 보정을 약하게 설정했다. 별도 카메라/마스크 합성은 이번 범위에 넣지 않았다.

원본 환경광, 안개, 광원, 물 머티리얼, 모델 배치와 전투 로직은 변경하지 않았다. 레퍼런스의 먼 유적 실루엣, 식생, 물 반사 디테일 자체를 후처리가 만들어 주지는 않는다.

## 검증 및 보존

- 연결 에디터에서 프로젝트 절대 경로와 BattleScene 확인.
- 미저장 편집을 포함한 적용 직전 씬 사본: `Captures/CombatRuins/volume-before.unity`.
- 해당 사본 대비 씬 변경은 전용 볼륨 추가와 Main Camera Post Processing 활성화뿐이다.
- Unity 에디터 컴파일 성공. `error CS` 0건.
- 같은 메인 카메라 적용 전/후: `Captures/CombatRuins/volume-before.png`, `volume-after.png`.
- Play Mode 진입/렌더/종료 확인. 런타임 Error/Exception/Assert 0건. 기록: `Captures/CombatRuins/volume-validation.txt`, `volume-play.png`.
- 씬 직접 실행 시 킹 자동 배치 실패 안내와 AI 대상 없음 경고가 발생해 기물이 표시되지 않았다. 따라서 캐릭터 색감과 전투 상호작용 검증은 미완료다.
- 확인 순서: OutgameScene → 킹을 포함한 덱 편성 → 출격 → 밝고 어두운 타일에서 캐릭터 얼굴/윤곽/색 및 선택 표시 확인 → Play Mode 종료.
- 전투 규칙 변경이 없어 Edit Mode 전투 회귀 테스트는 실행하지 않았다.
- 검증 중 에디터 파일 요청 브리지의 공유 위반 1건을 읽기 재시도로 수정했다. 신규 요청을 컴파일 완료 전에 보낸 미지원 명령 오류도 1건 있었으며, 최종 컴파일 후 재실행에 성공했다. 최종 Unity 콘솔 오류 0건, 기존 DefaultTestDeck 편성 예산 초과(21/20) 경고 1건을 확인했다. 게임 런타임 오류와 구분한다.
- 추가 환경 구도 비교: `volume-overview-before.png`, `volume-overview-after.png`. 원경 기어가 짙은 안개에 가려지는 현상은 후처리를 끈 비교 화면에도 동일하다.
- 기존 중복 System.Collections.Immutable 어셈블리 경고는 이번 볼륨 변경 범위 밖이다.
