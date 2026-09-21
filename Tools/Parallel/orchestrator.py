"""Durable queue and bounded dispatch loop for Checkmate RPG development."""
import argparse
import datetime as dt
import json
import os
from pathlib import Path
import subprocess
import sys
import threading
import time
import uuid

from jev_router import choose_route, serialize_decision
import codex_planner
import codex_reviewer
import pipeline_store

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / ".parallel"
QUEUE = LOCAL / "queue"
DECISIONS = LOCAL / "decisions"
EVENTS = LOCAL / "events"
STATE_PATH = LOCAL / "orchestrator-state.json"
MODEL_HEALTH_PATH = LOCAL / "model-health.json"
CONFIG_PATH = Path(__file__).with_name("automation_config.json")
DISPATCH = Path(__file__).with_name("dispatch.py")
LOCK = threading.Lock()
TEAM = Path(__file__).with_name("Team.ps1")


def utc_now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def config():
    return read_json(CONFIG_PATH)


def model_health():
    if not MODEL_HEALTH_PATH.exists():
        return {}
    try:
        return read_json(MODEL_HEALTH_PATH)
    except (OSError, ValueError):
        return {}


def mark_model_unavailable(model, reason, cooldown_hours=6):
    if not model:
        return
    health = model_health()
    health[model] = {
        "available": False,
        "reason": reason[:300],
        "disabled_until": (dt.datetime.now(dt.timezone.utc) + dt.timedelta(hours=cooldown_hours)).isoformat(),
        "updated_utc": utc_now(),
    }
    write_json(MODEL_HEALTH_PATH, health)


def state():
    if not STATE_PATH.exists():
        write_json(STATE_PATH, {"paused": True, "running_task": None, "updated_utc": utc_now()})
    return read_json(STATE_PATH)


def update_state(**changes):
    with LOCK:
        current = state()
        current.update(changes, updated_utc=utc_now())
        write_json(STATE_PATH, current)
        return current


def emit(kind, title, body, data=None):
    event = {"id": uuid.uuid4().hex, "kind": kind, "title": title, "body": body,
             "created_utc": utc_now(), "data": data or {}}
    write_json(EVENTS / (event["id"] + ".json"), event)
    return event


def queue_tasks(status=None):
    tasks = []
    QUEUE.mkdir(parents=True, exist_ok=True)
    for path in QUEUE.glob("*.json"):
        task = read_json(path)
        task["_path"] = str(path)
        if status is None or task.get("status") == status:
            tasks.append(task)
    return sorted(tasks, key=lambda item: (item.get("priority", 100), item["id"]))


def ready_tasks():
    return queue_tasks("Ready")


def materialize_plan(request, plan):
    created = []
    for index, planned in enumerate(plan["tasks"]):
        unique_id = f"{request['id'][-8:]}-{planned['id']}"[:48].rstrip("-")
        path = QUEUE / f"{unique_id}.json"
        task = {
            **planned,
            "id": unique_id,
            "request_id": request["id"],
            "status": "Planned",
            "priority": index + 1,
            "attempts": 0,
            "created_utc": utc_now(),
        }
        write_json(path, task)
        created.append(task)
    return created


