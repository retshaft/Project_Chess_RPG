# Command UI follow-up — 2026-09-29

- Codex 메인, codex/integration 원본 작업. 기존 미커밋 변경 보존.
- 칙령 보드: 수용량 요약, 절대/일반 슬롯 분리, 극성 및 효과 정보 구획. 선택창 중립/혼돈 기호 구분.
- 전투 HUD: 초상/기물/기본 능력/HP/SP/스킬 구획, 선택 해제 및 사망 시 정보 초기화. 이동/공격/스킬 버튼 표시 정리.
- 사용자 수정 반영: 배치 코스트 표시 제거. 편성 카드와 교체 후보에 개별 COST, 편성 화면에 합계 및 인원 표시. UnitData.DeploymentCost 사용. 비용 상한이나 전투 규칙은 추가하지 않음.
- 메인 부관 없는 배경 유지. 씬/저장 데이터 수정 없음.

## 검증

- HUD/칙령 변경 후 EditMode CheckmateRPG.Tests 144/144 통과 (f2cb069c231541f1bdd2a6f34c84cdf3).
- 마지막 코스트 표시 수정 후 Unity 컴파일 완료, 신규 컴파일 오류 없음.
- OutgameScene Play: 칙령 빈 슬롯 및 표시 전용 장착 프리뷰 확인. 실제 저장/재화 지출 없음.
- 편성 Play: 카드 비용 3,2,2,2,2 및 합계 11, 인원 5/8 일치. 실제 Game View 스크린샷 확인.
- Test/BattleScene Play: 배치 CostText 0개, 시작 버튼 유지 확인. 임시 런타임 유닛으로 HP 500/520, SP 3/6 및 선택 해제 SP — 확인. 전체 전투 승패 검증은 아님.
- 기존 오류: KiaraActive1/2/3 누락 AbilityDefinition 타입 3건. BattleScene의 기존 카메라 SendMessage 및 물 셰이더 Enum drawer 오류도 별도 잔존. 신규 UI 예외는 관찰되지 않음.
- Play Mode 종료 완료.

## 확인용 캡처

- Captures/UICommand/formation-cost-final.png: 최종 편성 비용 표시.
- Captures/UICommand/deployment-no-cost.png: 배치 비용 표시 제거.
- Captures/UICommand/synchro-empty.png, synchro-equipped-preview.png: 칙령 보드. 후자는 저장 없는 표시 프리뷰.
- 그 외 중간 캡처는 지연된 프레임/테스트 상태일 수 있어 최종 결과로 사용하지 않음.
