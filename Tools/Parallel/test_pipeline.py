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


if __name__ == "__main__":
    unittest.main()