def process_request_once():
    requested = pipeline_store.list_requests("Requested")
    if not requested:
        return None
    request = pipeline_store.transition(requested[0], "Planning")
    emit("status", "요청 계획 시작", request["description"])
    try:
        plan = plan_path = None
        planner_error = None
        planner_models = ("gpt-5.6-luna", "gpt-5.6-sol", "gpt-6-astra")
        for planner_model in planner_models:
            health = model_health().get(planner_model, {})
            if health.get("available") is False:
                continue
            try:
                plan, plan_path = codex_planner.plan_request(request, model=planner_model)
                break
            except Exception as exc:
                planner_error = exc
                text = str(exc).lower()
                if any(marker in text for marker in ("usage limit", "rate limit", "quota", "credits", "too many requests")):
                    mark_model_unavailable(planner_model, "계획 작성 중 사용량 또는 요청 한도 초과")
                    continue
                raise
        if plan is None or plan_path is None:
            raise RuntimeError(str(planner_error or "사용 가능한 계획 작성 모델이 없습니다."))
        settings = config()
        autonomy_mode = settings.get("autonomy_mode", "guarded")
        review = codex_planner.needs_review(plan, autonomy_mode=autonomy_mode)
        # In full autonomy, ordinary implementation assumptions are accepted automatically.
        # Only plan generation failure or an irreversible external action may stop the queue.
        next_status = "Ready" if autonomy_mode == "full" or not review else "PlanReview"
        fields = {"plan_path": str(plan_path), "plan_summary": plan["summary"]}
        pipeline_store.transition(request, next_status, **fields)
        if next_status == "Ready":
            materialize_plan(request, plan)
        title = ("작업 계획 완료" if next_status == "Ready" else
                 "사용자 정보 필요" if next_status == "Attention" else "계획 검토 필요")
        emit("attention" if next_status != "Ready" else "result", title,
             f"{request['id']}: {plan['summary']}",
             {"item_id": request["id"], "status": next_status})
        return next_status
    except Exception as exc:
        pipeline_store.transition(request, "Attention", error=str(exc))
        emit("attention", "요청 계획 실패", f"{request['id']}: {exc}")
        return "Attention"


def repository_state():
    result = subprocess.run(["git", "status", "--short"], cwd=ROOT, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(result.stderr.strip())
    changed = [line[3:] for line in result.stdout.splitlines() if len(line) > 3]
    ignored = set(config().get("ignorable_root_changes", []))
    meaningful = [path for path in changed if path not in ignored]
    return {"root_dirty": bool(meaningful), "changed_files": meaningful[:100], "ignored_changes": [path for path in changed if path in ignored], "model_health": model_health()}


def prepare_task(task, agent, selected_model=None):
    repo = repository_state()
    if repo["root_dirty"]:
        raise RuntimeError(
            "원본 작업 트리에 미커밋 변경이 있어 안전한 기준 커밋을 만들 수 없습니다. "
            "기존 변경을 검토·보존한 뒤 다시 시도하세요."
        )
    if agent not in task.get("eligible_agents", []):
        raise ValueError("Selected agent is not allowed by the task contract.")
    result = subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(TEAM),
         "-Action", "New", "-Agent", agent, "-Task", task["id"], "-Base", "HEAD"],
        cwd=ROOT, capture_output=True, text=True,
    )
    if result.returncode:
        raise RuntimeError((result.stderr or result.stdout).strip())
    worktree = ROOT / ".worktrees" / f"{agent}-{task['id']}"
    base = subprocess.run(["git", "-C", str(worktree), "rev-parse", "HEAD"],
                          capture_output=True, text=True, check=True).stdout.strip()
    task_file = LOCAL / "tasks" / f"{task['id']}.json"
    prompt = (
        f"Goal: {task['goal']}\n"
        f"Current behavior: {task['current_behavior']}\n"
        f"Expected behavior: {task['expected_behavior']}\n"
        "Verification:\n- " + "\n- ".join(task["verification"])
    )
    write_json(task_file, {
        "status": "Ready", "agent": agent, "base_commit": base, "mode": "edit",
        "allowed_files": task["allowed_files"], "goal": task["goal"], "prompt": prompt,
        "model": selected_model,
    })
    task.update(status="Ready", agent=agent, worktree=str(worktree), task_file=str(task_file),
                base_commit=base, prepared_utc=utc_now())
    if selected_model:
        task["model"] = selected_model
    write_json(task["_path"], {key: value for key, value in task.items() if key != "_path"})
    return task


