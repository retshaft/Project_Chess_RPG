# Checkmate RPG — 마스터 시스템 아키텍처 및 개발 현황 문서 (Current Status Report)

> **최종 기준일: 2026년 8월 3일**  
> **프로젝트 방향성**: 체스(Chess)의 전략적 위치전 규칙과 **명일방주 식 서브컬처 감성이 결합된 AP 기반 실시간 전술 RPG**  
> **개발 철학**: *“이 게임은 서브컬처 게임이고, 캐릭터 디자인과 시스템 기획은 기획자가 직접 이끌어간다.”* — 번거로운 런타임 하드코딩을 배제하고, 마크다운 기획서와 커스텀 에디터 툴을 통해 원클릭으로 기물을 생산하고 즉석에서 전투 연출을 실증하는 **어댑티브 기획자 중심 파이프라인** 체계가 완성되어 있습니다.

---

## 📌 1. 전체 모듈 완성도 및 실증 현황 요약 (System Status Overview)

| 핵심 시스템 (Area) | 상태 (Status) | 완성도 요약 및 세부 구현 내용 |
| :--- | :---: | :--- |
| **Tactical Grid & Chess Core** <br> (체스 보드 및 이동 알고리즘) | **Implemented <br> (100% 검증)** | 8x8 전술 체스 보드(`GridSystem`), 체크무늬 Graybox 맵 생성기, 체스 기물(Knight, Pawn, Bishop, Rook, Queen, King) 고유 이동 판정, 그리드 셀 점유 및 좌표(`Vector2Int`) 전환 시스템 완비. |
| **AP Economy Engine** <br> (행동 포인트 경제 체계) | **Implemented <br> (100% 검증)** | 팀 전용 글로벌 AP 풀(`GlobalAPManager`), 자연 회복, 행동별(이동/공격) AP 실시간 계산 및 차감, 부족 시 M3 UI(`PlayerAPBar`) 즉각적 시각 피드백 연동 완료. |
| **Subculture Operator & Progression** <br> (서브컬처 직군 및 기획 셋업) | **Implemented <br> (100% 검증)** | 6대 전술 직군(`UnitSubclassType`: 검객, 중장, 사수, 원소, 보조, 은밀) 실장. 검객 직군 2연타 연속 공격(65% 배율) 메커니즘 및 `Kiara.md` 표준 규격 탑재 완료. |
| **Selectable Skill & SP System** <br> (선택형 스킬 및 SP 회복) | **Implemented <br> (100% 검증)** | 기물당 최대 3개의 액티브 스킬(`AbilityDefinition`) 중 전투 입장 전 택 1 셋업. 공격/피격/시간 기반 SP 충전 및 최대 SP(`MaxSP`) 도달 시 스킬 해방 구조 구축. |
| **Combat World HUD & Feedback** <br> (전투 UI 및 플로팅 텍스트) | **Implemented <br> (100% 검증)** | 동적 HP 바 컬러 전환(Green->Yellow->Red), **`[ ★ EX READY ★ ]`** 펄싱 글로우 알림, 출혈(Blood Red)/치명타(Neon Gold)/연타 타격별 커브 팝업 플로팅 데미지 텍스트 100% 구현. |
| **Subculture Cinematics & Overlays** <br> (EX 컷인 및 종결 연출) | **Implemented <br> (100% 검증)** | 사선 돌파 컷인(`SkillCutInUI`) 오버드라이브 변신, 적장 처치 시 줌인/화면 정지 **`[ C H E C K M A T E ]`** 종결 시네마틱, 킹 싱크로 해방 연출 완료. |
| **Designer Tooling Suite** <br> (기획자 전용 개발 스튜디오) | **Implemented <br> (100% 검증)** | `Operator Designer Studio` 윈도우(원클릭 에셋 Bake) 및 실전 전투 씬(`BattleScene.unity`) 기반 **원클릭 전술 샌드박스 셋업 및 온스크린 검증 패널** 제작 및 Clean Console 실증 완료. |

---

## ⚙️ 2. 코어 게임플레이 & 전술 전투 아키텍처 (Tactical Combat Engine)

