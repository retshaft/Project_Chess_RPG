# M1 Baseline & Playability Lock

> Started: 2026-05-27
>
> Scope: `M1 Current Baseline & Playability Lock` 실행 로그 및 체크리스트

## Goal

현재 프로젝트를 시연 가능한 안정 기준선으로 고정한다.

## Checklist

| Item | Status | Notes |
|---|---|---|
| Git 기준선 확인 (`main`, 작업트리 상태) | Done | 현재 `main...origin/main`, 로컬 변경 없음 |
| `BattleTest` 씬/데이터 파일 존재 확인 | Done | `BattleTest.unity`, `PawnUnitData.asset`, `KnightUnitData.asset` 확인 |
| `BattleTest` 참조 GUID 1차 점검 | Done | 테스트 매니저/데이터 GUID 매칭 정상 |
| 패키지 컴포넌트 GUID 오탐 분리 | Done | 일부 GUID는 Assets가 아닌 URP 패키지 컴포넌트 참조로 판단 |
| `.meta` 추적 위험 재점검 | Done | 전체 Assets 파일 기준 `.meta` 누락 0건 확인 |
| Fresh clone 기준 Play Mode 실행 검증 | Pending | 현재 작업 환경에서는 Unity 에디터 Play Mode 직접 실행 불가, 사용자 로컬 검증 필요 |
| 콘솔 오류/누락 스크립트 여부 점검 | Pending | Play Mode 실행 시점 로그 수집 필요 (본 환경에서 직접 수집 불가) |
| M1 종료 기준 정리 | Pending | 검증 후 Acceptance Criteria 체크 |

## Findings (Today)

1. `BattleTest` 내부의 `_pawnData`, `_knightData`, `TestSceneBattleManager` GUID는 실제 `.meta`와 일치한다.
2. 씬 안에서 조회되지 않던 2개 GUID는 URP 카메라/라이트 추가 컴포넌트로 보이며, 사용자 코드 누락으로 단정할 수 없다.
3. 현재 Git 기준선은 깨끗해서 M1 점검 결과를 기준선으로 삼기 좋다.
4. `Assets` 전체 파일 기준 `.meta` 누락 항목은 현재 0건이다.
5. 저장된 `Logs/` 기준으로 치명 오류 문자열(`error`, `exception`, `missing script`)은 검출되지 않았다.

## Next Actions

1. 사용자 로컬 Unity 에디터에서 `BattleTest` Play Mode를 실행한다.
2. 콘솔 에러, Missing Script, AP HUD/입력 흐름을 확인한다.
3. 확인 결과를 반영해 M1 종료 여부를 결정한다.
