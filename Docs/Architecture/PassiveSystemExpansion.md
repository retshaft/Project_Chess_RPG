# 패시브 시스템 데이터 기반 확장 방향성 기획

## 현재 구조와 한계점
현재 Kiara의 패시브 로직은 `KiaraPassiveController` (MonoBehaviour)와 `KiaraPassiveReactionTrigger` (IReactionTrigger)라는 하드코딩된 스크립트로 구현되어 있습니다.
새로운 캐릭터를 추가할 때마다 이런 전용 스크립트를 작성하면, 확장과 밸런싱이 어렵고 유지보수 비용이 증가합니다.

## 장기적인 아키텍처 개편안 (데이터 기반 패시브)

장기적으로 캐릭터 패시브도 액티브 스킬이나 버프처럼 **데이터 드라이븐(Data-driven)** 방식으로 개편하는 것이 좋습니다.

### 1. `PassiveDefinitionSO` 도입
패시브 효과를 정의하는 별도의 ScriptableObject를 도입합니다.
- **Trigger 조건**: 이벤트(예: `OnAttack`, `OnHit`, `OnDamaged`, `OnBleedApplied`)
- **조건식 (Condition)**: 발동 확률, 대상 상태(출혈 여부 등), 남은 HP 비율 등
- **발동 효과 (Effects)**:
  - 스탯 수정 (StatModifier Profile 부여)
  - 새로운 이펙트/돌연변이 발생 (DamageMutation, ApplyEffectMutation)
  - 쿨다운 감소 등 특수 효과

### 2. 범용 `GenericPassiveComponent`
캐릭터에 고유의 패시브 컨트롤러 대신 `GenericPassiveComponent` 하나만 부착하고, `PassiveDefinitionSO` 목록을 할당합니다.
이 컴포넌트가 알아서 이벤트 버스(`IEventBus`)를 구독하고, 지정된 트리거 조건이 맞을 때 효과를 실행합니다.

### 3. StatModifier 시스템 연동
최근 추가된 `StatModifierProfileSO`와 `StatModifierType` 시스템을 패시브에도 그대로 활용할 수 있습니다.
예를 들어, "적이 출혈 상태일 때 방어력을 15% 무시한다"라는 패시브는,
- `OnAttack` 이벤트가 발생하기 전 타겟의 출혈 여부를 확인
- 조건 통과 시 이번 공격에만 일시적으로 `DefPenetrationFlat = 0.15` 수정자를 부여
하는 식으로 데이터화할 수 있습니다.

## 결론
이번 작업에서 구축한 `StatModifier` 시스템과 `AreaDamage` 시스템이 향후 모든 데이터 기반 개발의 뼈대가 될 것입니다. 장기적으로 하드코딩된 패시브 컨트롤러들을 제거하고 이 뼈대 위에 `PassiveDefinitionSO`를 올려 완전히 데이터만으로 캐릭터를 기획하고 밸런싱할 수 있는 환경으로 나아가야 합니다.
