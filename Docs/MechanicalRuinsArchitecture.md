# 유적 건축 모듈 UV·재질 작업 — 2026-09-13

## 산출물

- 원본: `C:/Users/hdyun/Downloads/Ruin.blend`. 원본 파일은 덮어쓰지 않았다.
- 미저장 편집을 포함한 백업: `ArtSource/MechanicalRuins/Ruin_before_UV_20260913_010833.blend`
- 작업본: `ArtSource/MechanicalRuins/Ruin_textured_20260913_010833.blend`
- Unity 모듈: `Assets/Environment/MechanicalRuins/Architecture/Prefabs/` (13개)
- FBX 13개, 공용 URP Lit 재질 2개, 1024×1024 텍스처 8개.
- 검토 이미지: `ArtSource/MechanicalRuins/Ruin_Material_Review.png`, `Unity_Material_Review.png`
- 검사 기록: 같은 폴더의 `uv_validation.json`, `export_manifest.json`

## 처리

원형과 배치를 유지한 작업본에서 비균일 스케일을 메시로 적용했다. 하드서피스 자동 투영(66도, 간격 0.025)으로 UV를 분할·패킹했다. 원래 UV는 UV_Source_Backup으로 보존한다. UVMap은 반복 재질용이며 표면적에 따른 대략적인 반복 밀도를 적용했다. UV_Unique_Packed는 0~1 안의 고유 패킹본이다. 각 모듈의 동일한 텍셀 밀도나 수작업 심 배치가 완료되었다는 뜻은 아니다.

석재는 중성 회녹색, 기어·Crank·Cylinder는 산화 금속을 사용한다. 주기적인 다중 스케일 노이즈로 새 공용 텍스처를 만들었으며 타일 반복을 지원한다. BaseColor는 sRGB, Normal·Roughness·MetallicSmoothness는 선형 데이터다. Unity의 금속 마스크 R은 금속도, A는 smoothness다. 노멀 강도는 0.3으로 조정했다. 개별 모듈의 모서리 마모·균열·이끼를 베이크한 전용 텍스처는 포함하지 않는다.

FBX는 모듈마다 분리하고 내보내기 복제본의 원점을 바닥 중앙으로 옮겼다. Blender 원본 배치는 유지한다. FBX에는 UVMap과 UV_Unique_Packed 두 채널만 포함한다. Blender 작업본에는 원본 UV와 텍스처를 패킹해 보관했다. 기존 Smooth by Angle 모디파이어를 유지하며 내보내기에 반영했다.

## 검증

- Blender 메시 13개 모두 UV 면적 0인 폴리곤 0개, 고유 패킹 좌표는 부동소수점 허용 오차 안에서 0~1.
- 정밀 UV 중첩·왜곡 지표 및 실제 라이트맵 베이크는 미검증이다.
- Blender 재질 뷰포트와 전체 비교 렌더 확인. 전체 비교 렌더는 최종 노멀 강도 완화 전 캡처다.
- Unity 6000.3.10f1의 연결 프로젝트 경로 확인, 13개 모델 모두 정점 수와 일치하는 UV0/UV1 채널 확인.
- 프리팹 13개 생성, 대표 기어·기둥·계단을 Unity PreviewRenderUtility로 렌더 확인하고 요철·광택 완화 후 재촬영.
- 최종 Unity 콘솔 오류 및 경고 0건. Play Mode에는 진입하지 않았다. 새 아트 임포트 작업으로 게임 회귀 테스트는 재실행하지 않았다.
- Windows Computer Use 캡처는 `SetIsBorderRequired ... 0x80004002` 오류로 재시도 후 중단. Blender MCP와 Unity MCP 렌더로 검토했다.

## 배치 전 확인

- Blender 원래 크기를 보존했다. Crank 약 183.6×183.6×213.5m, BigGear 최대 지름 약 49.55m. 게임 기준 크기가 확정되면 프리팹에서 조정해야 한다.
- BigGear 비정상 연결 에지 8개, BigGearBroken 2개 및 열린 경계 4개. LowWall 경계 96개, Pillar·Crank 경계 각 8개. 의도된 구조인지 판단 전 자동 용접·구멍 메우기는 하지 않았다.
- 충돌체, LOD, 스냅 규격, 실제 BattleScene 배치는 포함하지 않는다. 새 Prefabs 폴더에서 원하는 모듈을 배치할 수 있다.

## GLM 짧은 검토

칙령 화면의 런타임 구성 및 수용량 에셋 로딩, BattleRewardService와 결과 화면의 보상 가드, 공명 2단계 초기 SP +10 연결을 코드·인계서에서 확인했다. 루트 editmode-results.xml은 2026-09-12 한국 시간의 118/118 Passed 기록이다. 오늘 재실행한 결과는 아니다.

보상 중복 방지는 BattleResultUI 인스턴스의 `_rewardGranted`에 의존한다. 서비스는 호출마다 반복 보상을 지급하므로 별도 결과 인스턴스가 같은 전투에 생성되는 경우까지 막는 전투 ID 기반 보장은 없다. GLM 인계서에는 후속 검증 이후에도 오래된 '미실행' 문단이 남아 있어 최신 검증 구간과 XML을 우선 대조했다. 이번에는 게임 코드를 수정하지 않았다.
