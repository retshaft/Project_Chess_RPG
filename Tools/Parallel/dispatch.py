"""Run the installed external agents in registered worktrees; no shell interpolation."""
import argparse
import contextlib
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / '.parallel'
CONFIG = Path(__file__).with_name('agents.json')
AGENTS = ('zcode', 'antigravity', 'codex')


def git(worktree, *args):
    result = subprocess.run(['git', '-C', str(worktree), *args], capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.decode('utf-8', 'replace').strip())
    return result.stdout.decode('utf-8', 'replace').strip()


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + '.tmp')
    temp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    temp.replace(path)


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def validate_worktree(path, agent, allowed_files=None):
    path = Path(path).resolve(strict=True)
    if not path.is_relative_to((ROOT / '.worktrees').resolve()) or not (path / '.git').is_file():
        raise ValueError('Use a linked worktree under this project/.worktrees.')
    if Path(git(path, 'rev-parse', '--show-toplevel')).resolve() != path:
        raise ValueError('The path must be the worktree root.')
    common = Path(git(path, 'rev-parse', '--path-format=absolute', '--git-common-dir')).resolve()
    if common != Path(git(ROOT, 'rev-parse', '--path-format=absolute', '--git-common-dir')).resolve():
        raise ValueError('The worktree belongs to another repository.')
    branch = git(path, 'branch', '--show-current')
    if not branch.startswith(f'codex/{agent}/'):
        raise ValueError('The branch does not belong to the selected agent.')
    dirty = git(path, 'status', '--porcelain')
    if dirty:
        changed = [line[3:] for line in dirty.splitlines() if len(line) > 3]
        violations = sorted(set(changed) - set(allowed_files or []))
        if violations:
            raise ValueError('Worktree has changes outside the task scope: ' + ', '.join(violations))
    return path, branch, git(path, 'rev-parse', 'HEAD')


def safe_relative(path):
    # Exact files, not glob patterns; avoid traversal and Windows alternate streams.
    path = str(path).replace('\\', '/')
    if not path or path.startswith('/') or any(p in ('', '.', '..') for p in path.split('/')):
        raise ValueError('Allowed file paths must be normalized relative paths.')
    if any(c in path for c in ':*?[]'):
        raise ValueError('Allowed file paths must be exact files without drive names or globs.')
    return path


def load_task(path, agent, base):
    task = read_json(Path(path))
    if task.get('status') != 'Ready' or task.get('agent') != agent:
        raise ValueError('Task must be Ready and assigned to the selected agent.')
    if task.get('base_commit') != base:
        raise ValueError('Task base_commit does not match the worktree HEAD.')
    if task.get('mode') not in ('plan', 'edit'):
        raise ValueError('Task mode must be plan or edit.')
    if not isinstance(task.get('prompt'), str) or not task['prompt'].strip():
        raise ValueError('Task prompt is empty.')
    if 'goal' in task and (not isinstance(task.get('goal'), str) or not task['goal'].strip()):
        raise ValueError('Task goal must be a non-empty string when provided.')
    if not isinstance(task.get('allowed_files'), list):
        raise ValueError('Task requires an allowed_files list.')
    task['allowed_files'] = [safe_relative(p) for p in task['allowed_files']]
    if task['mode'] == 'plan' and task['allowed_files']:
        raise ValueError('Plan tasks cannot allow file changes.')
    return task


@contextlib.contextmanager
def leases(agent, worktree, run_id):
    folder = LOCAL / 'locks'
    folder.mkdir(parents=True, exist_ok=True)
    workspace_key = hashlib.sha256(str(worktree).lower().encode()).hexdigest()[:16]
    acquired = []
    try:
        for name in (agent, 'workspace-' + workspace_key):
            path = folder / (name + '.lock')
            with path.open('x', encoding='utf-8') as stream:
                json.dump({'run_id': run_id, 'runner_pid': os.getpid(), 'worktree': str(worktree)}, stream)
            acquired.append(path)
        yield
    finally:
        for path in reversed(acquired):
            path.unlink(missing_ok=True)


