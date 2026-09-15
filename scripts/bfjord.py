#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Prepare the bundled sample and dispatch structured Unity authoring requests."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
CATALOGS = (
    'assets/CoastalBridgeTool/demo-dependencies',
    'assets/BFjordTools/asset-catalog',
)
MAX_RESIDENT_TIMEOUT_SECONDS = 3600


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative_asset(value):
    if not isinstance(value, str) or not value.startswith('Assets/') or '\\' in value or ':' in value:
        raise ValueError('Catalog destination must be a relative Assets/ path')
    if any(p in ('', '.', '..') for p in value.split('/')) or PurePosixPath(value).is_absolute():
        raise ValueError('Catalog path cannot traverse directories')
    return Path(value)


def reject_links(path):
    for parent in (path, *path.parents):
        if parent.is_symlink():
            raise ValueError('Symlinked source or destination is not supported: ' + str(parent))


def prepare_project(project, dry_run=False):
    project = Path(os.path.abspath(project))
    reject_links(project)
    if not (project / 'Packages/manifest.json').is_file():
        raise ValueError('Choose a Unity project containing Packages/manifest.json')
    plans = {}
    for catalog in CATALOGS:
        folder = ROOT / catalog
        manifest = json.loads((folder / 'manifest.json').read_text())
        if manifest.get('schemaVersion') != 1 or not isinstance(manifest.get('files'), list):
            raise ValueError('Unsupported asset catalog schema')
        for item in manifest['files']:
            relative = relative_asset(item['path'])
            source, target = folder / relative, project / relative
            reject_links(source)
            reject_links(target)
            if not source.is_file() or digest(source) != item['sha256']:
                raise ValueError('Source asset hash mismatch: ' + str(relative))
            if 'bytes' in item and source.stat().st_size != item['bytes']:
                raise ValueError('Source asset size mismatch: ' + str(relative))
            if relative in plans and plans[relative][2] != item['sha256']:
                raise ValueError('Catalogs disagree about destination: ' + str(relative))
            if target.exists() and (not target.is_file() or digest(target) != item['sha256']):
                raise ValueError('Existing asset differs; preserve it before preparing this sample: ' + str(relative))
            plans[relative] = (source, target, item['sha256'])
    created = []
    try:
        for source, target, expected in plans.values():
            if target.exists() or dry_run:
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            reject_links(target)
            with target.open('xb') as output, source.open('rb') as input_file:
                created.append(target)
                shutil.copyfileobj(input_file, output)
            if digest(target) != expected:
                raise ValueError('Asset changed during copy: ' + str(target))
    except Exception:
        for target in reversed(created):
            target.unlink()
        raise
    return {'state': 'prepared' if dry_run else 'restored', 'catalogFiles': len(plans),
            'copied': len(created), 'unchanged': sum(t.exists() for _, t, _ in plans.values()) - len(created)}


def executable(value, candidates):
    if value:
        path = Path(value).expanduser()
        if path.is_file():
            return str(path)
        found = shutil.which(value)
        if found:
            return found
        raise ValueError('Executable not found: ' + value)
    for path in candidates:
        if Path(path).is_file():
            return str(path)
    return None