def route_planned_task():
    tasks = queue_tasks("Planned")
    if not tasks:
        return None
    settings = config()
    decision = choose_route(tasks, repository_state(), settings["jev_model"])
    record_decision(decision)
    autonomy_mode = settings.get("autonomy_mode", "guarded")
    selected = next((item for item in tasks if item["id"] == decision.task_id), None)
    health = model_health()
    chosen_model = decision.model or ""
    unavailable = health.get(chosen_model, {})
    try:
        blocked = unavailable.get("available") is False and dt.datetime.fromisoformat(
            unavailable.get("disabled_until", "")
        ) > dt.datetime.now(dt.timezone.utc)
    except (TypeError, ValueError):
        blocked = False
    if blocked:
        chosen_model = ""
        for candidate in settings.get("model_policy_order", [
            "gpt-5.6-luna", "gemini-3.1-pro-high", "gemini-3.8-flash", "GLM-5.3-Flash", "gpt-5.6-sol", "gpt-6-astra"
        ]):
            candidate_profile = settings.get("models", {}).get(candidate, {})
            if candidate_profile.get("agent") in (selected or {}).get("eligible_agents", []) and not model_health().get(candidate, {}).get("available") is False:
                chosen_model = candidate
                break
    profile = settings.get("models", {}).get(chosen_model, {}) if chosen_model else {}
    chosen_agent = profile.get("agent") or decision.agent
    invalid_choice = selected is None or chosen_agent not in selected.get("eligible_agents", [])
    needs_fallback = (
        decision.task_id == "wait" or decision.agent == "hold"
        or decision.confidence < float(settings["minimum_confidence"])
        or invalid_choice
    )
    if needs_fallback and autonomy_mode == "full":
        task = selected or tasks[0]
        preferred = profile.get("agent")
        if preferred in task.get("eligible_agents", []):
            agent = preferred
        else:
            task, agent = fallback_route(tasks)
        emit("status", "Jev 판단 자동 승격",
             f"신뢰도 {decision.confidence:.2f}; {task['id']} → {agent}", serialize_decision(decision))
    elif decision.task_id == "wait" or decision.agent == "hold":
        emit("attention", "자동 실행 보류", "Jev가 계획된 작업을 보류했습니다.", serialize_decision(decision))
        return "held"
    elif decision.confidence < float(settings["minimum_confidence"]):
        emit("attention", "낮은 판단 신뢰도",
             f"{decision.task_id}: {decision.confidence:.2f}", serialize_decision(decision))
        return "low-confidence"
    elif invalid_choice:
        return "rejected"
    else:
        task, agent = selected, chosen_agent
    try:
        prepare_task(task, agent, chosen_model or None)
        emit("status", "작업 공간 준비 완료", f"{task['id']} → {agent}")
        return "prepared"
    except Exception as exc:
        task["status"] = "Attention"
        task["last_failure"] = str(exc)
        write_json(task["_path"], {key: value for key, value in task.items() if key != "_path"})
        emit("attention", "작업 준비 실패", f"{task['id']}: {exc}")
        return "attention"


def fallback_route(tasks):
    task = tasks[0]
    eligible = task.get("eligible_agents", [])
    for agent in ("codex", "antigravity", "zcode"):
        if agent in eligible:
            return task, agent
    raise ValueError(f"No eligible fallback agent for {task['id']}")


def requeue_after_provider_failure(task, result):
    """Move a provider-limited Codex task back to routing for an alternate worker."""
    settings = config()
    if not settings.get("allow_emergency_fallback"):
        return False
    fallback = settings.get("emergency_fallback_agent")
    if not fallback or fallback not in ("antigravity", "zcode"):
        return False
    output = ((result.stdout or "") + "\n" + (result.stderr or "")).lower()
    markers = ("usage limit", "rate limit", "quota", "credits", "too many requests")
    if not any(marker in output for marker in markers):
        return False
    model = task.get("model") or config().get("models", {}).get(task.get("agent"), {}).get("model") or task.get("agent")
    mark_model_unavailable(model, "사용량 또는 요청 한도 초과")
    eligible = list(task.get("eligible_agents", []))
    if fallback not in eligible:
        eligible.append(fallback)
    task["eligible_agents"] = eligible
    task["status"] = "Planned"
    task["last_failure"] = "Codex 사용량 한도 도달; " + fallback + "로 자동 전환"
    for key in ("agent", "worktree", "task_file", "base_commit", "prepared_utc", "started_utc"):
        task.pop(key, None)
    write_json(task["_path"], {key: value for key, value in task.items() if key != "_path"})
    emit("status", "대체 에이전트로 전환", f"{task['id']} → {fallback}", {"item_id": task["id"], "status": "Planned"})
    return True


