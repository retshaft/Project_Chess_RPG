# 병렬 작업 보드

메인 개발자만 갱신한다. 기준일: 2026-09-08.

| 작업 | 담당 | 상태 | 소유 파일 / 다음 단계 |
|---|---|---|---|
| 병렬 개발 기반 | Codex | Verified (운영 도구) | 격리 저장소 도구 검증 11개 통과. 게임 코드 변경 없음; Unity 컴파일·플레이 미실행 |
| zcode-bootstrap | ZCode / GLM-5.3-Flash | Draft | 연결 확인용. 기능 작업 미배정 |
| antigravity-bootstrap | Antigravity / Gemini | Draft | 연결 확인용. 기능 작업 미배정 |

## 첫 배정 전 확인

- 원본 프로젝트의 적 유닛 데이터 8개는 기존 미커밋 변경으로 보존한다. `Assets/Data/Units/Enemies/Enemy_BlackKnight.asset`, `Enemy_BlackPawn.asset` 및 `Assets/Resources/Units/Enemies/Enemy_Black{Bishop,King,Knight,Pawn,Queen,Rook}.asset`이다.
- 이 데이터는 bootstrap 워크트리에 포함되지 않는다. 관련 전투·밸런스 과제를 배정하기 전에 메인이 변경을 검토하여 기준에 포함할지 결정한다.
- 기능 과제는 `Docs/MILESTONES.md`의 현재 우선순위에서 선정한다. 이 보드의 대기 행은 구현 지시가 아니다.
