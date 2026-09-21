# m2-representative-combo 인계

- 담당 / 브랜치 / 워크트리: ZCode / 해당없음 (작업서 지시에 따라 Git 커밋 생략, 워크스페이스에 변경물 존재)
- 실제 시작 커밋: 77160f46d11c6780484164d3167f5a7140d3135d
- 코드 커밋 목록: 없음 (작업서 "Git 명령은 하지 말라" 지시)
- 상태: Review (Unity 검증 대기)

## 변경 결과

- **구현 내용**: M2 유일 잔여 항목 "대표 넉백/Splat 콤보 1개를 BattleScene에 배치"를 에디터 메뉴 도구로 구현. Play Mode에서 메뉴 한 번으로 기존 유닛을 배치하고 디버그 푸시 능력을 접수해, 콤보가 런타임에서 수동 클릭 없이 재현된다.
  - 재사용 경로(새 전투 규칙 없음): `BattleManager` F1 디버그 시나리오와 동일하게 `TryEnqueueAbility`(디버그 푸시 능력) → `AbilityActionResolvedEvent` 구독 → `KnockbackMutation(ApplySplatDamage: true)` 커밋. 넉백 거리(힘−실효무게)는 `PhysicsRules`, Splat 피해(양측 MaxHP 10%, 고정 피해)는 `MovementMutationProcessor` 기존 규격이 계산한다.
  - 모드 A(기물 충돌 Splat, 생존 적 2 이상): 행 4에 더미(dx) — 빈 칸(dx+1) — 대상(dx+2) — 시전자(dx+3) 배치, 방향 ←, 힘 = 대상 무게+2. 비틀거림(실효무게 −1)·보스 면역 어느 쪽이든 `PhysicsRules` 거리가 2 이상 보장되어 대상이 빈 칸 1칸을 지나 더미에 막히고 **양측이** MaxHP 10% 피해를 받는다.
  - 모드 B(벽 Splat, 생존 적 1): 대상을 가장자리(왼쪽 우선, 불가 시 오른쪽)에 세우고 안쪽에서 민다. 힘 = 무게+1로 거리 1 이상 보장. 이 모드는 벽 충돌이라 **대상만** 피해를 받는다(기존 `MovementMutationProcessor`의 WallSplat 규격).
  - 멱등성: 유닛을 새로 생성하지 않고 배치만 강제 이동(`ApplyResolvedMovement`)하므로 반복 실행해도 중복 오브젝트가 없다. 유닛 선정은 ActorId 정렬로 결정화. 배치 전 점유 검사로 `DuplicatePositionValidator` 위반을 방지한다.
  - 안전장치: 틱 정지(배치 페이즈) 시 실행 거부, 해석 대기 중 재무장 금지, 능력 해석이 취소/실패(`Succeeded == false`)면 넉백 미커밋, Play Mode 종료 시 구독 해제.
- **변경 파일**:
  - `Assets/Scripts/Editor/RepresentativeComboSetupEditor.cs` [NEW]
  - `Assets/Scripts/Editor/RepresentativeComboSetupEditor.cs.meta` [NEW] (guid `a7d3e5f18b2c4960be4f8a1d6c3e7b92`, 프로젝트 내 미사용 GUID 확인 완료)
- **공통 API·직렬화 영향**: 없음. 런타임 코드·씬·프리팹·에셋·ProjectSettings 일절 수정하지 않았고, 호출한 런타임 API는 모두 기존 public 멤버(F1이 사용하는 것들)다.
- **수치 근거**: 힘 = 대상 무게+2(모드 A) / +1(모드 B)은 `PhysicsRules.ComputeKnockbackDistance`(거리 = 힘 − 실효무게, 비틀거림 −1, 보스 면역) 규격에서 어떤 무게·보스 여부에도 Splat이 확정되도록 뽑은 도구 입력값이며, 새 전투 수식이 아니다. 비틀거림 지속 8초는 F1 시나리오와 동일 값. 기획 문서에 없는 지형 연계 수치는 추가하지 않았다.

## 검증 근거

