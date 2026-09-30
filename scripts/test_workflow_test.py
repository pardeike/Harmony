"""Exercise the local test command with an isolated fake dotnet process."""
import os
from pathlib import Path
import subprocess
import tempfile
import time
import unittest


ROOT = Path(__file__).resolve().parents[1]
FAKE = '''#!/usr/bin/env python3
import os, pathlib, subprocess, sys, time
args = sys.argv[1:]
case = os.environ["HARMONY_WORKFLOW_CASE"]
print("private build output")
if case == "exit":
    print("error TEST001: requested failure")
    sys.exit(7)
if case == "cancel":
    child = subprocess.Popen([sys.executable, "-c", "import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); print('ready',flush=True); time.sleep(60)"], stdout=subprocess.PIPE)
    child.stdout.readline()
    pathlib.Path(os.environ["HARMONY_WORKFLOW_ARGS"]).write_text(str(child.pid))
    time.sleep(60)
if "--results-directory" in args:
    directory = pathlib.Path(args[args.index("--results-directory") + 1])
    directory.mkdir(parents=True, exist_ok=True)
    if case != "missing":
        count = "0" if case == "empty" else "1"
        text = '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
        if count == "1":
            text += '<UnitTestResult outcome="Passed" />'
        text += '</Results><ResultSummary outcome="Completed"><Counters total="' + count + '" executed="' + count + '" passed="' + count + '" failed="0" /></ResultSummary></TestRun>'
        (directory / "test.trx").write_text("invalid" if case == "malformed" else text)
        if case == "second_failed":
            (directory / "failed.trx").write_text(text.replace('outcome="Passed"', 'outcome="Failed"'))
pathlib.Path(os.environ["HARMONY_WORKFLOW_ARGS"]).write_text("\\n".join(args))
'''


class WorkflowTests(unittest.TestCase):
    def run_case(self, case, extra=()):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            fake = root / "dotnet"
            fake.write_text(FAKE)
            fake.chmod(0o755)
            env = dict(os.environ, HARMONY_DOTNET=str(fake),
                       HARMONY_WORKFLOW_CASE=case, HARMONY_WORKFLOW_ARGS=str(root / "args"))
            result = subprocess.run(["bash", str(ROOT / "scripts/test.sh"), "--filter", "Example", *extra],
                                    env=env, capture_output=True, text=True, timeout=20)
            args = (root / "args").read_text() if (root / "args").exists() else ""
            return result, args

    def test_success_is_quiet_and_arguments_are_forwarded(self):
        result, args = self.run_case("success")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(result.stdout, "ok\n")
        self.assertEqual(result.stderr, "")
        self.assertIn("--filter\nExample", args)
        self.assertNotIn("--no-build", args)

    def test_child_failure_is_not_hidden(self):
        result, _ = self.run_case("exit")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("TEST001", result.stdout + result.stderr)
        self.assertIn(".log", result.stdout + result.stderr)

    def test_missing_empty_and_malformed_reports_fail(self):
        for case in ("missing", "empty", "malformed", "second_failed"):
            with self.subTest(case=case):
                result, _ = self.run_case(case)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("report", result.stdout + result.stderr)

    def test_runsettings_follow_the_workflow_options(self):
        result, args = self.run_case("success", ["--", "RunConfiguration.TargetPlatform=x64"])
        self.assertEqual(result.returncode, 0, result.stderr)
        arguments = args.splitlines()
        self.assertLess(arguments.index("--logger"), arguments.index("--"))
        self.assertIn("RunConfiguration.TreatNoTestsAsError=true", arguments)

    def test_redirected_reports_are_rejected_before_starting_tests(self):
        for option in ("--logger=console", "--logger:console", "--results-directory=/tmp"):
            with self.subTest(option=option):
                result, args = self.run_case("success", [option])
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(args, "")
                self.assertIn("full log:", result.stderr)

    @unittest.skipIf(os.name == "nt", "Unix process groups")
    def test_cancellation_stops_grandchildren_too(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            fake = root / "dotnet"
            fake.write_text(FAKE)
            fake.chmod(0o755)
            pid_file = root / "child"
            env = dict(os.environ, HARMONY_DOTNET=str(fake), HARMONY_WORKFLOW_CASE="cancel",
                       HARMONY_WORKFLOW_ARGS=str(pid_file))
            process = subprocess.Popen(["bash", str(ROOT / "scripts/test.sh")], env=env,
                                       stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            child_pid = None
            try:
                deadline = time.monotonic() + 10
                while not pid_file.exists() and time.monotonic() < deadline:
                    time.sleep(0.02)
                self.assertTrue(pid_file.exists(), "Fake test process never started")
                child_pid = int(pid_file.read_text())
                process.terminate()
                stdout, stderr = process.communicate(timeout=10)
                self.assertNotEqual(process.returncode, 0)
                self.assertNotIn("ok", stdout)
                self.assertIn("cancelled", stderr)
                deadline = time.monotonic() + 2
                while time.monotonic() < deadline:
                    try:
                        os.kill(child_pid, 0)
                    except ProcessLookupError:
                        return
                    time.sleep(0.02)
                self.fail("Cancellation left a grandchild running")
            finally:
                if process.poll() is None:
                    process.kill()
                    process.communicate(timeout=10)
                if child_pid:
                    try:
                        os.kill(child_pid, 9)
                    except ProcessLookupError:
                        pass


if __name__ == "__main__":
    unittest.main()