def validate_task(task):
    required = ("id", "goal", "worktree", "task_file", "allowed_files")
    missing = [key for key in required if not task.get(key)]
    if missing:
        raise ValueError("Missing task fields: " + ", ".join(missing))
    worktree = Path(task["worktree"]).resolve()
    task_file = Path(task["task_file"]).resolve()
    worktrees_root = (ROOT / ".worktrees").resolve()
    tasks_root = (ROOT / ".parallel" / "tasks").resolve()
    if not worktree.is_relative_to(worktrees_root):
        raise ValueError("worktree must be under .worktrees")
    if not task_file.is_relative_to(tasks_root):
        raise ValueError("task_file must be under .parallel/tasks")
    assigned = read_json(task_file).get("agent")
    if assigned and task.get("agent") and assigned != task["agent"]:
        raise ValueError("Jev route does not match the assigned task file agent")
    return worktree, task_file


def record_decision(decision):
    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ-")
    write_json(DECISIONS / (stamp + uuid.uuid4().hex[:8] + ".json"), serialize_decision(decision))


def dispatch(task, agent, timeout):
    worktree, task_file = validate_task(task)
    cmd = [sys.executable, str(DISPATCH), "run", "--agent", agent,
           "--worktree", str(worktree), "--task", str(task_file), "--timeout", str(timeout)]
    return subprocess.run(
        cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace",
        timeout=timeout + 60,
    )


def unity_is_running():
    probe = subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         "@(Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\").Count"],
        capture_output=True, text=True,
    )
    try:
        return int(probe.stdout.strip() or "0") > 0
    except ValueError:
        return True


def run_unity_checks(task):
    if unity_is_running():
        return {"passed": False, "deferred": True,
                "reason": "Unity editor is already running; automatic batch verification deferred."}
    settings = config()
    unity = Path(settings["unity_path"])
    if not unity.is_file():
        return {"passed": False, "reason": f"Unity executable not found: {unity}"}
    worktree = Path(task["worktree"])
    verification_dir = LOCAL / "verification" / task["id"]
    verification_dir.mkdir(parents=True, exist_ok=True)
    acquire = subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(TEAM),
         "-Action", "AcquireUnity"], cwd=worktree, capture_output=True, text=True,
    )
    if acquire.returncode:
        return {"passed": False, "reason": (acquire.stderr or acquire.stdout).strip()}
    lease = json.loads(acquire.stdout)
    token = lease["token"]
    try:
        compile_log = verification_dir / "compile.log"
        compile_result = subprocess.run(
            [str(unity), "-quit", "-batchmode", "-nographics", "-projectPath", str(worktree),
             "-logFile", str(compile_log)],
            capture_output=True, text=True, timeout=int(settings["unity_test_timeout_seconds"]),
        )
        compile_text = compile_log.read_text(encoding="utf-8", errors="replace") if compile_log.exists() else ""
        compile_ok = compile_result.returncode == 0 and ": error CS" not in compile_text and "Aborting batchmode" not in compile_text
        if not compile_ok:
            return {"passed": False, "reason": "Unity batch compile failed", "compile_log": str(compile_log)}

        result_xml = verification_dir / "editmode-results.xml"
        test_log = verification_dir / "editmode.log"
        test_result = subprocess.run(
            [str(unity), "-quit", "-batchmode", "-nographics", "-projectPath", str(worktree),
             "-runTests", "-testPlatform", "EditMode", "-testResults", str(result_xml),
             "-logFile", str(test_log)],
            capture_output=True, text=True, timeout=int(settings["unity_test_timeout_seconds"]),
        )
        return {"passed": test_result.returncode == 0 and result_xml.is_file(),
                "compile_log": str(compile_log), "test_results": str(result_xml),
                "test_log": str(test_log), "test_exit_code": test_result.returncode}
    except subprocess.TimeoutExpired:
        return {"passed": False, "reason": "Unity verification timed out."}
    finally:
        subprocess.run(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(TEAM),
             "-Action", "ReleaseUnity", "-Token", token],
            cwd=worktree, capture_output=True, text=True,
        )


