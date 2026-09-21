# 외부 AI 자동 호출

> 2026-09-09 실행 확인: 설치본이 `--max-turns`를 거부한다. 현재 연결기는 `--target`/`--max-turns`를 전달하지 않고 목표를 프롬프트에 포함한 제한 시간 내 단일 작업 호출을 사용한다. 아래의 Goal/턴 옵션 설명은 이전 설계 기록이며 실제 Goal 모드 검증을 의미하지 않는다. CLI의 `/goal` 활성화는 보장하지 않는다.

## 연결 구성

Codex는 `Tools/Parallel/dispatch.py`로 ZCode와 Antigravity를 직접 실행하고 결과를 수집한다. 별도 상주 서버 없이 요청할 때만 프로세스를 띄운다. 에이전트당 한 작업, 워크트리당 한 작업만 허용하므로 두 AI를 병렬 실행할 수 있다.

| 담당 | 호출 경로 | 고정 모델 |
|---|---|---|
| ZCode | 설치본에 포함된 `resources/glm/zcode.cjs`와 Node.js | GLM-5.3-Flash |
| Antigravity | 공식 `agy.exe` CLI | Gemini 3.1 Pro (High), 사용자 선택 |

설정은 `Tools/Parallel/agents.json`에 있다. 로그인 정보는 프로젝트에 저장하지 않는다. ZCode는 공식 CLI 로그인으로 생성된 사용자 설정의 인증을 자식 프로세스에만 전달한다. Antigravity는 자체 인증 저장소를 사용한다. 다른 모델로 자동 대체하지 않는다.

ZCode 0.16.5 설치본은 도움말에 나온 `--settings`, `--max-turns`를 실제로 거부하므로 사용하지 않는다. 지원되는 `ZCODE_MODEL`로 모델을 지정하고 실행 시간 제한은 연결 도구가 관리한다. 앱 업데이트 후 연결 테스트를 다시 수행한다.

## 메인이 사용하는 명령

원본 프로젝트 루트에서 실행한다. Python 3.9 이상과 Git이 필요하며 이 PC에는 Python 3.13 및 Node.js가 설치되어 있다.

```powershell
python Tools/Parallel/dispatch.py doctor
python Tools/Parallel/dispatch.py status

python Tools/Parallel/dispatch.py run --agent zcode --worktree .worktrees/zcode-bootstrap --smoke --timeout 90
python Tools/Parallel/dispatch.py run --agent antigravity --worktree .worktrees/antigravity-bootstrap --smoke --timeout 90

python Tools/Parallel/dispatch.py run --agent zcode --worktree .worktrees/zcode-작업ID --task .parallel/tasks/작업ID.json --timeout 600
# 목표 완료까지 여러 턴 유지 (기본 24턴)
python Tools/Parallel/dispatch.py run --agent zcode --worktree .worktrees/zcode-작업ID --task .parallel/tasks/작업ID.json --timeout 1800 --max-turns 48
```

CLI는 사용자 디렉터리에 자체 세션·로그를 기록하고 모델 서버에 접속한다. Codex의 제한된 실행 환경에서 막히면 해당 실행에 필요한 권한으로 호출한다. 전역 권한 검사를 끄거나 `yolo`, `--dangerously-skip-permissions`를 사용하지 않는다.

각 호출은 완료될 때까지 실행되지만 터미널 도구의 실행 세션으로 계속 추적할 수 있다. 두 호출은 별도 실행 세션으로 시작한다. 응답 완료 후 메인이 결과를 읽는다. 이 도구 자체가 Codex를 예약 실행하거나 대화가 끝난 뒤 새 작업을 계속 배정하지는 않는다.

모델이 도구 요청을 시도하다가 `CANCELED`를 반환할 수 있다. 제공자 종료 코드가 0이어도 `status`가 `SUCCESS`가 아니면 실패로 기록한다. Antigravity의 `--mode plan`은 연결 확인에 쓰며, `edit` 작업은 `accept-edits`로 실행한다. 작업 지시문에 파일 하나만 허용해도 모델이 그 범위를 지키는지 종료 후 다시 확인한다.

## 자동 호출용 작업서

메인은 기존 Markdown 작업서를 먼저 작성하고 기준 커밋과 허용 파일을 아래 JSON에 일치시킨다. 운영 중 생성한 JSON은 `.parallel/tasks/`에 둔다. 기준 SHA는 워크트리에서 `git rev-parse HEAD`로 확인한다.

```json
{
  "status": "Ready",
  "agent": "zcode",
  "base_commit": "실제 전체 커밋 SHA",
  "mode": "edit",
  "allowed_files": [
    "Assets/Scripts/담당파일.cs"
  ],
  "goal": "작업을 검토·구현·자체 점검·인계까지 완료하고 실제 차단 사유가 있을 때만 중단하세요.",
  "prompt": "배정된 Markdown 작업서를 읽고 해당 버그만 수정하세요. 재현 방법, 기대 동작과 테스트 조건을 명시하세요."
}
```

