# 얕은 물 사용 및 검증 기록

## 열기

`Assets/Scenes/Test/Water/ShallowWaterShowcase.unity`를 열고 Play한다. `Shallow Water` 오브젝트에 레퍼런스 조정판 `ShallowWater_Beach_Reference` 머티리얼이 적용되어 있다. 수중 타일, 모래, 돌, 수초, 다리 접촉 모형, Sun, Camera, Reflection Probe가 포함된다. 전투 로직이나 정식 씬을 교체하지 않는다.

기존 `Assets/Resources/Shader/WaterShader.shadergraph`, `WaterMats.mat`, WaterTex 노멀은 보존한다. 새 구현은 `Checkmate/Water/Shallow Water`이며 기존 두 노멀 텍스처를 참조한다.

## 렌더링 조사와 설정 변경

| 항목 | 확인한 구성 |
|---|---|
| Unity / URP | 6000.3.10f1 / 17.3.0, 로컬 PackageCache API 확인 |
| 활성 품질 | PC |
| 렌더러 | PC_Renderer, Forward+ |
| HDR / Depth / Opaque | 모두 켜짐 |
| MSAA / Render Scale | 1(꺼짐) / 1.0 |
| Renderer Feature | 기존 SSAO 1개, 추가 기능 없음 |
| 기존 Materials 씬 | Directional Light 1개, Probe 없음, 저장되지 않은 변경 존재 |
| PC 변경 | PC_RPAsset의 Opaque Downsampling: 2x Bilinear → None. 바닥색을 전체 해상도로 샘플링 |
| 검증 카메라 | Depth/Color 명시적 On, HDR, 후처리 On, SMAA |
| Mobile | 기존 Depth/Opaque Off, Render Scale 0.8 유지. 실제 적용 카메라에서 Depth/Color를 On으로 지정해야 함 |

패키지, 전투 코드, 정식 씬, 기존 Renderer Feature, MSAA, Render Scale은 수정하지 않았다. Test Runner가 기존 Materials 씬의 미저장 작업을 자동 저장했으며, 마지막에 카메라·조명·Plane을 모두 원래 활성 상태로 복구해 저장했다. 사용자 Plane과 머티리얼 작업은 보존되었다.

## 구현

- URP HLSLPROGRAM, UnityPerMaterial CBUFFER, 단일 Transparent 패스. ZWrite Off, ZTest LEqual, Cull Back.
- 재구성한 월드 좌표로 광학 경로 길이를 계산한다. Perspective/Orthographic을 같은 경로로 처리한다.
- RGB Beer–Lambert 흡수와 산란색. 얕은 바닥 원색이 남도록 약한 기본 밀도.
- 화면 경계·거리·수심에 따른 굴절 감쇠, 최대 오프셋 제한. 굴절 후보의 깊이를 확인해 전경/수면 위 샘플을 거부한다.
- 두 스크롤 노멀과 High의 세 번째 노멀, RNM 합성. 각 텍스처·방향·속도·스케일·강도 독립 조절.
- 재구성한 수중 수신점에 두 카우스틱 레이어를 투영한다. 굴절된 태양 방향, 수신면 기울기, 수심, 거리, 메인 라이트와 선택적 그림자를 반영한다.
- Schlick Fresnel(F0=0.02), URP Reflection Probe/환경 반사, GGX 기반 태양 하이라이트 및 강도 상한.
- Soft Contact 기본, 선택적 Noise Foam, 서로 다른 방향/파장의 4중 사인파 변위와 crest foam. 파장별 전파 속도 및 동일 변위 함수의 기울기로 표면 노멀을 계산한다.
- 거리 감쇠와 Fog. Opaque 색에 이미 안개가 적용되므로 물의 기여분만 감쇠하여 바닥의 이중 Fog를 피한다.
- Thickness/Fresnel/RefractionMask/Normal/Caustics/Contact/Foam 디버그.

