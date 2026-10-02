# CombatScene 유적 배경 셰이더 자산

현재 씬에 남아 있던 배경 배치는 다시 만들지 않고, `Resources/Models/Ruin/Textures`에 있는 오브젝트별 PBR 텍스처를 재사용할 수 있는 자산과 수동 배치 명령만 준비했다. 전투 보드의 이동 공간은 배치 명령을 실행하기 전까지 변경되지 않는다.

## 셰이더 그래프

- `Assets/Environment/CombatRuins/ShaderGraphs/RuinSurface_Master.shadergraph`
  - URP Lit 기반 표면 그래프다.
  - `_BaseMap`, `_NormalMap`, `_MaskMap`, `_BaseColor`, `_Metallic`, `_Smoothness`를 사용한다.
  - Unity의 Mask Map 규격에 맞춘 별도 패킹 파일이 없을 때는 오브젝트의 Roughness 맵을 `_MaskMap`에 연결하는 안전한 기본값을 사용한다. 최종 아트 패스에서 Metal/AO/Smoothness 채널을 패킹하면 더 정확해진다.
- `Assets/Environment/CombatRuins/ShaderGraphs/RuinMoss_Decal.shadergraph`
- `Assets/Environment/CombatRuins/ShaderGraphs/RuinWaterline_Decal.shadergraph`
  - URP Decal Master Stack 템플릿을 기반으로 한다.
  - `_Color` 텍스처의 알파와 `_Tint` 알파를 혼합해 물때·이끼 강도를 조절한다.
  - URP Renderer에 Decal Renderer Feature를 켜면 HDRP 데칼과 같은 투영 방식으로 사용할 수 있다. Renderer Feature를 켜지 않는 경우에는 기존 배치 도구의 메시 오버레이 방식으로 같은 마스크를 표면에 투영한다.

## 에디터 명령

Unity에서 텍스처와 Shader Graph import가 끝난 뒤 다음 메뉴를 실행한다.

1. `Tools/Checkmate/Background/Prepare Ruin ShaderGraph Materials`
   - 사용자 텍스처 세트에 맞춘 표면 머티리얼 7종과 데칼 머티리얼 2종을 `Assets/Environment/CombatRuins/Materials`에 만든다.
2. `Tools/Checkmate/Background/Compose Combat Ruins`
   - 사용자가 원하는 배치일 때만 실행한다. 현재 작업에서는 실행하지 않아 기존 씬 배치를 덮어쓰지 않았다.
3. `Tools/Checkmate/Background/Place CC0 Ivy Dressing`
   - `CC0 Ivy Vines` 그룹을 만들고 외곽 4곳에 덩굴을 배치한다. 기존 그룹이 있으면 그 그룹만 다시 만든다.

## 덩굴 자산

`Assets/Environment/CombatRuins/Vines/IvyCladStem.glb`는 glTFast로 가져오는 저폴리 덩굴 줄기다. 3DAssets.dev의 **Ivy-clad standing stem**을 사용했으며, 페이지에 명시된 CC0 1.0 Universal 자산이다.

- 원본: <https://3dassets.dev/assets/temperate-forest-ecology-ivy-clad-stem-28ae1e59>
- 다운로드: <https://cdn.3dassets.dev/assets/3843/v1/model.glb>
- 규모: 약 1.05 × 6.97 × 1.12 m, 1.3k triangles

## 검증 상태

이 작업 환경에서는 현재 Unity 에디터와 Unity MCP가 연결되어 있지 않아 Shader Graph import, Decal Renderer Feature 표시, 머티리얼 렌더링 및 Play Mode를 확인하지 못했다. Unity를 연 뒤 위 1번 명령으로 머티리얼을 만든 다음, 원하는 시점에 2·3번 명령을 각각 실행하고 콘솔 오류가 없는지 확인한다.
