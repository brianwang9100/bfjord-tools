# SPDX-License-Identifier: MIT
from contextlib import redirect_stdout
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import os
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

SPEC = importlib.util.spec_from_file_location('bfjord', Path(__file__).resolve().parents[1] / 'scripts/bfjord.py')
cli = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(cli)


class AssetRestoreTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name).resolve()
        self.original_root, self.original_catalogs = cli.ROOT, cli.CATALOGS
        cli.ROOT, cli.CATALOGS = self.root, ('catalog',)
        self.project = self.root / 'project'
        (self.project / 'Packages').mkdir(parents=True)
        (self.project / 'Packages/manifest.json').write_text('{}')
        self.catalog = self.root / 'catalog'
        self.entries = []

    def tearDown(self):
        cli.ROOT, cli.CATALOGS = self.original_root, self.original_catalogs
        self.temporary.cleanup()

    def source(self, name, contents):
        source = self.catalog / name
        source.parent.mkdir(parents=True, exist_ok=True)
        source.write_bytes(contents)
        self.entries.append({'path': name, 'bytes': len(contents), 'sha256': hashlib.sha256(contents).hexdigest()})
        (self.catalog / 'manifest.json').write_text(json.dumps({'schemaVersion': 1, 'files': self.entries}))
        return source

    def test_restore_is_repeatable_and_preserves_unrelated_assets(self):
        self.source('Assets/Plants/fern.fbx', b'geometry')
        unrelated = self.project / 'Assets/Unrelated.txt'
        unrelated.parent.mkdir(parents=True)
        unrelated.write_bytes(b'keep me')
        first = cli.prepare_project(self.project)
        second = cli.prepare_project(self.project)
        self.assertEqual(first['copied'], 1)
        self.assertEqual(second['copied'], 0)
        self.assertEqual(second['unchanged'], 1)
        self.assertEqual(unrelated.read_bytes(), b'keep me')

    def test_conflict_fails_before_copying_any_new_file(self):
        self.source('Assets/Plants/new.fbx', b'new geometry')
        self.source('Assets/Plants/existing.fbx', b'incoming geometry')
        target = self.project / 'Assets/Plants/existing.fbx'
        target.parent.mkdir(parents=True)
        target.write_bytes(b'artist edits')
        with self.assertRaises(ValueError):
            cli.prepare_project(self.project)
        self.assertEqual(target.read_bytes(), b'artist edits')
        self.assertFalse((self.project / 'Assets/Plants/new.fbx').exists())

    def test_modified_source_is_rejected_before_project_changes(self):
        source = self.source('Assets/Plants/fern.fbx', b'approved')
        source.write_bytes(b'changed')
        with self.assertRaises(ValueError):
            cli.prepare_project(self.project)
        self.assertFalse((self.project / 'Assets').exists())

    def test_dry_run_does_not_create_project_assets(self):
        self.source('Assets/Plants/fern.fbx', b'geometry')
        result = cli.prepare_project(self.project, dry_run=True)
        self.assertEqual(result['catalogFiles'], 1)
        self.assertEqual(result['copied'], 0)
        self.assertFalse((self.project / 'Assets').exists())

    def test_catalog_cannot_write_outside_assets(self):
        self.catalog.mkdir()
        (self.catalog / 'manifest.json').write_text(json.dumps({'schemaVersion': 1, 'files': [{'path': 'Assets/../../outside', 'sha256': 'a' * 64}]}))
        with self.assertRaises(ValueError):
            cli.prepare_project(self.project)
        self.assertFalse((self.root / 'outside').exists())


class RunRequestTests(unittest.TestCase):
    def test_resident_run_dispatches_original_request_file_with_target_scene(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            project = root / 'project'
            (project / 'Temp').mkdir(parents=True)
            (project / 'Temp/UnityLockfile').touch()
            request_path = root / 'request.json'
            request = {
                'schemaVersion': 1,
                'command': 'bwork_roads',
                'arguments': {'action': 'status'},
                'targetScene': 'Assets/Scenes/WorldAuthoringTools.unity',
            }
            request_path.write_text(json.dumps(request))
            args = SimpleNamespace(
                project=str(project), request=str(request_path), mode='auto',
                unity_cli=None, editor=None, result=None, timeout=245,
            )
            completed = SimpleNamespace(
                stdout=json.dumps({'success': True, 'data': {'success': True}}),
                stderr='', returncode=0,
            )

            with mock.patch.object(cli, 'executable', return_value='/tools/unity'), \
                    mock.patch.object(cli.subprocess, 'run', return_value=completed) as run:
                with redirect_stdout(io.StringIO()):
                    self.assertEqual(cli.run_request(args), 0)

            run.assert_called_once_with([
                '/tools/unity', 'command', 'bwork_request', '--requestPath', str(request_path),
                '--project-path', str(project), '--timeout', '245', '--format', 'json',
            ], capture_output=True, text=True)
            self.assertEqual(json.loads(request_path.read_text()), request)


class RunArgumentTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name).resolve()
        self.project = self.root / 'project'
        (self.project / 'Temp').mkdir(parents=True)
        (self.project / 'Temp/UnityLockfile').touch()
        self.request = self.root / 'request.json'
        self.request.write_text(json.dumps({
            'schemaVersion': 1,
            'command': 'bwork_verify',
            'arguments': {},
        }))
        self.recorded_arguments = self.root / 'unity-arguments.json'
        self.unity_cli = self.root / 'unity-cli'
        self.unity_cli.write_text(
            '#!/usr/bin/env python3\n'
            'import json, os, pathlib, sys\n'
            "pathlib.Path(os.environ['BFJORD_TEST_ARGUMENTS']).write_text(json.dumps(sys.argv[1:]))\n"
            "print(json.dumps({'success': True, 'data': {'success': True}}))\n"
        )
        self.unity_cli.chmod(0o755)

    def tearDown(self):
        self.temporary.cleanup()

    def invoke(self, *arguments):
        environment = os.environ.copy()
        environment['BFJORD_TEST_ARGUMENTS'] = str(self.recorded_arguments)
        return subprocess.run([
            sys.executable,
            str(Path(__file__).resolve().parents[1] / 'scripts/bfjord.py'),
            'run', '--mode', 'resident', '--unity-cli', str(self.unity_cli),
            '--project', str(self.project), '--request', str(self.request),
            *arguments,
        ], capture_output=True, text=True, env=environment)

    def test_resident_timeout_defaults_to_120_seconds_and_accepts_an_override(self):
        for arguments, expected in (((), '120'), (('--timeout', '240'), '240')):
            with self.subTest(arguments=arguments):
                completed = self.invoke(*arguments)

                self.assertEqual(completed.returncode, 0, completed.stderr)
                invocation = json.loads(self.recorded_arguments.read_text())
                self.assertEqual(invocation[invocation.index('--timeout') + 1], expected)

    def test_timeout_rejects_zero_negative_and_excessive_values_before_dispatch(self):
        for value in ('0', '-1', '3601', 'not-a-number'):
            with self.subTest(value=value):
                self.recorded_arguments.unlink(missing_ok=True)
                completed = self.invoke('--timeout', value)

                self.assertEqual(completed.returncode, 2)
                self.assertIn('must be between 1 and 3600 seconds', completed.stderr)
                self.assertFalse(self.recorded_arguments.exists())


if __name__ == '__main__':
    unittest.main()
