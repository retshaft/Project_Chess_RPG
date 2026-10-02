# 수면 디테일 — 2026-10-02

## 빠른 조절

대상: `Assets/Resources/Materials/Water/ShallowWater_Battle.mat`.
셰이더: `Assets/Resources/Shader/Water/ShallowWater.shader`.

- 잔물결: Strength A/B/C=0.32/0.24/0.12, 서로 다른 방향과 크기로 흐름.
- 넓은 수면: Pixel Waves=1. 공통 WaveSurface 함수로 정점 변위와 픽셀 노멀 계산 재사용. 원거리 디테일 감쇠 65~230m (기존 12~45m).
- 투과: Density=0.28, 청록색 흡수/산란. 수중 유적의 형태 유지.
- 바닥 빛무늬: Caustics Intensity=1.4, Depth=7m.
- 접촉 포말: Contact Mode=Foam, Width=0.38, Foam Intensity=0.55, Contact Ripples=0.55. 깊이 경계를 따라 끊어진 띠가 이동하며 픽셀 폭으로 가장자리를 부드럽게 한다.
- 반사: 기존 환경 프로브 유지, Reflection=1.15, Roughness=0.16. 정밀 캐릭터 거울 반사는 아님.

새 옵션 기본값은 0이라 기존 프리셋은 자동 활성화되지 않는다. 추가 텍스처/런타임 스크립트/Update/렌더 패스 없음. 노멀과 거품 계산은 기존 셰이더 경로를 확장한다.

현재 사용자가 조정한 물 크기 (7,7,7), 위치 (8,-10.2,8), 씬 배치와 바닥 격자 재질은 보존했다. 외곽 유적 일부가 수면 밖에 있는 것은 이 기존 범위 때문이다. 레퍼런스의 캐릭터 발걸음·공격 원형 물결은 접촉 이벤트가 필요한 별도 효과이며 이번 배경 수면 변경에는 포함하지 않는다.

## 검증

- Unity 6000.3.10f1 실제 프로젝트 연결 확인.
- 머티리얼 파일 변경 중 YAML 오류 1회 발생, 즉시 백업의 원래 속성 블록 기반으로 수정. 재임포트 및 이후 콘솔 오류/경고 0.
- ShaderUtil 메시지 0, ShaderHasError=False. 근경·전체 렌더 확인.
- BattleScene Play: 11.72758초/1199프레임 및 13.69524초/1369프레임 동일 시점 캡처. RGB 합 차이 12 초과 픽셀 452,018/1,296,000으로 움직임 확인. Play 종료.
- Pool Medium (WaveAmplitude=0, PixelWaves=0) 임시 적용 렌더, 셰이더 오류 없음, Battle 머티리얼/카메라 즉시 복구.
- 전투 코드 변경 없음으로 전투 테스트 재실행 생략. GPU/모바일 성능과 캐릭터 상호작용 미검증.
- 캡처: `Captures/CombatRuins/water-detail-{before,after,close,play-a,play-b,pool-regression}.png`.
- 작업 직전 씬/머티리얼/셰이더 백업: 같은 폴더 `water-detail-before.*`.

## 깊은 물 및 포말 수정 (2026-10-02 후속, 위 수치보다 우선)

사용자 피드백에 따라 얕은 물 같은 투과를 줄였다. Density=1.3, Absorption=(0.8,0.42,0.32), MaxThickness=40. 광학 경로 10m의 RGB 투과율은 약 0.003%/0.43%/1.56%, 15m에서는 최대 0.20%로 떨어진다(표면색/반사 전). 1m 이내 접촉부는 비치고, 깊은 바닥 격자는 가려지며 3m 아래 광장은 희미한 윤곽만 남는다. 이는 이 장면의 연출값이며 모든 물의 실제 투과율을 뜻하지 않는다.

안개 합성에서 원래 바닥색으로 보간하던 경로가 깊은 바닥을 다시 노출시키던 문제를 수정했다. 이미 안개 처리된 투과 성분은 유지하고 나머지 물 성분에만 안개색을 합성한다. 안개가 없는 경우 원래 셰이딩 결과와 같다.