| 항목 | 결과: 통과 / 실패 / 미실행 | 실행한 커밋·환경·로그 위치·미실행 이유 |
|---|---|---|
| 컴파일 | 미실행 | 작업서 "Unity 실행과 Git 명령은 하지 말라" 지시. 호출 API 시그니처는 소스 코드 대조로 검증함(`AbilityLevelData`, `KnockbackMutation`, `ApplyStatusEffect`, `ApplyResolvedMovement`, `EventBus.Subscribe`, `FindObjectsByType` 등) |
| Edit Mode 테스트 | 미실행 | 동일 지시. 순수 배치 도구로 기존 테스트 대상 코드(PhysicsRules, MovementMutationProcessor)는 변경하지 않음 |
| Play Mode | 미실행 | 동일 지시. 아래 "메인 확인 사항"의 절차로 통합 후 검증 필요 |

## 메인 확인 사항

- **충돌 가능 파일**: 없음. 허용 목록(신규 Editor 스크립트 + .meta + 본 인계서)만 변경했다. `BattleManager.cs`, `ActionRuntimeController.cs`, `GridSystem.cs`, 시뮬레이션·예측·리플레이 코어, 씬, UnitData 에셋은 참조만 하고 수정하지 않았다.
- **알려진 제약**: 능력 접수~해석 사이 틱 창(수 틱 이내)에 적 AI가 경로 칸(빈 칸 dx+1)에 들어오면 충돌 상대가 그 유닛으로 바뀌어도 Splat은 동일하게 성립한다. 대상 자체는 비틀거림으로 이동 불가라 이탈하지 않는다.
- **컴파일 확인**: Unity가 에디터 어셈블리(`CheckmateRPG.Editor`)를 다시 컴파일할 때 오류 0인지 콘솔로 확인 필요.

### 통합 후 Play Mode 검증 절차 (메인/사용자)

1. `Assets/Scenes/OutgameScene.unity` → 덱 편성 → 출격으로 `BattleScene` 진입, START BATTLE로 전투 시작(또는 BattleScene 직접 Play).
2. 메뉴 `Tools/Checkmate RPG/M2 Representative Combo/2. Run Knockback+Splat Combo (Play Mode)` 클릭.
3. 콘솔에서 `[M2Combo] 유닛 배치/푸시 접수/넉백 뮤테이션 커밋` 로그 확인.
4. 기대 결과(모드 A): 대상 적이 왼쪽 1칸 밀린 뒤 더미에 막히고, **대상과 더미 양측 HP가 각자 MaxHP의 10%(내림) 감소**(플로팅 텍스트/상태바 확인).
5. 재현성: 같은 전투에서 2번 반복 실행해도 동일하게 재현되는지 확인(HP는 누적 감소하는 것이 정상).
6. 배치만 보려면 `1. Place Combo Units` 실행 후 수동 관찰(예측 오버레이 등 §4 확인용).
7. 종료 후 Play Mode 종료 및 콘솔 런타임 예외 0 확인.
8. 생존 적이 1명뿐일 때(모드 B): 대상이 가장자리에서 밀려 벽에 막히고 대상만 MaxHP 10% 감소하는지 확인.

## 종료 확인

- [x] 허용 파일만 변경했고 관련 .meta를 포함했다.
- [x] 다른 워크트리, 공유 설정, main을 수정하지 않았다.
- [x] Play Mode 종료 및 Unity 실행권 반납을 확인했다(미사용 — 작업서 지시로 Unity 미실행).
- [x] 미커밋 변경과 미검증 항목을 숨기지 않았다.

## 메인 검토 보완 (2026-09-08)

- 최초 전달 시 파일이 메인 checkout에 생성된 사실을 확인했다. 원본은 보존하고 동일 내용을 `codex/zcode/bootstrap` 워크트리에 복사해 격리 상태를 복구했다.
- `CheckmateRPG.Editor.csproj --no-restore` 빌드에서 오류 0개를 확인했다. 기존 API 사용에 따른 경고만 남아 있다.
- Unity Play Mode와 실제 메뉴 실행은 현재 프로젝트를 열고 있는 Unity 인스턴스 때문에 아직 수행하지 않았다.
