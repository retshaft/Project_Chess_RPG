"""Read-only Codex review for one isolated worker result."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

SCHEMA = Path(__file__).with_name("review_schema.json")


def review_task(task, timeout=900):
    codex = shutil.which("codex.cmd") or shutil.which("codex")
    if not codex:
        raise RuntimeError("Codex CLI is not installed.")
    worktree = Path(task["worktree"]).resolve()
    prompt = f"""Review the uncommitted changes for this delegated Checkmate RPG task.
Read AGENTS.md and the relevant code. Inspect the actual git diff and untracked files.
Report only actionable correctness, regression, scope, serialization, prediction/replay consistency,
or missing-verification problems. Do not edit files. Do not reject merely for style.

Goal: {task['goal']}
Allowed files: {json.dumps(task['allowed_files'], ensure_ascii=False)}
Expected behavior: {task.get('expected_behavior', '')}
Required verification: {json.dumps(task.get('verification', []), ensure_ascii=False)}
"""
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as temporary:
        output = Path(temporary.name)
    try:
        cmd = [codex, "exec", "--cd", str(worktree), "--sandbox", "read-only",
               "--ephemeral", "--color", "never",
               "--output-schema", str(SCHEMA), "--output-last-message", str(output), prompt]
        result = subprocess.run(cmd, cwd=worktree, capture_output=True, text=True, timeout=timeout)
        if result.returncode:
            raise RuntimeError((result.stderr or result.stdout)[-3000:])
        return json.loads(output.read_text(encoding="utf-8-sig"))
    finally:
        output.unlink(missing_ok=True)