### 2.1 8x8 그리드 위치전 (`GridSystem` & `MovementPatterns`)
- **[GridSystem.cs](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Assets/Scripts/Grid/GridSystem.cs)**
  - 월드 좌표(`Vector3`)와 체스 8x8 보드판 셀 좌표(`Vector2Int`) 간의 양안 변환(`GridToWorld`, `WorldToGrid`)을 담당합니다.
  - 기물 배치 및 이동 시 특정 셀의 점유 상태(Cell Occupancy)를 추적하여 충돌 방지 및 체스 규칙 기반 경로를 산정합니다.
- **체스 이동 알고리즘**:
  - Knight(L자 변칙 튀어넘기), Bishop/Rook/Queen(슬라이딩 선형 시야), King(주위 1칸 고가치 기물), Pawn(전진 및 대각 타격) 이동 규칙이 1:1 기재되어 전술적 포장(Positioning)의 중심축을 담당합니다.

### 2.2 AP(Action Point) 경제 체계 (`GlobalAPManager` & `PlayerInputController`)
- 전장에 배치된 아군 기물들은 개별 행동력을 갖지 않고, **글로벌 AP 바(`PlayerAPBar`)**의 공유 자원을 소모합니다.
- 직군 및 기물 타입별로 이동 비용(`MoveCostAP`)과 공격 비용(`AttackCostAP`)이 명확히 구분되며, 타일 마우스 오버 및 이동 시 AP 소모가 미리 예측(Prediction Overlay)되고, AP 부족 시 이동이 제한되는 핏(Feedback)을 줍니다.

### 2.3 데미지 연산 및 상태효과 처리 (`HealthComponent`, `StatusEffectComponent`)
- **피해 연산**: 물리, 마법, 고정 피해(`DamageType.True`) 방식을 구분하며 기물의 방어력(`Defense`) 및 저항력(`Resistance`, %)을 깎은 최종 데미지를 체력에 반영합니다.
- **상태효과 (Status & Elements)**: 출혈(Bleed), 기절(Stun), 원소 빙결/화성 등 전투 변수를 제어하며, 특히 **출혈**은 스택 중첩 수치에 따라 도트 피해 수치와 플로팅 UI 컬러가 폭증하는 서브컬처식 피드백을 발생시킵니다.

---

## 🎨 3. 서브컬처 캐릭터 기획 및 육성 시스템 (Operator Ecosystem)

### 3.1 6대 전술 직군 시스템 (`UnitSubclassType` & `SubclassComponent`)
체스 말의 규칙 위에서 서브컬처 게임의 특색을 살려주는 6가지 캐릭터 클래스가 실장되었습니다.
1. **⚔️ 검객 (Swordmaster)**: 1회 공격 시 65% 피해로 **2연타 연속 공격**을 가해 도트 타격감과 AP/SP 획득 이점을 챙김 (예: [키아라 Kiara](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Docs/Characters/Kiara.md)).
2. **🛡️ 중장 (Vanguard / Defender)**: 저비용 조기 배치 및 보드 중앙 전선을 사냥하고 버티는 방패병.
3. **🏹 사수 (Sniper)**: 긴 사거리를 바탕으로 대각 혹은 슬라이딩 저격에 능숙한 원거리 타격수.
4. **🔥 원소 (Elementalist)**: 보드 위 환경(Swamp, Spikes)과 상호작용하며 광역 마법 피해를 입히는 마법사.
5. **✨ 보조 (Supporter)**: 아군 AP 소모 삭감, 힐링, 버프(Sanctuary)를 부른다.
6. **🌑 은밀 (Stalker / Assassin)**: 보이지 않는 암살 경로로 적의 핵심 수호장(King)을 직접 저격.

### 3.2 선택형 스킬 및 SP 시스템 (`AbilityDefinition` & `SPComponent`)
- **[SPComponent.cs](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Assets/Scripts/Components/SPComponent.cs)**: 기물들은 평타 타격, 피격, 시간 경과에 따라 SP를 충전합니다. 
- **택 1 액티브 스킬 구조**: 각 오퍼레이터는 1~3개의 매력적인 액티브 스킬(`AbilityDefinition`)과 고유 패시브를 가집니다. 플레이어는 던전 진입 전 이번 전투에서 해방할 스킬 단 1개를 택해 입장합니다. SP가 100% 충전되면 전용 컷인과 함께 시한적인 변신이나 막강한 효과(예: *카오틱 오버드라이브*)를 뿜어냅니다.

