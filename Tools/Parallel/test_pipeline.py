from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import codex_planner
import pipeline_store


class PipelineStoreTests(unittest.TestCase):
    def test_request_is_persisted_and_normalized(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(pipeline_store, "REQUESTS", Path(folder)):
            request = pipeline_store.add_request("  Fix   the battle result flow.  ")
            self.assertEqual(request["status"], "Requested")
            self.assertEqual(request["description"], "Fix the battle result flow.")
            self.assertTrue(Path(folder, request["id"] + ".json").is_file())

    def test_invalid_transition_is_rejected(self):
        record = {"id": "x", "status": "Requested", "history": []}
        with self.assertRaises(ValueError):
            pipeline_store.transition(record, "Done")

    def test_sensitive_plans_require_review(self):
        ordinary = {"tasks": [{"risk": "low", "needs_user_decision": False,
                                "allowed_files": ["Assets/Scripts/UI/View.cs"]}]}
        scene = {"tasks": [{"risk": "low", "needs_user_decision": False,
                             "allowed_files": ["Assets/Scenes/Battle.unity"]}]}
        self.assertFalse(codex_planner.needs_review(ordinary))
        self.assertTrue(codex_planner.needs_review(scene))
        self.assertFalse(codex_planner.needs_review(scene, autonomy_mode="full"))
        blocked = {"tasks": [{"risk": "low", "needs_user_decision": True,
                               "user_question": "Provide the publishing credential.",
                               "allowed_files": ["Assets/Scripts/UI/View.cs"]}]}
        self.assertTrue(codex_planner.needs_review(blocked, autonomy_mode="full"))

    def test_plan_agent_choices_are_deduplicated_after_structured_output(self):
        plan = {"tasks": [{"eligible_agents": ["zcode", "zcode", "codex"]}]}
        normalized = codex_planner.normalize_plan(plan)
        self.assertEqual(normalized["tasks"][0]["eligible_agents"], ["zcode", "codex"])

    def test_user_questions_only_returns_unresolved_decisions(self):
        plan = {"tasks": [
            {"needs_user_decision": True, "user_question": "Which scope?"},
            {"needs_user_decision": False, "user_question": None},
        ]}
        self.assertEqual(codex_planner.user_questions(plan), ["Which scope?"])

    def test_plan_review_can_return_to_requested_for_user_decision(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(pipeline_store, "REQUESTS", Path(folder)):
            record = pipeline_store.add_request("Plan the outgame loop completely.")
            pipeline_store.transition(record, "Planning")
            pipeline_store.transition(record, "PlanReview")
            pipeline_store.transition(record, "Requested", user_decision="Focus on the core loop.")
            self.assertEqual(record["status"], "Requested")


if __name__ == "__main__":
    unittest.main()