표면은 월드 Y 위쪽의 수평 수면용이다. 수직 폭포, 수중 카메라, 서로 포개지는 수면용이 아니다. Wave를 켜려면 세분화된 메시와 여유 bounds가 필요하다. 제공 메시에는 최대 기본 변위에 대한 bounds 여유가 있다.

## 아티스트 조절값

| 목적 | Property |
|---|---|
| 바닥색 유지 | Density를 낮춘다. Absorption은 RGB별 m당 흡수량 |
| 물색 | Scattering, Absorption; 단순 알파 색 덮기를 사용하지 않음 |
| 잔물결 | Strength A/B/C, Layer A/B/C의 tiling/direction/speed |
| 큰 수면 굴곡 | Wave Amplitude/Frequency/Speed/Direction, Wave Refraction |
| 굴절 | Refraction, Max Offset |
| 카우스틱 | Intensity, A/B의 scale/rotation/speed/distortion, Depth |
| 반사 | Reflection, Roughness, F0 |
| 하이라이트 | Stylized Specular: Off/Subtle/Strong, Specular, Specular Limit |
| 접촉과 거품 | Contact Width/Intensity; Foam 모드와 Foam Intensity를 함께 켠다 |
| 거리 품질 | Fade Start/End, Quality |

기본 추천: Clear High, Density 0.65, Absorption RGB (0.65, 0.15, 0.07), 노멀 0.16/0.12/0.055, Refraction 0.018, F0 0.02, Roughness 0.24, Foam/Wave 0.

| 프리셋 | 용도 / 비용 차이 |
|---|---|
| Clear High | 기본 얕은 물. 3중 노멀, 굴절 깊이 확인, 바닥 그림자 |
| Pool Medium | 밀도 0.38, 잔물결 약함. 2중 노멀, 굴절 유지, 바닥 그림자 생략 |
| Stream High | 밀도 0.75, 노멀 강화, 약한 Foam Contact |
| Mobile Low | 2중 노멀, 굴절 오프셋·두 번째 Depth 조회 및 바닥 그림자 생략 |
| Clear Ultra | 현재 High와 동일 경로. 불필요한 별도 변형이나 평면반사를 추가하지 않음 |
| Beach Reference | 첫 레퍼런스 방향. 녹청색 투과, amplitude 0.16m, frequency 1.65rad/m, speed 0.55, 굴절 0.048, 카우스틱 0.45 |
| Lagoon Reference | 두 번째 레퍼런스 방향. 청록색 흡수/산란 강화, amplitude 0.19m, 카우스틱 0.65 |

품질·거품·파도·디버그는 uniform 분기라서 조합별 키워드를 만들지 않는다. URP 자체의 그림자/Probe/Forward+ 키워드만 선언한다.

## 제한과 성능

- Opaque Texture는 투명 오브젝트를 포함하지 않는다. 수중 유리·파티클은 정확히 굴절되지 않으며, 물과의 정렬 순서에 따라 사라지거나 위에 그려질 수 있다. Blend One Zero는 이미 계산된 투과색을 다시 알파 혼합하지 않기 위한 의도적 설정이다.
- 화면 밖, 가려진 바닥은 재구성할 수 없다. 전경 후보 거부로 줄일 수 있지만 깊이 불연속의 미세한 굴절 이음은 완전히 제거하지 못한다.
- Screen-space caustics이므로 수면을 통해 보이는 불투명 수신체만 처리한다. 광자 추적/실제 수면 집광 시뮬레이션은 아니다.
- 반사 Probe는 단일 위치에서 잡은 근사 반사다. 동적 캐릭터의 정확한 거울 반사는 제공하지 않는다. Planar Reflection, 별도 Renderer Feature/Depth Prepass, 대양파, 추가 광원의 물 스페큘러는 제외했다.
- High 기본 텍스처 조회: Depth 2, Opaque Color 1, Normal 3, Caustics 2 + URP 그림자/환경 반사 조회. Foam을 켜면 노이즈 조회 1개 추가. Low는 Depth 1, Color 1, Normal 2, Caustics 2 + 환경/표면 그림자.
- 전체 해상도 Opaque 복사는 기존 반해상도보다 픽셀 저장량이 4배다. 모바일은 기기별로 해상도와 복사 비용을 프로파일링해야 한다. 이 작업에서 모바일 장치 성능을 보증하지 않는다.
- 검증 씬의 128px 실시간 Probe는 시작 시 한 번 갱신하며 매 프레임 렌더링하지 않는다. 고정 환경에서는 구운 Probe가 적절하다.
- 다음 개선 후보: 아트 제작 카우스틱 텍스처, 실제 배경/캐릭터 최종 에셋에서 튜닝, GPU 장치 측정, 필요한 장면에 한정한 Planar Reflection.