def verify_worker_result(task):
    worktree = Path(task["worktree"])
    diff = subprocess.run(["git", "-C", str(worktree), "diff", "--check", task["base_commit"]],
                          capture_output=True, text=True)
    if diff.returncode:
        return {"passed": False, "checks": {"diff_check": diff.stdout[-3000:]}}
    changed = subprocess.run(
        ["git", "-C", str(worktree), "diff", "--name-only", "--no-renames", task["base_commit"]],
        capture_output=True, text=True, check=True,
    ).stdout.splitlines()
    untracked = subprocess.run(
        ["git", "-C", str(worktree), "ls-files", "--others", "--exclude-standard"],
        capture_output=True, text=True, check=True,
    ).stdout.splitlines()
    changed = sorted(set(changed + untracked))
    violations = sorted(set(changed) - set(task["allowed_files"]))
    unity_required = any(path.endswith((".cs", ".unity", ".prefab", ".asset", ".asmdef")) for path in changed)
    checks = {"diff_check": "passed", "changed_files": changed, "scope_violations": violations,
              "unity_verification_required": unity_required,
              "manual_play_mode_required": any(path.endswith((".unity", ".prefab")) for path in changed)}
    if changed and not violations and unity_required:
        checks["unity"] = run_unity_checks(task)
    return {
        "passed": bool(changed) and not violations and (not unity_required or checks["unity"]["passed"]),
        "checks": checks,
    }


def verify_and_review_task(task):
    verification = verify_worker_result(task)
    task["verification_result"] = verification
    unity = verification.get("checks", {}).get("unity", {})
    if unity.get("deferred"):
        task["status"] = "VerificationPending"
        task["verification_pending_reason"] = unity.get("reason")
    elif not verification["passed"]:
        task["status"] = "Attention"
    else:
        task.pop("verification_pending_reason", None)
        try:
            review = codex_reviewer.review_task(task)
            task["review_result"] = review
            if review["verdict"] == "pass" and not verification["checks"]["manual_play_mode_required"]:
                task["status"] = "IntegrationReady"
            elif review["verdict"] == "pass":
                task["status"] = "VerificationPending"
                task["verification_pending_reason"] = "Play Mode interaction verification is required."
            else:
                task["status"] = "Attention"
        except Exception as exc:
            task["status"] = "Attention"
            task["review_error"] = str(exc)
    write_json(task["_path"], {k: v for k, v in task.items() if k != "_path"})
    kind = "result" if task["status"] == "IntegrationReady" else (
        "status" if task["status"] == "VerificationPending" else "attention"
    )
    emit(kind, "작업 검토 완료" if kind == "result" else "작업 검증 대기" if kind == "status" else "작업 확인 필요",
         f"{task['id']} 상태: {task['status']}\n변경 파일: "
         + ", ".join(verification.get("checks", {}).get("changed_files", [])[:8])
         + ("\n다음: Unity 검증 또는 통합 대기" if task["status"] == "VerificationPending" else "\n다음: 결과 확인"),
         {"item_id": task["id"], "status": task["status"]})
    return task["status"].lower()


def deferred_verification_tasks():
    result = queue_tasks("Review")
    if unity_is_running():
        return result
    for task in queue_tasks("VerificationPending"):
        unity = task.get("verification_result", {}).get("checks", {}).get("unity", {})
        if unity.get("deferred"):
            result.append(task)
    return result


