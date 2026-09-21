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

일반 개발 요청은 `/dev-request description:<요청 내용>`으로 등록한다. Codex가 저장소를 읽어 구조화된 작업 계획을 만들며, 고위험 파일·게임 규칙·사용자 결정이 포함된 계획은 `/dev-approve request_id:<ID>` 전까지 실행하지 않는다. `/dev-detail`로 근거를 확인하고 실패한 항목은 `/dev-retry`, 불필요한 요청은 `/dev-reject`로 처리한다.

## 처리 흐름

`Requested → Planning → PlanReview/Ready → Planned → Routing → Running → Review → IntegrationReady/VerificationPending/Attention` 순서로 진행한다. Unity 씬이나 프리팹 변경은 자동 배치 검증을 통과해도 Play Mode 확인 전까지 `VerificationPending`에 남는다. 자동화는 main 병합과 원격 push를 수행하지 않는다.

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

봇은 지정한 서버의 지정한 사용자 명령만 받으며 Message Content Intent를 사용하지 않는다. 판단 신뢰도가 설정값보다 낮거나 Jev가 `wait` 또는 `hold`를 선택하면 실행하지 않고 `dev-attention`에 알린다. 외부 AI 실행 결과는 `Review`까지만 진행하며 자동 커밋, 병합, push, Unity 실행은 하지 않는다.