## 파일

- `Assets/Resources/Shader/Water/`: ShallowWater.shader와 meta.
- `Assets/Resources/Materials/Water/`: 물 프리셋 7개와 검증 환경용 머티리얼.
- `Assets/Resources/Textures/Water/`: 절차 생성 카우스틱·모래·모형 색 텍스처.
- `Assets/Scenes/Test/Water/`: 검증 씬과 수면/바닥 Mesh 에셋.
- `Assets/Scripts/Editor/Water/`: 덮어쓰기 방지 생성 도구와 독립 Editor asmdef.
- 수정: `Assets/Settings/PC_RPAsset.asset`, 병렬 보드 및 이번 작업서. `Assets/Scenes/Test/Materials.unity`는 Test Runner 자동 저장 후 원래 활성 상태로 복원 저장되었다.

생성 도구는 씬이 이미 존재하면 중단한다. 기존 씬을 자동 저장하지 않으며 검증 준비 중 해당 씬의 루트만 임시 비활성화한다. `Tools/Checkmate/Water/Restore Previous Scene`으로 원래 활성 상태와 씬을 복구한다.

## 검증 결과

2026-09-09, 연결된 원본 프로젝트 Unity Editor에서 검증했다.

- ShaderUtil: 물 셰이더 메시지 0개, ShaderHasError=false. Editor 도구 C# 컴파일 성공. SRP Batcher compatibility code=0(호환).
- Play Mode: 실제 진입/시간 및 프레임 진행 확인, 기본 화면·낮은 시점·수직 Orthographic·수심 디버그·원거리·Low·선택적 Wave/Foam을 카메라 렌더 캡처로 확인. 검사 중 런타임 오류/경고 0개. 종료 확인.
- 수중 타일 줄눈과 모래가 보이며, 낮은 시점에서는 반사가 증가한다. 기본 흰 거품 없음. 원거리에서 카우스틱과 잔물결이 감쇠한다. 전경 모형 주변에 큰 화면 찢김은 관찰되지 않았다.
- 투명 큐브 겹침을 실제 검사했다. 수중 투명체가 물 흡수/굴절과 분리되어 그려지는 제한을 재현했다. 큐브는 Play Mode 종료로 제거되며 씬에 저장하지 않았다.
- 기존 WuWa 툰 Shader Graph를 모형 머티리얼에 적용해 함께 렌더링했다. 실제 완성 캐릭터 애니메이션/정식 전투 배경 전체 검증은 하지 않았다.
- `CheckmateRPG.Tests`: **68개 실행, 67 통과 / 1 실패**(20.88초). 기존 `Processor_Poison_GeneratesTrueDamageBasedOnMagnitude`는 기대 5, 실제 4. 기존 `StatusEffectProcessor.cs`의 float 계산이 4.99999952가 된 뒤 FloorToInt로 4가 됨을 별도로 확인했다. 물 작업 범위 밖이라 해당 코드와 테스트는 보존했다. 전체 회귀 통과로 표시하지 않는다.
- MCP 렌더 통계가 draw call 0을 반환하여 유효한 FPS/GPU 성능 수치로 사용하지 않았다. 긴 시간의 shimmer 관찰과 모바일 실기기 측정은 미실행이다.
- 추가 씬 종료/스크립트 재로딩 뒤 URP 내부 ReflectionProbeManager 및 ZBinningJob 예외가 발생했다. 씬 재로드와 렌더 파이프라인 상태 초기화로 복구했다(패키지/렌더러 설정 변경 없음). 이후 저장된 물 씬을 단독으로 열어 Play/렌더/종료 재검증 시 오류·경고 0개. 에디터 씬 전환/재로딩 시 유사 현상의 재발 가능성은 남는다.
- 기존 Materials 씬의 미저장 변경은 Test Runner가 자동 저장했다. 원래 루트 3개(Camera/Light/Plane)를 모두 활성 상태로 복원해 저장하고 확인했다. 검사에 쓴 화면/옵션 변경은 원래 기본 물 머티리얼에 남기지 않았다.
- 최종 에디터는 ShallowWaterShowcase 씬을 단독으로 열어 두었고 Play Mode는 꺼져 있다.