def executable(agent):
    config = read_json(CONFIG)[agent]
    if agent == 'codex':
        binary = shutil.which('codex.cmd') or shutil.which('codex')
        if not binary:
            raise FileNotFoundError('Codex CLI is required for Codex dispatch.')
        return [binary], config
    binary = Path(os.environ['LOCALAPPDATA']) / config['cliRelativePath']
    if not binary.is_file():
        raise FileNotFoundError(f'{agent} executable not found: {binary}')
    if agent == 'zcode':
        node = shutil.which('node')
        if not node:
            raise FileNotFoundError('Node.js is required for the bundled ZCode CLI.')
        return [node, str(binary)], config
    return [str(binary)], config


def command(agent, prompt, mode, timeout, worktree, goal=None, max_turns=24):
    prefix, config = executable(agent)
    env = os.environ.copy()
    if agent == 'antigravity':
        return prefix + ['--model', config['model'], '--mode', 'plan' if mode == 'plan' else 'accept-edits',
                         '--print', prompt, '--output-format', 'json', '--print-timeout', f'{timeout}s'], env, None
    if agent == 'codex':
        permissions = ['--sandbox', 'read-only'] if mode == 'plan' else ['--approve-for-me']
        return prefix + [
            'exec', '--cd', str(worktree), *permissions,
            '--ephemeral', '--color', 'never', '-'
        ], env, prompt
    # Installed 0.16.5 rejects advertised turn flags; use a bounded prompt run.
    # ZCODE_MODEL is supported by its actual environment parser.
    env['ZCODE_MODEL'] = config['provider'] + '/' + config['model']
    # This version drops the configured provider credential when ZCODE_MODEL overrides
    # model.main. Pass the credential created by official CLI login only to this child.
    # Never copy credentials into project files, command arguments, or result records.
    user_config = read_json(Path.home() / '.zcode/cli/config.json')
    provider = user_config.get('provider', {}).get(config['provider'], {})
    options = provider.get('options', {})
    if not options.get('apiKey') or not options.get('baseURL'):
        raise ValueError('ZCode CLI login is required before automatic dispatch.')
    env['ANTHROPIC_API_KEY'] = options['apiKey']
    env['ZCODE_BASE_URL'] = options['baseURL']
    target = goal or (
        'Complete the delegated task end-to-end. Keep working through inspection, '
        'implementation, self-review, and handoff; do not stop after making a plan. '
        'Stop only when the task is complete or a real blocker requires the lead developer.'
    )
    return prefix + [
        '--cwd', str(worktree), '--mode', mode, '--json',
        '--prompt', 'Task objective: ' + target + '\n\n' + prompt
    ], env, None


def parse_result(agent, output, exit_code):
    if agent == 'codex':
        good = exit_code == 0 and isinstance(output, str) and bool(output.strip())
        return {'status': 'Review' if good else 'Failed', 'response': output.strip(),
                'session_id': None,
                'reason': None if good else 'Codex CLI failed or returned no final response.'}
    try:
        payload = json.loads(output)
    except (ValueError, TypeError):
        return {'status': 'Failed', 'reason': 'Agent did not return valid JSON; inspect stdout/stderr.'}
    if not isinstance(payload, dict):
        return {'status': 'Failed', 'reason': 'Agent returned an unexpected JSON shape.'}
    if agent == 'antigravity':
        good = exit_code == 0 and payload.get('status') == 'SUCCESS'
        return {'status': 'Review' if good else 'Failed', 'response': payload.get('response', ''),
                'session_id': payload.get('conversation_id'), 'provider_status': payload.get('status'),
                'usage': payload.get('usage'), 'error': payload.get('error')}
    # Keep unknown versions conservative until their actual envelope is verified.
    result = payload.get('result', payload)
    if not isinstance(result, dict):
        return {'status': 'Failed', 'reason': 'Unrecognized ZCode result envelope.'}
    response = result.get('text') or result.get('response') or result.get('content')
    good = exit_code == 0 and isinstance(response, str) and bool(response.strip())
    return {'status': 'Review' if good else 'Failed', 'response': response or '',
            'session_id': result.get('sessionId') or payload.get('sessionId'),
            'reason': None if good else 'Inspect ZCode JSON envelope and authentication.'}