- `agent`: `zcode` 또는 `antigravity`.
- `mode`: 분석만 수행하는 `plan` 또는 파일 편집을 허용하는 `edit`. `plan`의 허용 파일 목록은 비어 있어야 한다.
- `allowed_files`: 정확한 상대 파일 경로 목록. 새 Unity 파일은 필요한 `.meta`도 포함한다. 폴더·와일드카드·상위 경로는 허용하지 않는다.
- 자동 호출에서는 서브 AI가 Git 커밋이나 Unity 실행을 하지 않는다. 결과는 응답으로 인계하고, 메인이 diff를 확인해 인계서 작성·선택적 커밋·Unity 검증을 수행한다. 수동 앱 작업의 커밋 절차에 대한 예외다.
- 작업 시작 시 워크트리가 깨끗해야 한다. 이전 결과가 미커밋 상태이면 메인이 먼저 리뷰한다. 자동 초기화·stash는 하지 않는다.
- 허용 파일 검사는 종료 후 diff를 검사하는 방식이며 파일 시스템 보안 샌드박스는 아니다. 외부 AI의 기존 권한 확인을 유지한다. 다른 워크트리나 시스템 변경을 강제로 차단한다고 간주하지 않는다.

## 결과 읽기

결과는 Git에서 제외된 `.parallel/runs/<실행ID>/`에 저장한다.

- `task.json`: 실행한 과제 및 허용 파일.
- `stdout.json`, `stderr.log`: 원본 응답과 진단 로그. 업무 내용이 포함될 수 있으므로 커밋하지 않는다.
- `result.json`: 모델, 워크트리, 기준 SHA, 세션 ID, 응답, 변경 파일, 종료 코드 및 실행 시간.

상태 `ConnectionVerified`는 응답만 받는 연결 테스트 통과다. 일반 작업은 성공해도 `Review`이며 게임 기능 검증 통과를 뜻하지 않는다. `Failed`, `TimedOut`, `ScopeViolation`은 메인 확인이 필요하다. 프로세스 종료 코드가 0이어도 제공자가 오류 상태를 반환하면 실패로 처리한다. 도구 권한이 거부된 채 응답만 완료될 수도 있으므로 메인은 응답·진단 로그·실제 변경을 함께 검토한다.

기본 제한은 10분이며 15초~1시간 범위로 지정한다. 시간 초과 시 해당 자식 프로세스 트리를 종료하고 변경을 보존한다. 자동 재시도는 없다. 실행 잠금은 `.parallel/locks/`에 기록한다. 메인 프로세스가 강제 종료되어 잠금이 남았다면 기록된 PID와 실제 작업 종료를 확인한 뒤 해당 잠금만 정리한다. 시간만 보고 잠금을 제거하지 않는다.

ZCode 작업은 `--target`으로 세션 목표를 설정하고 `--max-turns`만큼 여러 턴을 허용한다. 작업 JSON에 `goal` 문자열을 넣으면 해당 목표를 사용하며, 없으면 연결기가 검토·구현·자체 점검·인계까지 완료하고 계획 단계에서 멈추지 말라는 기본 목표를 주입한다. 목표가 끝났거나 실제 차단 사유가 생긴 경우에만 종료한다.

목표 모드는 같은 작업을 무한 재시도하는 기능이 아니다. 시간 초과·권한 거부·범위 위반은 변경을 보존한 채 `TimedOut`, `Failed`, `ScopeViolation`으로 종료하고 메인이 검토한다.

## 설치 및 로그인

이 PC에는 Antigravity 공식 설치 스크립트로 CLI를 설치했다. 기존 PATH와 별칭은 변경하지 않았다. 설치 경로는 `%LOCALAPPDATA%/agy/bin/agy.exe`다.

ZCode 로그인이 만료되면 설치본의 공식 `login --no-browser` 명령을 실행하고 사용자가 링크에서 인증한다. 비밀번호나 토큰을 채팅에 요청하지 않는다. 로그인 명령은 사용자 CLI 설정을 갱신할 수 있으나 연결 도구는 각 요청에서 지정 모델을 사용한다. 사용 가능 모델이나 사용 한도가 바뀌면 오류를 기록하고 사용자에게 알린다.

근거: [Antigravity 공식 설치·인증 안내](https://antigravity.google/docs/cli/install/), [공식 자동 실행·JSON 응답 안내](https://antigravity.google/docs/cli/headless/). ZCode는 이 PC의 설치본 도움말과 실제 호출로 지원 옵션을 검증했다.

## 보조 도구 검증

```powershell
python -m unittest discover -s Tools/Parallel -p test_dispatch.py -v
```

모델을 호출하지 않고 작업서 검증, 파일 경로 제한, 중복 실행 차단, 응답 오류 처리, 프롬프트 인자 전달을 검사한다. 실제 연결 및 파일 작업 검증 기록은 `CONNECTION_STATUS.md`에 남긴다.