def run_once():
    current = state()
    if current.get("paused") or current.get("running_task"):
        return "paused" if current.get("paused") else "busy"
    pending = deferred_verification_tasks()
    if pending:
        return verify_and_review_task(pending[0])
    planned = process_request_once()
    if planned:
        return planned.lower()
    routed = route_planned_task()
    if routed and routed != "prepared":
        return routed
    tasks = ready_tasks()
    if not tasks:
        return routed or "idle"

    settings = config()
    task = tasks[0]
    selected_agent = task["agent"]
    if int(task.get("attempts", 0)) >= int(settings["maximum_attempts"]):
        if task.get("status") != "Attention" or task.get("last_failure") != "자동 재시도 한도 도달":
            task["status"] = "Attention"
            task["last_failure"] = "자동 재시도 한도 도달"
            write_json(task["_path"], {k: v for k, v in task.items() if k != "_path"})
            emit("attention", "재시도 한도 도달",
                 f"{task['goal']}\n자동 재시도를 멈췄습니다. 필요하면 아래 버튼으로 새 시도를 시작하세요.",
                 {"item_id": task["id"], "status": "Attention"})
        return "attempt-limit"

    task["status"] = "Running"
    task["attempts"] = int(task.get("attempts", 0)) + 1
    task["started_utc"] = utc_now()
    write_json(task["_path"], {k: v for k, v in task.items() if k != "_path"})
    update_state(running_task=task["id"])
    emit("status", "작업 시작",
         f"{task['id']} → {selected_agent} / {task.get('model', '기본 모델')}\n"
         f"목표: {task.get('goal', '')}\n대상: {', '.join(task.get('allowed_files', [])[:8])}",
         {"item_id": task["id"], "status": "Running"})
    try:
        result = dispatch(task, selected_agent, int(task.get("timeout_seconds", settings["default_timeout_seconds"])))
        if result.returncode != 0 and requeue_after_provider_failure(task, result):
            return "rerouted"
        task["status"] = "Review" if result.returncode == 0 else "Attention"
        task["finished_utc"] = utc_now()
        task["last_exit_code"] = result.returncode
        task["last_output"] = result.stdout[-4000:]
        if result.stderr:
            task["last_failure"] = result.stderr[-2000:]
        if result.returncode == 0:
            return verify_and_review_task(task)
        if int(task.get("attempts", 0)) < int(settings["maximum_attempts"]):
            task["status"] = "Ready"
            task["last_failure"] = "자동 재시도 예정: " + (result.stderr or "외부 에이전트가 작업을 완료하지 못함")[-1000:]
            write_json(task["_path"], {k: v for k, v in task.items() if k != "_path"})
            emit("status", "작업 자동 재시도", f"{task['id']}는 확인 없이 다시 시도합니다.", {"item_id": task["id"], "status": "Ready"})
            return "retrying"
        write_json(task["_path"], {k: v for k, v in task.items() if k != "_path"})
        emit("attention", "작업 실행 실패", f"{task['id']} 상태: {task['status']}")
        return task["status"].lower()
    finally:
        update_state(running_task=None)


def serve(stop_event=None):
    stop_event = stop_event or threading.Event()
    while not stop_event.is_set():
        try:
            result = run_once()
            update_state(last_cycle_utc=utc_now(), last_cycle_result=result, last_cycle_error=None)
        except Exception as exc:
            update_state(last_cycle_utc=utc_now(), last_cycle_result="error", last_cycle_error=str(exc)[:1000])
            emit("attention", "자동화 오류", str(exc))
        stop_event.wait(float(config()["poll_seconds"]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("status", "start", "pause", "once", "serve"))
    args = parser.parse_args()
    if args.action == "status":
        print(json.dumps({"state": state(), "ready": len(ready_tasks())}, ensure_ascii=False, indent=2))
    elif args.action == "start":
        print(json.dumps(update_state(paused=False), ensure_ascii=False))
    elif args.action == "pause":
        print(json.dumps(update_state(paused=True), ensure_ascii=False))
    elif args.action == "once":
        print(run_once())
    else:
        serve()


if __name__ == "__main__":
    main()
