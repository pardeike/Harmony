"""Build and test Harmony, retaining logs and requiring completed TRX reports."""
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]


class Cancelled(Exception):
    pass


def verify_reports(directory):
    reports = list(directory.glob("*.trx"))
    if not reports:
        raise ValueError("missing test report")
    for report in reports:
        root = ET.parse(report).getroot()
        ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        summary = root.find("t:ResultSummary", ns)
        if summary is None or summary.get("outcome") != "Completed":
            raise ValueError(f"unfinished test report: {report.name}")
        counters = summary.find("t:Counters", ns)
        if counters is None:
            raise ValueError(f"missing counters in report: {report.name}")
        counts = {key: int(value) for key, value in counters.attrib.items()}
        results = root.findall("t:Results/t:UnitTestResult", ns)
        passed = sum(item.get("outcome") == "Passed" for item in results)
        executed = sum(item.get("outcome") in ("Passed", "Inconclusive") for item in results)
        if (not results or passed == 0 or counts.get("total") != len(results)
                or counts.get("passed") != passed or counts.get("executed") != executed
                or any(item.get("outcome") not in ("Passed", "NotExecuted", "Inconclusive") for item in results)
                or any(counts.get(key, 0) != 0 for key in
                       ("failed", "error", "timeout", "aborted", "passedButRunAborted", "notRunnable",
                        "disconnected", "inProgress", "pending"))
                or any(value < 0 for value in counts.values())):
            raise ValueError(f"empty, failed or inconsistent test report: {report.name}")


def main():
    parent = ROOT / "artifacts" / "tests"
    parent.mkdir(parents=True, exist_ok=True)
    directory = Path(tempfile.mkdtemp(prefix="run-", dir=parent))
    log = directory / "test.log"
    arguments = sys.argv[1:]
    separator = arguments.index("--") if "--" in arguments else len(arguments)
    settings = arguments[separator + 1:]
    arguments = arguments[:separator]
    # These are owned by the workflow so an old or redirected report cannot prove success.
    if any(re.split("[=:]", arg, maxsplit=1)[0] in ("--results-directory", "--logger", "-l") for arg in arguments):
        message = "failed: arguments; the workflow owns --results-directory and --logger"
        log.write_text(message + "\n")
        print(f"{message}; full log: {log}", file=sys.stderr)
        return 2
    command = [os.environ.get("HARMONY_DOTNET", "dotnet"), "test", str(ROOT / "HarmonyTests/HarmonyTests.csproj"),
               "--nologo", "-v", "quiet", *arguments, "--results-directory", str(directory),
               "--logger", "trx;LogFilePrefix=tests", "--", *settings,
               "RunConfiguration.TreatNoTestsAsError=true", f"RunConfiguration.ResultsDirectory={directory}"]
    process = None

    def cancel(signum, _frame):
        # Unwind Popen.wait before waiting again; its waitpid lock is not reentrant.
        raise Cancelled(signum)

    signal.signal(signal.SIGTERM, cancel)
    signal.signal(signal.SIGINT, cancel)
    try:
        with log.open("w") as output:
            process = subprocess.Popen(command, cwd=ROOT, stdout=output, stderr=subprocess.STDOUT,
                                       start_new_session=os.name != "nt")
            code = process.wait()
        if code:
            lines = log.read_text(errors="replace").splitlines()
            diagnostics = [line for line in lines if re.search(r"error|failed|exception|not found", line, re.I)]
            print(f"failed: build/test (exit {code}); full log: {log}", file=sys.stderr)
            print("\n".join((diagnostics or lines[-8:])[:12]), file=sys.stderr)
            return code if code > 0 else 1
        verify_reports(directory)
    except Cancelled as error:
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        signal.signal(signal.SIGINT, signal.SIG_IGN)
        if process is not None:
            try:
                if os.name == "nt":
                    process.terminate()
                else:
                    os.killpg(process.pid, signal.SIGTERM)
                process.wait(timeout=3)
            except (subprocess.TimeoutExpired, ProcessLookupError):
                pass
            finally:
                try:
                    if os.name == "nt":
                        process.kill()
                    else:
                        # The parent can exit while a grandchild ignores termination.
                        os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                process.wait()
        print(f"failed: cancelled; full log: {log}", file=sys.stderr)
        return 128 + error.args[0]
    except (OSError, ValueError, ET.ParseError) as error:
        print(f"failed: test command/report: {error}; full log: {log}", file=sys.stderr)
        return 1
    print("ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())
