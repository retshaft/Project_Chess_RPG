"""Use Codex in read-only mode to turn one request into bounded task contracts."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

import pipeline_store

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = Path(__file__).with_name("planner_schema.json")


def normalize_plan(plan):
    for task in plan["tasks"]:
        task["eligible_agents"] = list(dict.fromkeys(task["eligible_agents"]))
        if not task["eligible_agents"]:
            raise ValueError("A planned task must have at least one eligible agent.")
    return plan


def plan_request(request, timeout=900):
    codex = shutil.which("codex.cmd") or shutil.which("codex")
    if not codex:
        raise RuntimeError("Codex CLI is not installed.")
    prompt = f"""You are the lead planner for the Checkmate RPG Unity repository.
Read AGENTS.md, Docs/MILESTONES.md, Docs/DEV_PROCESS.md, and relevant implementation files.
Do not edit any file. Convert the request below into the smallest independent implementation tasks.
Use exact repository-relative file paths; do not use globs or directories. Include tests only when
they materially verify behavior. Mark game-rule choices and visual preferences as user decisions.
Avoid assigning scene, prefab, package, shared interface, or integration work to a cheap worker
unless the scope is exceptionally clear. Return only the requested structured result.

User request:
{request['description']}
"""
    pipeline_store.PLANS.mkdir(parents=True, exist_ok=True)
    output = pipeline_store.PLANS / f"{request['id']}.json"
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as temporary:
        temporary_path = Path(temporary.name)
    try:
        cmd = [codex, "exec", "--cd", str(ROOT), "--sandbox", "read-only",
               "--ephemeral", "--color", "never",
               "--output-schema", str(SCHEMA), "--output-last-message", str(temporary_path), prompt]
        result = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, timeout=timeout)
        if result.returncode:
            raise RuntimeError((result.stderr or result.stdout)[-3000:])
        plan = normalize_plan(json.loads(temporary_path.read_text(encoding="utf-8-sig")))
        pipeline_store.write_json(output, plan)
        return plan, output
    finally:
        temporary_path.unlink(missing_ok=True)


def needs_review(plan):
    protected_fragments = (".unity", ".prefab", "Packages/", "ProjectSettings/", "IBattleContext")
    for task in plan["tasks"]:
        if task["risk"] == "high" or task["needs_user_decision"]:
            return True
        if any(fragment in path for path in task["allowed_files"] for fragment in protected_fragments):
            return True
    return False