def stop_process(proc):
    if proc.poll() is not None:
        return
    if os.name == 'nt':
        subprocess.run(['taskkill', '/PID', str(proc.pid), '/T', '/F'], capture_output=True)
    else:
        proc.kill()
    proc.wait(timeout=15)


def changed_files(worktree, base):
    changed = git(worktree, 'diff', '--name-only', '--no-renames', '-z', base).split('\0')
    changed += git(worktree, 'ls-files', '--others', '--exclude-standard', '-z').split('\0')
    return sorted(set(p for p in changed if p))


def run(args):
    task_preview = None if args.smoke else read_json(Path(args.task))
    worktree, branch, base = validate_worktree(
        args.worktree, args.agent, (task_preview or {}).get('allowed_files', [])
    )
    if args.smoke:
        task = {'mode': 'plan', 'allowed_files': [], 'prompt':
                'Connectivity check only. Do not use tools, read files, or modify anything. '
                'Reply exactly PARALLEL_CONNECTION_OK.'}
    else:
        task = load_task(args.task, args.agent, base)
    prompt = task['prompt'] if args.smoke else (
        'You are a delegated developer in Checkmate RPG. Read AGENTS.md and '
        'Docs/Parallel/WORKFLOW.md first. This Ready task is assigned by the lead developer. '
        'Work only in the current worktree. Do not change branches, commit, merge, push, '
        'launch Unity, or access another worktree. The lead handles Git integration and Unity tests. '
        'Do not start subagents. Report blocked permissions and unverified tests. '
        'Your final response is the handoff. Only these exact files may change: '
        + json.dumps(task['allowed_files'], ensure_ascii=False) + '\n\n' + task['prompt'])
    goal = None if args.smoke else task.get('goal')
    cmd, env, stdin_text = command(args.agent, prompt, task['mode'], args.timeout, worktree,
                                   goal=goal, max_turns=args.max_turns)
    run_id = dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ-') + uuid.uuid4().hex[:8]
    directory = LOCAL / 'runs' / run_id
    with leases(args.agent, worktree, run_id):
        directory.mkdir(parents=True)
        write_json(directory / 'task.json', task)
        record = {'run_id': run_id, 'agent': args.agent, 'model': read_json(CONFIG)[args.agent]['model'],
                  'worktree': str(worktree), 'branch': branch, 'base_commit': base, 'status': 'Running',
                  'runner_pid': os.getpid(), 'started_utc': dt.datetime.now(dt.timezone.utc).isoformat()}
        write_json(directory / 'result.json', record)
        print(json.dumps({'run_id': run_id, 'status': 'Running', 'result': str(directory / 'result.json')}), flush=True)
        proc = None
        started = time.monotonic()
        monitor = None
        try:
            with (directory / 'stdout.json').open('wb') as out, (directory / 'stderr.log').open('wb') as err:
                show_window = bool(read_json(Path(__file__).with_name('automation_config.json')).get('show_agent_windows', False))
                if show_window and os.name == 'nt':
                    monitor_title = f"Agent {args.agent} - {task.get('id', run_id)}"
                    monitor_cmd = (
                        f"$host.UI.RawUI.WindowTitle='{monitor_title}'; "
                        f"Write-Host '실행 로그: {directory}'; "
                        f"Get-Content -LiteralPath '{directory / 'stdout.json'}' -Wait"
                    )
                    monitor = subprocess.Popen(
                        ['powershell.exe', '-NoProfile', '-NoExit', '-Command', monitor_cmd],
                        creationflags=subprocess.CREATE_NEW_CONSOLE,
                    )
                proc = subprocess.Popen(cmd, cwd=worktree, env=env,
                                        stdin=subprocess.PIPE if stdin_text is not None else subprocess.DEVNULL,
                                        stdout=out, stderr=err,
                                        creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
                record['agent_pid'] = proc.pid
                write_json(directory / 'result.json', record)
                try:
                    proc.communicate(
                        input=stdin_text.encode('utf-8') if stdin_text is not None else None,
                        timeout=args.timeout + 15,
                    )
                    exit_code = proc.returncode
                    record.update(parse_result(args.agent, (directory / 'stdout.json').read_text(encoding='utf-8-sig', errors='replace'), exit_code))
                    record['exit_code'] = exit_code
                except subprocess.TimeoutExpired:
                    stop_process(proc)
                    record.update(status='TimedOut', reason='Time limit reached. No automatic retry.')
        except (Exception, KeyboardInterrupt) as exc:
            if proc:
                stop_process(proc)
            record.update(status='Failed', reason=str(exc))
        finally:
            if monitor and monitor.poll() is None:
                # Keep the window open so the user can inspect the completed run.
                pass
            try:
                record['changed_files'] = changed_files(worktree, base)
                record['head_after'] = git(worktree, 'rev-parse', 'HEAD')
                violations = sorted(set(record['changed_files']) - set(task['allowed_files']))
                if violations or record['head_after'] != base or git(worktree, 'branch', '--show-current') != branch:
                    record.update(status='ScopeViolation', out_of_scope=violations,
                                  reason='Changes preserved for lead review. Nothing was reset.')
                if args.smoke and record['status'] == 'Review':
                    record['status'] = 'ConnectionVerified' if record.get('response', '').strip() == 'PARALLEL_CONNECTION_OK' else 'Failed'
            except Exception as exc:
                record.update(status='Failed', reason='Post-run verification failed: ' + str(exc))
            record['duration_seconds'] = round(time.monotonic() - started, 2)
            record['finished_utc'] = dt.datetime.now(dt.timezone.utc).isoformat()
            write_json(directory / 'result.json', record)
        print(json.dumps(record, ensure_ascii=False), flush=True)
        return 0 if record['status'] in ('Review', 'ConnectionVerified') else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    sub.add_parser('doctor')
    sub.add_parser('status')
    run_parser = sub.add_parser('run')
    run_parser.add_argument('--agent', choices=AGENTS, required=True)
    run_parser.add_argument('--worktree', required=True)
    source = run_parser.add_mutually_exclusive_group(required=True)
    source.add_argument('--task', help='Ready task JSON file')
    source.add_argument('--smoke', action='store_true')
    run_parser.add_argument('--timeout', type=int, default=600)
    run_parser.add_argument('--max-turns', type=int, default=24,
                            help='Maximum ZCode turns for the session goal.')
    args = parser.parse_args()
    if args.action == 'doctor':
        for agent in AGENTS:
            try:
                cmd, config = executable(agent)
                print(json.dumps({'agent': agent, 'installed': True, 'command': cmd, 'model': config['model'],
                                  'authentication': 'Not checked; run a smoke test.'}))
            except Exception as exc:
                print(json.dumps({'agent': agent, 'installed': False, 'reason': str(exc)}))
        return 0
    if args.action == 'status':
        for path in sorted((LOCAL / 'runs').glob('*/result.json')):
            data = read_json(path)
            print(json.dumps({k: data.get(k) for k in ('run_id', 'agent', 'model', 'status', 'worktree')}, ensure_ascii=False))
        for path in sorted((LOCAL / 'locks').glob('*.lock')):
            print(json.dumps({'lock': str(path), **read_json(path)}))
        return 0
    if not 15 <= args.timeout <= 3600:
        raise ValueError('Timeout must be between 15 and 3600 seconds.')
    return run(args)


if __name__ == '__main__':
    try:
        sys.exit(main())
    except Exception as error:
        print(json.dumps({'status': 'Blocked', 'reason': str(error)}, ensure_ascii=False), file=sys.stderr)
        sys.exit(2)
