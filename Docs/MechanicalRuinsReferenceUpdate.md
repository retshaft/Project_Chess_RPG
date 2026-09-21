# 유적 레퍼런스 반영 — 2026-09-13

사용자가 제공한 첫 이미지는 배경 분위기, 나머지 세 장은 기존 모듈의 재질·장식 기준으로 사용했다. 이미지 내 제목이나 GAME READY 표기는 실제 에셋 검증 근거로 사용하지 않았다. 원본 이미지는 ArtSource/MechanicalRuins/References에 보관했다.

## 결과

- Blender: ArtSource/MechanicalRuins/Ruin_reference_refined.blend
- 비교 렌더: ArtSource/MechanicalRuins/Reference_Material_Review.png
- Unity 렌더: ArtSource/MechanicalRuins/Unity_Reference_Review.png
- 사용 프리팹: Assets/Environment/MechanicalRuins/Architecture/ReferenceVariant/Prefabs (13개)
- 재질 4개: 석재, 이끼 석재, 황동, 잎. 새 텍스처 9개, 모듈 FBX 13개.

회색 석재의 사각 줄눈과 블록별 명도, 기둥 및 링의 표면에 맞춘 금속 띠, 일부 기둥의 원형 기계 장식, 표면 레이캐스트로 부착한 잎과 연결 줄기를 추가했다. 기존 모듈 실루엣과 크기는 보존했다. 기존 버전을 덮어쓰지 않는 별도 ReferenceVariant다.

첫 이미지의 침수 배경 전체를 새로 배치한 작업은 아니다. 레퍼런스 시트의 모든 신규 모듈 36종이나 개별 조각·균열을 재현한 것도 아니다. 현재 모델을 바탕으로 재질과 장식을 보완했으며, 제공 이미지보다 단순한 기하와 디테일을 가진다. 실제 배경에서의 크기 조정·배치·조명 일치는 후속 검토 대상이다.

## UV·출력

석재 줄눈 방향을 유지하도록 재질 UV에 면 방향별 투영을 적용했다. 곡면·모서리의 패턴 전환과 정밀 왜곡 검사는 최종 수작업 보완 대상이다. Blender의 기존 고유 UV 백업을 유지했고, 장식까지 합친 FBX의 보조 UV는 Unity에서 생성했다. 프리팹은 최대 4개 서브메시/재질로 표현된다. 식생은 소형 메시이며 바람 애니메이션은 없다.

## 검증·복구

- 13개 Unity 모델의 UV0/UV1 정점 수 일치, 누락/비 URP 재질 0개.
- 대표 모듈의 Blender 및 Unity 비교 렌더 확인.
- 최종 Unity 콘솔 오류·경고 0건. 아트 임포트 작업으로 Play Mode 및 게임 테스트는 미실행.
- 최초 내보내기에서 순회 중 복제본을 추가한 코드가 Blender를 정지시켰다. 직전 저장 파일을 확인하고 해당 프로세스를 재시작해 복구했다. 원본 목록을 고정하는 export_reference.py로 수정하고 13개 내보내기 성공 및 렌더 종료 코드 0을 확인했다. 복구된 Blender MCP 연결도 확인했다.
- refine_reference.py는 초기 재질/잎 생성 기록이다. 후속 금속 띠·문양·줄기는 저장된 blend에 포함되며 해당 스크립트만으로 전 과정을 재생할 수 있는 것은 아니다.
- 작업본에는 Reference_Material_Review 씬을 열어 두었다. Scene 씬에 실제 크기 모듈과 별도 장식 메시가 있다.
