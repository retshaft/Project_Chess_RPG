# **Interrupt Rule Specification**

## **실시간 전술 체스 RPG 인터럽트 규칙 문서 초안**

---

# **목적**

Interrupt 시스템은:

행동 중단 / 행동 실패 / 행동 변경

을 일관된 규칙으로 처리하기 위한 시스템이다.

본 시스템의 목적은:

* 행동 우선순위 명확화  
* 실시간 충돌 해결  
* 캐스팅/이동/공격 취소 처리  
* 상태 이상과 행동 흐름 통합  
* 이벤트 폭발 방지

이다.

---

# **핵심 원칙**

## **원칙 1**

모든 행동은 Interrupt 가능성을 가진다.

단,  
행동마다:

* Interrupt Window  
* Interrupt Immunity  
* Partial Resolve 여부

가 다를 수 있다.

---

## **원칙 2**

Interrupt는 ActionScheduler만 처리 가능하다.

다른 시스템은:

Interrupt 요청

만 가능.

실제 상태 변경은 Scheduler가 수행한다.

---

## **원칙 3**

Interrupt 발생 ≠ 즉시 상태 제거

Interrupt 역시:

Resolution Queue

를 통해 Resolve된다.

---

# **Interrupt 대상 행동**

Interrupt 가능한 행동:

| 행동 | 기본 Interrupt 가능 여부 |
| ----- | ----- |
| 이동 | 가능 |
| 공격 | 가능 |
| 캐스팅 | 가능 |
| 채널링 | 가능 |
| 회복 행동 | 가능 |
| 후딜레이 | 일부 가능 |

---

# **Interrupt 발생 원인**

## **Hard Interrupt**

즉시 행동 실패 처리.

예:

* Death  
* Stun  
* Freeze  
* Petrify

특징:

현재 행동 즉시 Cancel

---

## **Soft Interrupt**

행동 지연 또는 일부 변경.

예:

* Knockback  
* Slow  
* Forced Rotation  
* Minor Hitstun

특징:

행동 지속 가능할 수도 있음

---

# **Interrupt Source Category**

## **상태 기반**

* Stun  
* Silence  
* Root  
* Fear

---

## **피격 기반**

* Damage Threshold 초과  
* 특정 태그 공격 피격

---

## **위치 기반**

* 넉백  
* 위치 교환  
* 충돌  
* 범위 이탈

---

## **자원 기반**

* SP 부족  
* AP 부족  
* 탄약 부족

---

## **시스템 기반**

* 행동 강제 취소  
* 타임아웃  
* 사망 처리

---

# **Interrupt Severity**

추천 enum:

public enum InterruptSeverity  
{  
    None,  
    Minor,  
    Major,  
    Critical  
}

---

# **Severity 규칙**

| Severity | 효과 |
| ----- | ----- |
| Minor | Recovery 증가 |
| Major | 행동 취소 |
| Critical | 행동 취소 \+ 추가 패널티 |

---

# **Action Interrupt State Flow**

기본 행동 흐름:

Queued  
→ Executing  
→ Resolving  
→ Recovery  
→ Completed

---

# **Interrupt 발생 시**

## **Executing 중 Interrupt**

Executing  
→ Interrupted  
→ Cancelled

---

## **Resolving 중 Interrupt**

기본 규칙:

Resolve 단계 진입 후에는 취소 불가

즉:  
데미지 판정 이후는 대부분 확정.

---

## **Recovery 중 Interrupt**

기본 규칙:

Recovery는 행동 불가 상태일 뿐  
행동 자체는 완료된 상태

따라서:

* 후딜 감소 가능  
* 후딜 연장 가능  
* 후딜 취소 가능

---

# **Interrupt Window**

모든 행동은:

Interrupt 가능한 시간대

를 가진다.

예시:

| 행동 단계 | Interrupt 가능 여부 |
| ----- | ----- |
| Queued | 가능 |
| Casting | 가능 |
| Windup | 가능 |
| Resolve | 불가능 |
| Recovery | 제한적 |

---

# **Cast Interrupt Rule**

캐스팅 행동은:

Resolve 이전 피격 시 취소 가능

기본 규칙:

* Resolve 이전 → Cancel 가능  
* Resolve 이후 → 결과 보장

---

# **Movement Interrupt Rule**

이동은:

Forced Movement

에 의해 끊길 수 있다.

예:

* 넉백  
* 끌어당김  
* 위치 교환

---

# **Forced Movement 우선순위**

추천 규칙:

Forced Movement  
\>  
Normal Movement

---

# **Death Interrupt Rule**

사망은:

최상위 Interrupt

규칙:

* 현재 행동 즉시 종료  
* Queue 제거  
* Recovery 제거  
* TickScheduler 등록 제거

---

# **Silence Rule**

Silence는:

Ability Action만 차단

기본 공격/이동은 유지 가능.

---

# **Root Rule**

Root는:

Movement Action 차단

하지만:

* 공격 가능  
* 스킬 가능

---

# **Stun Rule**

Stun은:

모든 행동 차단

규칙:

* 현재 행동 Cancel  
* 신규 행동 예약 금지

---

# **Interrupt Immunity**

일부 행동은:

Interrupt Immunity

보유 가능.

예:

* 궁극기  
* 초대형 캐스팅  
* 보스 패턴

---

# **Immunity Level 추천**

public enum InterruptImmunity  
{  
    None,  
    MinorOnly,  
    MajorOnly,  
    Full  
}

---

# **Partial Resolve Rule**

중요.

일부 행동은:

부분 성공

가능.

예:

* 투사체 생성 완료 후 시전자 사망  
* 폭발 예약 완료 후 인터럽트

---

# **추천 규칙**

Resolve 시작 후 생성된 결과는 유지

---

# **Simultaneous Interrupt Rule**

동일 Tick 충돌 시:

우선순위:

Critical  
\>  
Major  
\>  
Minor

동일 Severity일 경우:

Action Speed 우선

동일 속도일 경우:

Queue Order 우선

---

# **Interrupt Event Flow**

추천 이벤트 흐름:

InterruptRequestedEvent  
→ InterruptValidatedEvent  
→ ActionInterruptedEvent  
→ ActionCancelledEvent

---

# **중요 규칙**

## **이벤트 직접 취소 금지**

이벤트 핸들러 내부에서:

금지:

action.Cancel();

반드시:

Scheduler에 Interrupt 요청

만 수행.

---

# **Tick 처리 순서**

추천:

1\. Tick Advance  
2\. Action Update  
3\. Interrupt Check  
4\. Resolve Queue  
5\. Effect Tick  
6\. Death Check  
7\. Event Dispatch

---

# **디버그 로그 필수**

Interrupt 발생 시 반드시 기록:

\- Source  
\- Target  
\- Interrupt Type  
\- Severity  
\- ActionId  
\- Tick  
\- Previous State  
\- New State

---

# **중요 제한사항**

금지:

Interrupt 중 즉시 RuntimeState 수정

금지:

Interrupt handler 내부에서 추가 Interrupt 재귀 실행

금지:

MovementComponent가 직접 행동 취소

---

# **최종 목표**

Interrupt 시스템 목표는:

"행동 충돌을 예측 가능하게 만들기"

이다.

플레이어는:

* 왜 행동이 취소되었는가  
* 왜 실패했는가  
* 왜 늦었는가

를 항상 이해 가능해야 한다.

Interrupt는:

복잡성을 추가하는 시스템이 아니라  
전술적 깊이를 만드는 시스템

이어야 한다.

