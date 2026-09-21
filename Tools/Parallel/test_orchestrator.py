import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import orchestrator
from jev_router import _choice


class OrchestratorTests(unittest.TestCase):
    def test_state_starts_paused(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(orchestrator, "STATE_PATH", Path(folder) / "state.json"):
            self.assertTrue(orchestrator.state()["paused"])

    def test_only_ready_tasks_are_returned_in_priority_order(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(orchestrator, "QUEUE", Path(folder)):
            Path(folder, "later.json").write_text(json.dumps({"id": "later", "goal": "b", "status": "Ready", "priority": 20}), encoding="utf-8")
            Path(folder, "first.json").write_text(json.dumps({"id": "first", "goal": "a", "status": "Ready", "priority": 10}), encoding="utf-8")
            Path(folder, "done.json").write_text(json.dumps({"id": "done", "goal": "c", "status": "Review"}), encoding="utf-8")
            self.assertEqual([task["id"] for task in orchestrator.ready_tasks()], ["first", "later"])

    def test_task_paths_are_confined_to_coordination_folders(self):
        task = {"id": "x", "goal": "x", "allowed_files": ["x"],
                "worktree": str(orchestrator.ROOT), "task_file": str(orchestrator.ROOT / "task.json")}
        with self.assertRaises(ValueError):
            orchestrator.validate_task(task)

    def test_paused_loop_does_not_call_jev(self):
        with patch.object(orchestrator, "state", return_value={"paused": True, "running_task": None}), \
             patch.object(orchestrator, "choose_route") as choose:
            self.assertEqual(orchestrator.run_once(), "paused")
            choose.assert_not_called()

    def test_official_sdk_answer_shape_is_read(self):
        class Answer:
            def model_dump(self, mode="json"):
                return {"choice": "zcode", "confidence": 0.91, "probabilities": {"zcode": 0.91}}
        class Response:
            answers = {"agent": Answer()}
        choice, confidence, _ = _choice(Response(), "agent")
        self.assertEqual(choice, "zcode")
        self.assertEqual(confidence, 0.91)

    def test_plan_materializes_without_execution_paths(self):
        plan = {"tasks": [{"id": "fix-ui", "goal": "fix", "allowed_files": ["x"],
                            "verification": ["check"], "risk": "low",
                            "eligible_agents": ["zcode"], "needs_user_decision": False,
                            "user_question": None, "current_behavior": "bad",
                            "expected_behavior": "good"}]}
        with tempfile.TemporaryDirectory() as folder, patch.object(orchestrator, "QUEUE", Path(folder)):
            tasks = orchestrator.materialize_plan({"id": "request-12345678"}, plan)
            self.assertEqual(tasks[0]["status"], "Planned")
            self.assertTrue(tasks[0]["id"].startswith("12345678-"))
            self.assertNotIn("worktree", tasks[0])

    def test_verification_rejects_scope_violations(self):
        task = {"worktree": "C:/work", "base_commit": "abc", "allowed_files": ["allowed.cs"]}
        completed = type("Result", (), {"returncode": 0, "stdout": "other.cs\n"})()
        empty = type("Result", (), {"returncode": 0, "stdout": ""})()
        with patch.object(orchestrator.subprocess, "run", side_effect=[empty, completed, empty]):
            result = orchestrator.verify_worker_result(task)
        self.assertFalse(result["passed"])
        self.assertEqual(result["checks"]["scope_violations"], ["other.cs"])

    def test_serve_records_each_cycle_result(self):
        class StopAfterWait:
            stopped = False

            def is_set(self):
                return self.stopped

            def wait(self, _seconds):
                self.stopped = True

        with patch.object(orchestrator, "run_once", return_value="idle"), \
             patch.object(orchestrator, "update_state") as update, \
             patch.object(orchestrator, "config", return_value={"poll_seconds": 30}):
            orchestrator.serve(StopAfterWait())

        update.assert_called_once()
        self.assertEqual(update.call_args.kwargs["last_cycle_result"], "idle")
        self.assertIsNone(update.call_args.kwargs["last_cycle_error"])

    def test_full_autonomy_fallback_prefers_low_cost_agent(self):
        tasks = [
            {"id": "first", "eligible_agents": ["zcode", "codex"]},
            {"id": "second", "eligible_agents": ["antigravity"]},
        ]
        task, agent = orchestrator.fallback_route(tasks)
        self.assertEqual(task["id"], "first")
        self.assertEqual(agent, "zcode")

    def test_running_unity_defers_verification_without_attention(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "task.json"
            task = {"id": "x", "status": "Review", "_path": str(path)}
            verification = {
                "passed": False,
                "checks": {
                    "diff_check": "passed", "changed_files": ["Assets/X.cs"],
                    "scope_violations": [], "manual_play_mode_required": False,
                    "unity": {"passed": False, "deferred": True, "reason": "Unity is open."},
                },
            }
            with patch.object(orchestrator, "verify_worker_result", return_value=verification), \
                 patch.object(orchestrator, "emit") as emit, \
                 patch.object(orchestrator.codex_reviewer, "review_task", return_value={"verdict": "pass"}) as review, \
                 patch.object(orchestrator, "commit_worker_snapshot", return_value=("abc", "backup/x")) as commit:
                status = orchestrator.verify_and_review_task(task)

            self.assertEqual(status, "verificationpending")
            self.assertEqual(task["status"], "VerificationPending")
            review.assert_called_once()
            commit.assert_called_once()
            self.assertEqual(emit.call_args.args[0], "status")

    def test_open_unity_still_reviews_uncommitted_pending_work(self):
        with patch.object(orchestrator, "queue_tasks", side_effect=[[], [{
                 "id": "pending", "verification_result": {"checks": {"unity": {"deferred": True}}}
             }]]), patch.object(orchestrator, "unity_is_running", return_value=True):
            self.assertEqual(len(orchestrator.deferred_verification_tasks()), 1)

    def test_parallel_scheduler_blocks_overlapping_files(self):
        left = {"allowed_files": ["Assets/Scripts/UI/Menu.cs", "Assets/A.cs"]}
        same = {"allowed_files": ["Assets/Scripts/UI/Menu.cs"]}
        separate = {"allowed_files": ["Assets/Scripts/UI/Stage.cs"]}
        self.assertTrue(orchestrator.tasks_conflict(left, same))
        self.assertFalse(orchestrator.tasks_conflict(left, separate))


if __name__ == "__main__":
    unittest.main()