최종 화면: [WaterPreview.png](WaterPreview.png). 추가 진단 캡처는 `Temp/WaterVerification/`에 있으며 Unity가 Temp를 정리하면 제거될 수 있다.

## 레퍼런스 후속 조정 (2026-09-09)

첫 이미지의 완만한 큰 물결과 수중 윤곽 왜곡, 두 번째 이미지의 선명한 청록색을 각각 프리셋으로 분리했다. 배경/캐릭터 에셋 자체를 해당 게임처럼 재현한 것은 아니다. 정지 이미지에서 실제 파도 속도를 알 수 없으므로 속도는 잔잔한 해변을 기준으로 조정했다.

- 기존 굴절은 `normal - waveNormal`만 사용해 큰 물결 기울기가 상쇄되는 결함이 있었다. 원래 메시 노멀에 대한 변위 기울기를 굴절에 다시 합치고 화면 종횡비도 보정했다.
- 4개 방향/파장 성분으로 큰 굴곡과 작은 굴곡이 서로 다른 속도로 움직인다. 디테일 노멀의 접선도 변위 노멀에 직교하게 보정했다.
- 수면 메시를 128×128 셀(16,641 정점 / 32,768 삼각형)로 조밀하게 하고 bounds 높이를 0.6m로 늘렸다. Inspector 허용 최대 변위 0.25m에서도 정점이 bounds 밖으로 나가지 않는다. 저사양에서는 메시 밀도를 낮출 수 있다.
- 기존 Pool/Clear 프리셋의 변위 0 설정은 유지했다. 참조용 프리셋 2개만 새로 추가했다. 프로젝트 URP 설정을 추가 변경하지 않았다.
- 모래의 과한 노란색이 물색을 지배하지 않도록 `SandReference.mat`을 별도로 만들고 검증 씬 바닥에 적용했다. 기존 Sand.mat은 보존했다.
- computer-use로 Unity 창을 선택했으나 화면 캡처가 `SetIsBorderRequired failed / 0x80004002`로 재시도까지 실패했다. 실제 비주얼 검증은 Unity MCP 카메라 캡처로 수행한다.

후속 검증 완료:

- Unity C# 및 셰이더 컴파일 오류 0. ShaderUtil 메시지 0, SRP Batcher compatibility code 0.
- Play Mode에서 시간/프레임 진행, Beach/Lagoon의 변위와 투과, 수면 접촉을 확인했다. 최종 콘솔 오류/경고 0. Play Mode 종료.
- 시간을 7.550839초로 고정하고 디테일 노멀을 모두 끈 상태에서 Wave Refraction 0 ↔ 1.15를 비교했다. 수중 타일/다리 윤곽이 바뀌었고 두 캡처에서 478,742픽셀이 RGB 합 차이 6을 초과했다(총 2,073,600픽셀). 큰 물결의 굴절 기여가 사라지던 결함에 대한 렌더 회귀 확인이다.
- Orthographic 수직 시점의 바닥 투과와, Pool Medium의 Wave Amplitude=0에 따른 평평한 수면을 별도 캡처로 확인했다.
- 이번 후속에서는 전투 코드/규칙을 수정하지 않아 전투 테스트 68개를 재실행하지 않았다. 앞 절의 67/68은 이전 실행 기록이다. 모바일 장치 성능은 계속 미검증이다.
- 씬에는 Beach Reference를 적용하고 최종 저장했다. 진단용 임시 머티리얼/카메라 변경은 Play 종료 후 복원했다.

