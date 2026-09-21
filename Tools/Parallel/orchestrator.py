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
        plan, plan_path = codex_planner.plan_request(request)
        settings = config()
        autonomy_mode = settings.get("autonomy_mode", "guarded")
        review = codex_planner.needs_review(plan, autonomy_mode=autonomy_mode)
        next_status = "PlanReview" if review else "Ready"
        fields = {"plan_path": str(plan_path), "plan_summary": plan["summary"]}
        if review and autonomy_mode == "full":
            fields["error"] = "자동 판단으로 해결할 수 없는 정보가 필요합니다: " + " | ".join(codex_planner.user_questions(plan))
            next_status = "Attention"
        pipeline_store.transition(request, next_status, **fields)
        if next_status == "Ready":
            materialize_plan(request, plan)
        title = ("작업 계획 완료" if next_status == "Ready" else
                 "사용자 정보 필요" if next_status == "Attention" else "계획 검토 필요")
        emit("attention" if next_status != "Ready" else "result", title,
             f"{request['id']}: {plan['summary']}")
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
    return {"root_dirty": bool(changed), "changed_files": changed[:100]}


def prepare_task(task, agent):
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
    })
    task.update(status="Ready", agent=agent, worktree=str(worktree), task_file=str(task_file),
                base_commit=base, prepared_utc=utc_now())
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
    invalid_choice = selected is None or decision.agent not in selected.get("eligible_agents", [])
    needs_fallback = (
        decision.task_id == "wait" or decision.agent == "hold"
        or decision.confidence < float(settings["minimum_confidence"])
        or invalid_choice
    )
    if needs_fallback and autonomy_mode == "full":
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
        task, agent = selected, decision.agent
    try:
        prepare_task(task, agent)
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
         f"{task['id']} 상태: {task['status']}")
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
        emit("attention", "재시도 한도 도달", task["id"])
        return "attempt-limit"

    task["status"] = "Running"
    task["attempts"] = int(task.get("attempts", 0)) + 1
    task["started_utc"] = utc_now()
    write_json(task["_path"], {k: v for k, v in task.items() if k != "_path"})
    update_state(running_task=task["id"])
    emit("status", "작업 시작", f"{task['id']} → {selected_agent}")
    try:
        result = dispatch(task, selected_agent, int(task.get("timeout_seconds", settings["default_timeout_seconds"])))
        task["status"] = "Review" if result.returncode == 0 else "Attention"
        task["finished_utc"] = utc_now()
        task["last_exit_code"] = result.returncode
        task["last_output"] = result.stdout[-4000:]
        if result.stderr:
            task["last_failure"] = result.stderr[-2000:]
        if result.returncode == 0:
            return verify_and_review_task(task)
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
