import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import dispatch


class DispatchTests(unittest.TestCase):
    def test_exact_relative_paths(self):
        self.assertEqual(dispatch.safe_relative('Assets\\Scripts\\Example.cs'), 'Assets/Scripts/Example.cs')
        for path in ('../outside', '/root', 'C:/outside', 'x/../outside', 'Assets/**', 'x:stream', 'x//y'):
            with self.subTest(path=path), self.assertRaises(ValueError):
                dispatch.safe_relative(path)

    def test_task_requires_ready_matching_agent_base_and_scope(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'task.json'
            task = dict(status='Ready', agent='zcode', base_commit='abc', mode='plan', allowed_files=[], prompt='Read only')
            dispatch.write_json(path, task)
            self.assertEqual(dispatch.load_task(path, 'zcode', 'abc')['prompt'], 'Read only')
            for key, value in [('status', 'Draft'), ('agent', 'antigravity'), ('base_commit', 'wrong'),
                               ('mode', 'yolo'), ('allowed_files', ['Assets/Example.cs']), ('prompt', ''),
                               ('goal', '')]:
                dispatch.write_json(path, {**task, key: value})
                with self.subTest(key=key), self.assertRaises(ValueError):
                    dispatch.load_task(path, 'zcode', 'abc')

    def test_duplicate_agent_and_worktree_rejected(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(dispatch, 'LOCAL', Path(folder)):
            with dispatch.leases('zcode', Path(folder) / 'one', 'first'):
                with self.assertRaises(FileExistsError):
                    with dispatch.leases('zcode', Path(folder) / 'two', 'second'):
                        self.fail('duplicate agent acquired')
                with self.assertRaises(FileExistsError):
                    with dispatch.leases('antigravity', Path(folder) / 'one', 'third'):
                        self.fail('duplicate worktree acquired')
                self.assertTrue((Path(folder) / 'locks/zcode.lock').exists())
                self.assertFalse((Path(folder) / 'locks/antigravity.lock').exists())
            self.assertEqual(list((Path(folder) / 'locks').iterdir()), [])

    def test_antigravity_error_with_zero_exit_is_not_success(self):
        result = dispatch.parse_result('antigravity', json.dumps(dict(status='ERROR', response='')), 0)
        self.assertEqual(result['status'], 'Failed')

    def test_success_is_review_not_verified_development(self):
        result = dispatch.parse_result('antigravity', json.dumps(dict(status='SUCCESS', response='done')), 0)
        self.assertEqual(result['status'], 'Review')

    def test_zcode_actual_envelope(self):
        result = dispatch.parse_result('zcode', json.dumps(dict(sessionId='sess_test', response='done')), 0)
        self.assertEqual(result['session_id'], 'sess_test')
        self.assertEqual(result['status'], 'Review')

    def test_codex_plain_output(self):
        result = dispatch.parse_result('codex', 'completed', 0)
        self.assertEqual(result['status'], 'Review')
        self.assertEqual(dispatch.parse_result('codex', '', 0)['status'], 'Failed')

    def test_codex_uses_bounded_workspace_mode(self):
        with patch.object(dispatch, 'executable', return_value=(['codex.cmd'], {'model': 'configured-default'})):
            args, _, stdin_text = dispatch.command('codex', 'task', 'edit', 60, Path('C:/work'))
        self.assertIn('--approve-for-me', args)
        self.assertNotIn('--sandbox', args)
        self.assertNotIn('--ask-for-approval', args)
        self.assertNotIn('--dangerously-bypass-approvals-and-sandbox', args)
        self.assertEqual(args[-1], '-')
        self.assertEqual(stdin_text, 'task')

    def test_invalid_output_and_process_failure(self):
        for output in ('not json', '[]', '{}'):
            self.assertEqual(dispatch.parse_result('zcode', output, 0)['status'], 'Failed')
        self.assertEqual(dispatch.parse_result('zcode', '{"response":"partial"}', 1)['status'], 'Failed')

    def test_prompt_is_one_argument_and_permissions_not_bypassed(self):
        prompt = 'Read "a file"; $(not-a-command)\n한글 테스트'
        with patch.object(dispatch, 'executable', return_value=(['agy.exe'], {'model': 'gemini-3.1-pro-high'})):
            args, _, stdin_text = dispatch.command('antigravity', prompt, 'edit', 60, Path('.'))
        print_flag = '-p' if '-p' in args else '--print'
        self.assertEqual(args[args.index(print_flag) + 1], prompt)
        self.assertIsNone(stdin_text)
        self.assertNotIn('--dangerously-skip-permissions', args)
        self.assertIn('accept-edits', args)

    def test_zcode_goal_and_turn_budget_are_forwarded(self):
        prompt = 'Implement the assigned task.'
        goal = 'Finish the task and write the handoff before stopping.'
        with patch.object(dispatch, 'executable', return_value=(['zcode.cjs'], {
            'model': 'GLM-5.3-Flash', 'provider': 'zai'
        })), patch.object(dispatch, 'read_json', return_value={
            'provider': {'zai': {'options': {'apiKey': 'key', 'baseURL': 'https://example.invalid'}}}
        }):
            args, _, stdin_text = dispatch.command('zcode', prompt, 'edit', 60, Path('.'),
                                                   goal=goal, max_turns=18)
        self.assertNotIn('--target', args)
        self.assertNotIn('--max-turns', args)
        self.assertEqual(args[args.index('--prompt') + 1], 'Task objective: ' + goal + '\n\n' + prompt)
        self.assertNotIn('--dangerously-skip-permissions', args)
        self.assertIsNone(stdin_text)


if __name__ == '__main__':
    unittest.main()
