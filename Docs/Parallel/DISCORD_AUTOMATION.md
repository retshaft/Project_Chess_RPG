# Discord 기반 개발 자동화

이 시스템은 개인 Discord 서버를 원격 관제 화면으로 사용한다. 실제 파일 작업과 AI 호출은 이 PC의 로컬 오케스트레이터가 수행한다. 시작 상태는 항상 일시정지이며 Discord의 `/dev-start` 명령 전에는 외부 AI를 호출하지 않는다.

## 사용자 최초 설정

1. Discord Developer Portal에서 새 Application과 Bot을 만든다.
2. 설치 설정에서 `bot`과 `applications.commands` 범위를 선택해 개인 서버에 설치한다. 봇 권한은 채널 보기와 메시지 보내기만 허용한다.
3. 개인 서버에 `dev-status`, `dev-results`, `dev-attention` 텍스트 채널을 만든다.
4. Discord 개발자 모드를 켜고 서버 ID와 본인 사용자 ID를 복사한다.
5. PowerShell에서 아래 환경 변수를 사용자 범위로 저장한다. 값은 프로젝트 파일이나 채팅에 기록하지 않는다.

```powershell
[Environment]::SetEnvironmentVariable('DISCORD_BOT_TOKEN', 'Discord Bot Token', 'User')
[Environment]::SetEnvironmentVariable('DISCORD_GUILD_ID', '개인 서버 ID', 'User')
[Environment]::SetEnvironmentVariable('DISCORD_OWNER_ID', '본인 사용자 ID', 'User')
[Environment]::SetEnvironmentVariable('TYPESAFE_API_KEY', 'TypeSafe API Key', 'User')
```

6. 새 PowerShell을 열고 저장소 루트에서 실행한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Parallel/Start-Automation.ps1
```

첫 실행만 전용 Python 환경과 공식 TypeSafe SDK, Discord 라이브러리를 설치한다. 봇이 접속하면 `/dev-status`로 연결을 확인한 뒤 `/dev-start`로 시작한다. `/dev-pause`는 현재 외부 프로세스를 강제 종료하지 않고 다음 작업 배정부터 멈춘다.

일반 개발 요청은 `/dev-request description:<요청 내용>`으로 등록한다. 이 명령을 해당 범위의 가역적 저장소 작업에 대한 승인으로 간주하며, Codex 계획·Jev 배정·구현·검증·리뷰를 자동으로 진행한다. 합리적인 UI·시각·구현 선택은 기존 프로젝트와 보수적인 기본값으로 결정한다. 비밀키 누락, 외부 비용, 공개·배포, 대량 삭제처럼 자동으로 결정할 수 없는 경우만 `dev-attention`에 알린다. `/dev-detail`로 근거를 확인하고 `/dev-trace item_id:<작업 ID>`로 담당 모델·대상 파일·최근 실행 로그를 확인한다. 실패한 항목은 `/dev-retry`, 불필요한 요청은 `/dev-reject`로 처리한다.

작업이 배정되면 담당 AI의 실행 로그 창을 자동으로 연다. 창 제목에 `codex`, `antigravity`, `zcode`와 작업 ID가 표시되며, 작업이 끝난 뒤에도 결과를 확인할 수 있다. 창을 열지 않으려면 `Tools/Parallel/automation_config.json`의 `show_agent_windows`를 `false`로 바꾼다.

## 처리 흐름

`Requested → Planning → PlanReview/Ready → Planned → Routing → Running → Review → IntegratedCompilePending/InGameCheckPending/Done/Attention` 순서로 진행한다. 저위험 작업은 파일 범위와 diff 검사를 통과하면 자동 커밋하고 복구 브랜치를 만든 뒤 `codex/integration`에 순차 반영한다. `IntegratedCompilePending`은 통합된 프로젝트의 Unity 컴파일을 감시하는 상태이고, `InGameCheckPending`은 구현·리뷰·백업·통합이 끝나 인게임 확인만 남은 보관 상태다. 두 상태 모두 실행 큐와 재시도 큐를 사용하지 않는다. 원격 push와 main 병합은 수행하지 않는다.

## 작업 큐 형식

오케스트레이터가 읽는 로컬 큐는 Git에서 제외된 `.parallel/queue/*.json`이다. 기존 `.parallel/tasks` 작업서 및 전용 워크트리가 준비된 뒤 큐 항목을 만든다.

```json
{
  "id": "m6-example",
  "status": "Ready",
  "priority": 10,
  "goal": "작업 목적",
  "worktree": "C:/절대경로/.worktrees/zcode-m6-example",
  "task_file": "C:/절대경로/.parallel/tasks/m6-example.json",
  "allowed_files": ["Assets/Scripts/Example.cs"],
  "eligible_agents": ["zcode"],
  "attempts": 0
}
```

기존 작업 JSON의 `agent`와 `eligible_agents`는 일치해야 한다. 현재 연결기는 작업별 워크트리와 담당 AI를 미리 고정하기 때문이다. 작업 생성·워크트리 생성·통합과 Unity 최종 검증은 기존 `WORKFLOW.md` 규칙을 그대로 따른다.

## 저장 위치와 안전장치

- 실행 상태: `.parallel/orchestrator-state.json`
- 작업 큐: `.parallel/queue/`
- Jev 판단: `.parallel/decisions/`
- Discord 전송 이벤트: `.parallel/events/`
- 외부 AI 결과: `.parallel/runs/`

봇은 지정한 서버의 지정한 사용자 명령만 받으며 Message Content Intent를 사용하지 않는다. 판단 신뢰도가 설정값보다 낮거나 Jev가 `wait` 또는 `hold`를 선택하면 실행하지 않고 `dev-attention`에 알린다. 외부 AI 실행 뒤 허용 파일과 Git diff를 검사하고, Unity가 꺼져 있으면 별도 실행권을 얻어 배치 컴파일과 Edit Mode 테스트를 수행한다. 검증과 Codex 리뷰를 통과한 결과도 자동으로 main에 병합하거나 원격으로 push하지 않는다.