비교 이미지: [Beach](WaterReferenceBeach.png), [Lagoon](WaterReferenceLagoon.png). 추가 A/B 및 Pool 캡처는 `Temp/WaterReference/`에 있다.

## 씬 이동 투명도 보정 (2026-09-10)

다른 씬으로 물 오브젝트를 복사했을 때 투명하게 보이던 현상을 보정했다. 얕은 수심에서는 기존 셰이더가 배경색을 거의 그대로 투과하고, 복사한 씬의 카메라가 URP Depth/Opaque Texture를 만들지 않으면 굴절·수심 계산이 약해지는 것이 원인이었다. 또한 BattleScene의 물 높이가 타일 상단과 맞지 않아 표면이 가려질 수 있었다.

- `ShallowWaterSceneSupport`가 물 오브젝트에 포함되어 활성 카메라의 URP Depth/Opaque Texture 옵션을 자동으로 켠다.
- `Assets/Resources/Prefabs/ShallowWaterPortable.prefab`을 새 씬에 배치하면 동일한 지원 컴포넌트와 물 메시/머티리얼을 함께 가져온다.
- 얕은 광학 경로에도 최소 표면 색을 적용해 수면이 배경과 완전히 같아지지 않도록 셰이더를 보강했다.
- `BattleScene`의 `ShallowWater` 위치를 타일 상단 기준 y=0.95로 저장하고 Beach Reference 머티리얼을 연결했다.
- 수정 후 Unity 셰이더/스크립트 컴파일 오류 0건, Play Mode 카메라 렌더 확인 및 종료를 완료했다.

## 배틀 씬 재배치 및 현재 검증 (2026-09-10)

- 현재 편집 중인 BattleScene의 미저장 변경을 보존해 저장했다. 작업 전 복사본은 `Temp/BattleWater/BeforePlacement.unity`에 있다.
- `Battle Shallow Water`를 (8, 0.95, 8)에 배치하고 16×16 전투판 전체에 맞췄다. 타일 상단 y=0.7 기준 약 0.25m 깊이다. 기존 반사 프로브와 물 프리팹을 재사용했다.
- 현재 Beach Reference 설정을 복제한 전용 `ShallowWater_Battle.mat`을 연결했다. 기존 공유 머티리얼은 변경하지 않았다.
- 안개가 꺼진 경우 URP의 `ComputeFogIntensity`가 0을 반환해 물색과 굴절까지 사라지던 셰이더 결함을 수정했다. 안개 없음에서는 물 효과를 유지한다.
- 에디터 카메라로 안개 끔/켬 렌더를 확인했고 원래 안개 끔 설정으로 복원했다. ShaderUtil 메시지 0건. 물 충돌체 0개이며 하향 Raycast는 Plane/GrayboxBoardPlane에 도달했다.
- **현재 Play Mode 및 테스트 실행은 미검증**: 기존 `OutgameRuleRegressionTests.cs` 178/208행의 `TextMeshProUGUI` CS0246 오류 2건으로 진입이 막혔다. 앞 절의 과거 성공 기록은 이번 검증 결과가 아니다. 관련 테스트 파일은 변경하지 않았다.
- 오류 해결 후 BattleScene에서 Play를 실행해 유닛/타일 선택과 이동 중 수면 표시를 확인하고 콘솔 예외를 확인해야 한다. 작업 종료 시 Play Mode는 꺼져 있다.

현재 배치 화면: [BattleWater.png](BattleWater.png).