def resident_timeout(value):
    try:
        seconds = int(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError(
            f'must be between 1 and {MAX_RESIDENT_TIMEOUT_SECONDS} seconds'
        ) from error
    if not 1 <= seconds <= MAX_RESIDENT_TIMEOUT_SECONDS:
        raise argparse.ArgumentTypeError(
            f'must be between 1 and {MAX_RESIDENT_TIMEOUT_SECONDS} seconds'
        )
    return seconds


def run_request(args):
    project = Path(args.project).resolve()
    request_path = Path(args.request).resolve()
    request = json.loads(request_path.read_text())
    if request.get('schemaVersion') != 1 or not isinstance(request.get('arguments', {}), dict):
        raise ValueError('Expected schemaVersion 1 command request')
    command = request.get('command')
    if not isinstance(command, str) or not command.startswith('bwork_') or not command.replace('_', '').isalnum():
        raise ValueError('A named bwork_* authoring command is required')
    active = (project / 'Temp/UnityLockfile').exists()
    mode = ('resident' if active else 'batch') if args.mode == 'auto' else args.mode
    if mode == 'resident':
        cli = executable(args.unity_cli, ['/Applications/Unity Hub.app/Contents/Resources/cli/unity'])
        if not cli:
            raise ValueError('Pass --unity-cli for the resident Unity Pipeline CLI')
        invocation = [cli, 'command', 'bwork_request', '--requestPath', str(request_path),
                      '--project-path', str(project), '--timeout', str(args.timeout), '--format', 'json']
    else:
        if active:
            raise ValueError('The project is locked by an Editor; use resident mode or close that Editor first')
        editor = executable(args.editor, ['/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity'])
        if not editor:
            raise ValueError('Pass --editor with the Unity Editor executable')
        result_path = Path(args.result or project / 'Temp/bfjord-result.json').resolve()
        if result_path.exists():
            raise ValueError('Choose a new --result path or remove the previous command result before running another batch')
        if result_path.with_suffix('.log').exists():
            raise ValueError('Choose a --result path whose sibling Unity log does not already exist')
        result_path.parent.mkdir(parents=True, exist_ok=True)
        invocation = [editor, '-batchmode', '-projectPath', str(project), '-executeMethod',
                      'Bwork.Authoring.Editor.AuthoringBatch.Run', '-bfjordRequest', str(request_path),
                      '-bfjordResult', str(result_path), '-logFile', str(result_path.with_suffix('.log'))]
    completed = subprocess.run(invocation, capture_output=True, text=True)
    if completed.stdout:
        print(completed.stdout, end='')
    if completed.stderr:
        print(completed.stderr, end='', file=sys.stderr)
    if completed.returncode:
        return completed.returncode
    if mode == 'batch':
        if not result_path.is_file():
            raise ValueError('Unity exited without writing a result; inspect its log')
        payload = json.loads(result_path.read_text())
        print(json.dumps(payload, indent=2))
    else:
        payload = json.loads(completed.stdout)
    return 0 if payload.get('success', False) and payload.get('data', {}).get('success', True) else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    sub.add_parser('doctor', help='Describe local tool availability without starting Unity')
    prepare = sub.add_parser('prepare-project', help='Restore hash-verified CC0 sample assets; refuse conflicting files')
    prepare.add_argument('--project', default=str(ROOT / 'Examples~/SandboxProject'))
    prepare.add_argument('--dry-run', action='store_true')
    run = sub.add_parser('run', help='Execute a JSON request using the active Editor or an unlocked batch project')
    run.add_argument('--project', required=True)
    run.add_argument('--request', required=True)
    run.add_argument('--mode', choices=['auto', 'resident', 'batch'], default='auto')
    run.add_argument('--unity-cli')
    run.add_argument('--editor')
    run.add_argument('--result')
    run.add_argument('--timeout', type=resident_timeout, default=120,
                     help='resident Unity Pipeline command timeout in seconds (default: 120; maximum: 3600)')
    args = parser.parse_args()
    if args.action == 'doctor':
        print(json.dumps({'repository': str(ROOT), 'python': sys.version.split()[0],
                          'samplePresent': (ROOT / 'Examples~/SandboxProject/Packages/manifest.json').is_file(),
                          'catalogsPresent': all((ROOT / p / 'manifest.json').is_file() for p in CATALOGS),
                          'note': 'Tested with Unity 6000.6.0f1, URP 17.6.0 and Pipeline 0.7.0-exp.1.'}, indent=2))
    elif args.action == 'prepare-project':
        print(json.dumps(prepare_project(args.project, args.dry_run), indent=2))
    else:
        return run_request(args)
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (ValueError, OSError, json.JSONDecodeError, KeyError) as error:
        print(json.dumps({'success': False, 'error': str(error)}, indent=2), file=sys.stderr)
        sys.exit(1)