포말의 카우스틱 그물/반복 고리 패턴 대신 두 크기의 흐르는 노이즈로 접촉부 거품을 잘라낸다. Coverage(ContactRipples)=1, Width=0.24, Intensity=0.32. 균일한 밝은 접촉선과 수면 전체 crest foam은 끔. 포말은 드물고 약한 패치이며 수면 움직임에 따라 변화한다. Caustics Intensity=0.55, Depth=1.8로 깊은 바닥의 빛무늬도 억제했다.

검증: Unity 셰이더 메시지 0, 실제 BattleScene Play 근경/원경 렌더, 깊은 바닥 가림과 얕은 수중 광장 감쇠 확인. 콘솔 오류/경고 0, Play 종료. 전투 코드 변경 없음으로 전투 테스트 생략, GPU 성능 미측정. 이번 수정은 셰이더/전용 재질만 저장하며 씬의 미저장 사용자 변경은 유지한다.

캡처: Captures/CombatRuins/water-detail-depth-close.png, water-detail-depth-overview.png, water-detail-depth-play-b.png. 작업 직전 셰이더/재질은 water-detail-depth-before.*에 보관.

## 최종 수색·반사 보강 (2026-10-02, 현재 설정)

깊이를 어두운 색으로만 표현하던 결과를 수정했다. 현재 Scattering=(0.18,0.46,0.49), Density=1.1. 깊은 바닥 감쇠는 유지하고 물 자체의 밝기를 올린다. SurfaceTintStrength=0.06을 실제로 반영하도록 셰이더의 강제 최소 0.22를 제거했다. 따라서 기존 다른 프리셋에서도 지정한 SurfaceTintStrength 값이 그대로 적용된다.

새 `ShallowWaterReflection` 컴포넌트는 수면 위 유적과 하늘을 수평 반사 카메라로 렌더한다. 실제 설치된 URP의 SingleCameraRequest 경로를 사용하며, 반사 텍스처/카메라를 재사용한다. 512px 폭, 종횡비에 따른 높이(캡처 512×320), HDR/mipmap, 140m 원경 제한. 반사 카메라에서는 추가 그림자와 후처리를 끄고 메인 카메라에서 최종 후처리를 적용한다. 수중 기하와 수면 자체는 반사에서 제외한다.

- Planar Strength=0.9, Distortion=1.6, Roughness=0.16. 물결에 따라 반사상이 흔들리며 시선각 기반 Fresnel로 섞인다.
- 잔물결 A/B/C=0.30/0.22/0.085, 큰 물결 진폭 0.065m, 주파수 0.75, 속도 0.32.
- 포말은 기존 불규칙 패치 유지, Width=0.32m, Intensity=0.42. 반복 고리·수면 전체 거품 없음.
- 메인 게임 카메라와 Scene View의 보이는 수면만 반사 갱신. 다른 카메라/XR/수면 아래에서는 환경 프로브 fallback. 컴포넌트를 끄거나 Planar Strength=0이면 추가 반사 렌더를 생략한다.
- OnDisable에서 카메라/RT 정리. 재귀 렌더 방지와 finally의 컬링/수면 표시 복구. 매 프레임 Update 폴링 없음.
- 씬 변경: Battle Shallow Water에 컴포넌트 하나 추가. 작업 직전 라이브 씬 백업과 비교했을 때 물 프리팹 인스턴스의 추가 컴포넌트 목록 외 기존 문서 변경/삭제 없음.

검증: 셰이더/스크립트 컴파일 오류 0, 58초/5,590프레임까지 Play 렌더 및 콘솔 오류/경고 0. 반사 on/off A/B, 끈 뒤 임시 카메라 0/텍스처 0, 다시 켜기, 직교 상부 시점, 메인 전투 시점, Pool 공유 프리셋 임시 렌더 확인. CheckmateRPG.Tests 179/179 통과(job 486da623197d4dda92686a793295b546). 최종 Play 종료.