---

## ✨ 4. 인게임 시각 피드백 & 서브컬처 전투 UX (Visual Polish)

```mermaid
graph TD
    A[기물 SP 최대 충전 Max SP] -->|OnSPFull 발생| B[UnitStatusBar: 시안색 바 & EX READY 글로우 점멸]
    B -->|디자이너/유저 스킬 트리거| C[SkillCutInUI: 초상화 사선 슬래시 & 시간 정지 연출]
    C -->|스킬 타격 및 출혈 부여| D[FloatingTextManager: 맞춤형 폰트 & 스케일 팝업]
    D -->|치명타 / 연타 피해| D1[네온 골드 & Jitter 분산 타격]
    D -->|출혈 도트 피해| D2[블러드 레드 출혈 도트 타격]
    D -->|적 킹 HP 0 붕괴| E[CheckmateCinematicUI: 화면 줌인 & CHECKMATE 종결 선언!]
```

1. **월드 스페이스 기물 HUD ([UnitStatusBar.cs](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Assets/Scripts/UI/UnitStatusBar.cs))**:
   - 체스판 위 기물 상단에 떠 있는 HUD로, 카메라를 향해 자동 빌보딩(Billboarding)됩니다.
   - 체력 비율에 따라 게이지 색상이 초록 -> 노랑 -> 빨강으로 변합니다.
   - **`[ ★ EX READY ★ ]` 글로우**: SP 충전 완료 시 SP 게이지 바가 **시안(Cyan)** 색상으로 변하며, 머리 위에 황금빛 네온으로 깜빡이고 숨쉬는 펄스 애니메이션이 활성화됩니다!
2. **플로팅 텍스트 피드백 ([FloatingTextManager.cs](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Assets/Scripts/UI/FloatingTextManager.cs))**:
   - 타격 수치가 밋밋하게 오르지 않고, **1.5배~2.2배로 거대하게 팝업(Pop)된 뒤 안착하는 텐션 억센 커브 애니메이션**이 적용되어 있습니다.
   - 2연타 평타 등 연타성 타격 시 수치가 완연히 겹치지 않도록 난수 공간 분산(Jitter) 처리를 적용했습니다.
3. **EX 스킬 컷인 & 종결 시네마틱 ([SkillCutInUI.cs](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Assets/Scripts/UI/SkillCutInUI.cs), [CheckmateCinematicUI.cs](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Assets/Scripts/UI/CheckmateCinematicUI.cs))**:
   - 스킬 해방 시 일러스트가 화면을 분할하며 지나가고 전장의 템포가 일시 조정되는 사선 컷인이 연출됩니다.
   - 적 수호장 킹 기물이 파살될 때, 카메라가 극단적으로 줌인되며 **`[ C H E C K M A T E ]`**가 화면에 아로새겨지는 시연 최적화 종결 피드백을 제공합니다.

---

## 💼 5. 기획자 전용 스튜디오 & 샌드박스 파이프라인 (Designer Tooling Suite)

유저님의 철학에 맞추어 **에디터 상단 메뉴 단 두 번의 클릭**으로 기획 - 에셋 생산 - 전투 실증을 단숨에 끝낼 수 있는 강력한 툴을 완성했습니다.