추가 카메라 렌더 비용은 발생한다. 실제 GPU 벤치마크·모바일·XR 품질은 미검증이며 58초 플레이 프레임 수는 성능 측정치가 아니다. 반사는 수평면 기준이므로 큰 파도/수중 카메라에는 적합하지 않다. 캐릭터 접촉 물보라/발자국 이벤트는 포함하지 않는다.

현재 확인 이미지: Captures/CombatRuins/water-detail-polish-play-b.png, water-detail-polish-close.png, water-detail-polish-overview.png, water-detail-polish-top.png. 백업: water-detail-polish-before.unity/.mat/.shader.

## 탁한 표면 색 보정 (2026-10-02, 현재 설정)

- 표면의 일률적인 색 덮기 SurfaceTintStrength=0. 산란색 (0.12,0.39,0.45)로 녹색의 우윳빛을 줄이고 반사 선명도를 높였다(Roughness=0.09, PlanarStrength=1, Distortion=0.85).
- ClearDepth=2.5m 전이 추가. opticalPath = thickness - ClearDepth*(1-exp(-thickness/ClearDepth)). 수면 가까이는 맑게 비치고 깊어지면 흡수가 누적된다. 기본값 0이면 기존 경로 그대로.
- Density=1.15, Absorption=(0.8,0.52,0.48). 1m 광학경로의 최대 투과율 약 91%, 10m 약 1.55%, 15m 약 0.10%. 얕은 광장과 깊은 바닥을 분리한다. 실제 수질의 측정값이 아닌 연출 설정이다.
- 잔물결 A/B/C=0.22/0.14/0.055로 반사가 과하게 번지는 정도를 완화.
- 기존 씬/반사 컴포넌트/포말 배치는 변경하지 않음. 전용 재질과 셰이더 옵션만 변경.
- 근경·낮은 시점 캡처: water-detail-clarity-close.png, water-detail-clarity-low.png. 이전 재질/셰이더: water-detail-clarity-before.*.
- 이번 검증: ShaderUtil 메시지 0, Play 진입/수면 렌더 확인, 콘솔 오류·경고 0, Play 종료. 전투 로직 변경 없음으로 기존 전투 테스트 재실행 생략. GPU 성능 미측정.

## 2026-10-02 레퍼런스 후속 조정
- 이번 범위는 ShallowWater_Battle 재질만 변경. 사용자 최신 Density .3, ClearDepth 2.38, 수색과 SurfaceTintStrength .096 보존. 씬/기물/UI/스크립트 변경 없음.
- 파도 주파수 .75→.48, 진폭 .065→.085. 노멀 타일링 .10/.23/.82, 강도 .18/.085/.025. 보조 흐름을 주 흐름에 가깝게 정렬하고 타일링에 비례해 UV 속도 조정해 넓은 물결이 과속하지 않도록 함.
- 반사 왜곡 .85→.65, 거칠기 .09→.11. 접촉 포말 폭 .32→.22, 강도 .42→.30. 카우스틱 크기를 넓히고 속도를 낮춤, 강도 .55→.68. 수심 감쇠 1.8 유지.
- 검증: Water.Tests 6/6 통과(job 01acaa25249e416baa1236c292989040). Outgame→Battle Play, 36.27초/57.16초에서 파도 위상 및 UV 변화, 평면 반사 활성 확인. 여러 시점 렌더 확인. 콘솔 오류/경고 0건. Play 종료 및 Outgame 복귀 확인.
- Captures/CombatRuins/water-detail-reference-{before,after,depth,play-a,play-b}.png. before.mat은 이번 변경 직전 원본.
- 제한: 현재 유적 씬은 레퍼런스의 얕은 해안 지형과 조명이 달라 밝은 바닥 빛무늬가 덜 드러남. 이번에는 사용자가 조정한 투과를 보존했으며 깊이 차폐 수치를 강화하지 않음. 전투/UI 전체 회귀 테스트는 이번 재질 조정에서 재실행하지 않음.