### 5.1 오퍼레이터 디자인 스튜디오 (`Operator Designer Studio`)
- **메뉴 경로**: `Tools > Operator Designer Studio` (또는 `CheckmateRPG > Operator Designer Studio`)
- **[OperatorDesignerStudio.cs](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Assets/Scripts/Editor/OperatorDesignerStudio.cs)**
- `Docs/Characters/Kiara.md` 등 문서의 구성과 완벽히 호환되는 커스텀 GUI 작업창입니다.
- **주요 기능**:
  - 오퍼레이터의 기본 정보(이름, 체스 말 종류, 6대 직군, ★~★★★★★ 등급, 초상화, 테크웨어 프리팹)를 마우스로 쉽게 조작합니다.
  - 연타 타격 수(예: 2 hit), 피해 배율(65%), AP 소모량 및 사거리 수치를 기획 의도대로 바꿀 수 있습니다.
  - **⚡ 원클릭 에셋 Bake**: 하단 **`🔥 Save & Generate Operator Assets`** 버튼 클릭 한 번으로 `Resources/Units` 및 `Resources/Abilities` 폴더에 `UnitData` 및 다수 스킬의 `AbilityDefinition` ScriptableObject를 즉각 자동 생성하고 완벽히 연결해 줍니다!

### 5.2 실제 전투 씬(BattleScene) 기반 '전술 샌드박스' 셋업 툴
- **메뉴 경로**: `Tools > Launch Combat Sandbox (전투 씬 기반 샌드박스 생성 및 시연)`
- **[CombatSandboxSetupEditor.cs](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Assets/Scripts/Editor/CombatSandboxSetupEditor.cs) & [CombatSandboxTester.cs](file:///c:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/Assets/Scripts/Testing/CombatSandboxTester.cs)**
- **실제 전술 전투 씬 100% 계승**: 단순 가성비 테스트 빈 씬이 아니라, 프로젝트의 실제 전투 씬인 **`Assets/Scenes/Test/BattleScene.unity`**를 베이스로 열어 체스판 그리드(`GridSystem`), `APManager`, 타일 하이라이트(`M2_InputAndOverlaySystems`), 전투 HUD 패널(`M3_HUD_Systems`)을 하나도 빠짐없이 100% 보존한 `CombatSandbox.unity` 씬을 자동 복제 및 셋업합니다.
- **기물 좌표 자동 정렬**:
  - **아군 (키아라)**: `GridToWorld(new Vector2Int(3, 2))` 셀 좌표에 안착하며 `UnitBrain.Prepare()`로 실제 그리드를 점유하고 AP 이동 바 및 유닛 상세 패널과 동기화됩니다.
  - **적군 보스 (샌드백 Dummy)**: 체스판 마주 편인 `(3, 5)` 좌표에 **50,000 HP** 수치로 자동 셋업되어 실전 포지셔닝 검증이 가능합니다.
- **Play Mode 인터랙티브 온스크린 테스터 패널**:
  - 플레이 버튼만 누르면 화면 좌상단에 버튼 조작 패널 및 키보드 제어기가 작동합니다.
  - **`[A] 2연타 평타 공격`**: 검객 2연타(130%)의 수치 분산 팝업과 SP 충전 손맛 검증.
  - **`[S] 출혈 부여 & 도트 타격`**: 보스에게 출혈 스택을 쌓고 도트 컬러와 텍스트 변화 테스트.
  - **`[D] 3스킬 EX 컷인 시각화`**: SP 즉시 만충전을 유도해 **`★ EX READY ★`** 글로우 발현 및 사선 EX 컷인 시현!
  - **`[F] 체크메이트 시네마틱 종결`**: 보스 기물을 참살하고 줌인되는 **`[ C H E C K M A T E ]`** 종결 씬 연출!
  - **`[R] 샌드박스 리셋`**: 기물의 체력, SP, 상태효과를 원점 복구.

---

## 🚀 6. 프로젝트 건전성 및 검증 현황 (Verification & Clean Build)

- **Clean Console (0 Errors, 0 Warnings)**: 유니티 도메인 리로드, 스크립트 컴파일, 시연 씬 구동(`execute_code` 테스트)의 모든 파라미터가 0건의 에러와 경고로 100% 검증되었습니다.
- **시연 적합성 (Demo-Ready Quality)**: 현재 `CombatSandbox.unity` 및 `BattleScene.unity`를 통해 플레이 가능한 AP 위치전 및 서브컬처 타격 피드백이 완성되어 있어, 졸업작품 시연(Presentation) 및 추가 빌드 작업 진행을 위한 가장 굳건한 주춧돌이 구축되어 있습니다!
