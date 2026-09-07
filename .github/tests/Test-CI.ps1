param(
    [string]$Framework,
    [string]$FailureAt,
    [string]$ReportKind = 'valid',
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'

function New-TestReport([string]$Path, [string]$Kind = 'valid') {
    $xml = '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results><UnitTestResult outcome="Passed" /></Results><ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1" failed="0" error="0" timeout="0" aborted="0" passedButRunAborted="0" notRunnable="0" disconnected="0" inProgress="0" pending="0" /></ResultSummary></TestRun>'
    switch ($Kind) {
        'missing' { return }
        'malformed' { $xml = '<TestRun>' }
        'empty' { $xml = '' }
        'zero-tests' { $xml = $xml.Replace('total="1" executed="1" passed="1"', 'total="0" executed="0" passed="0"').Replace('<UnitTestResult outcome="Passed" />', '') }
        'no-results' { $xml = $xml.Replace('<UnitTestResult outcome="Passed" />', '') }
        'unfinished' { $xml = $xml.Replace('outcome="Completed"', 'outcome="InProgress"') }
        'failed' { $xml = $xml.Replace('failed="0"', 'failed="1"') }
        'bad-count' { $xml = $xml.Replace('executed="1"', 'executed="invalid"') }
        'missing-count' { $xml = $xml.Replace(' executed="1"', '') }
        'pending' { $xml = $xml.Replace('pending="0"', 'pending="1"') }
        'aborted-after-pass' { $xml = $xml.Replace('passedButRunAborted="0"', 'passedButRunAborted="1"') }
        'disconnected' { $xml = $xml.Replace('disconnected="0"', 'disconnected="1"') }
        'false-passed-count' { $xml = $xml.Replace('total="1" executed="1" passed="1"', 'total="2" executed="2" passed="2"') }
        'hidden-failure' { $xml = $xml.Replace('</Results>', '<UnitTestResult outcome="Failed" /></Results>') }
        'with-skipped' { $xml = $xml.Replace('total="1"', 'total="2"').Replace('</Results>', '<UnitTestResult outcome="NotExecuted" /></Results>') }
        'with-inconclusive' { $xml = $xml.Replace('total="1" executed="1"', 'total="2" executed="2"').Replace('</Results>', '<UnitTestResult outcome="Inconclusive" /></Results>') }
        'missing-executed-result' { $xml = $xml.Replace('total="1" executed="1"', 'total="2" executed="2"') }
        'missing-skipped-result' { $xml = $xml.Replace('total="1"', 'total="2"') }
        'unaccounted-result' { $xml = $xml.Replace('</Results>', '<UnitTestResult outcome="NotExecuted" /></Results>') }
        'overstated-executed' { $xml = $xml.Replace('total="1" executed="1"', 'total="2" executed="2"').Replace('</Results>', '<UnitTestResult outcome="NotExecuted" /></Results>') }
    }
    [System.IO.File]::WriteAllText($Path, $xml)
}

if ($Framework) {
    Add-Content -LiteralPath (Join-Path $ResultsDirectory 'invoked.txt') -Value $Framework
    New-TestReport (Join-Path $ResultsDirectory "check-$Framework.trx") $ReportKind
    if ($Framework -eq $FailureAt) { exit 7 }
    exit 0
}

. "$PSScriptRoot/../scripts/Test-Frameworks.ps1"
$testScript = $PSCommandPath
$pwsh = (Get-Process -Id $PID).Path
$temporary = Join-Path ([System.IO.Path]::GetTempPath()) ('harmony-ci-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $temporary
$checks = 0
try {
    foreach ($kind in @('valid', 'with-skipped', 'with-inconclusive', 'missing', 'malformed', 'empty', 'zero-tests', 'no-results', 'unfinished', 'failed', 'bad-count', 'missing-count', 'pending', 'aborted-after-pass', 'disconnected', 'false-passed-count', 'hidden-failure', 'missing-executed-result', 'missing-skipped-result', 'unaccounted-result', 'overstated-executed')) {
        $report = Join-Path $temporary "$kind.trx"
        New-TestReport $report $kind
        $expected = $kind -in @('valid', 'with-skipped', 'with-inconclusive')
        if ((Test-TrxSucceeded $report) -ne $expected) { throw "Wrong TRX verdict for $kind" }
        $checks++
    }

    # Real native exits must survive PowerShell scriptblock/function boundaries and later successful invocations.
    foreach ($failureAt in @('', 'first', 'middle', 'last')) {
        foreach ($checkReports in @($false, $true)) {
            $invoked = Join-Path $temporary 'invoked.txt'
            if (Test-Path -LiteralPath $invoked) { Remove-Item -LiteralPath $invoked }
            $parameters = @{ Frameworks = @('first', 'middle', 'last'); Operation = 'Regression'; ResultsDirectory = $temporary }
            if ($checkReports) { $parameters.ResultPrefix = 'check' }
            $failed = $false
            try {
                Invoke-FrameworkChecks @parameters -Action {
                    param($currentFramework)
                    & $pwsh -NoProfile -File $testScript -Framework $currentFramework -FailureAt $failureAt -ResultsDirectory $temporary
                    if ($LASTEXITCODE -ne 0) { throw "Native exit $LASTEXITCODE" }
                }
            } catch { $failed = $true }
            if ($failed -ne ($failureAt.Length -gt 0)) { throw "Wrong matrix verdict for failure '$failureAt', reports=$checkReports" }
            if (((Get-Content -LiteralPath $invoked) -join ',') -ne 'first,middle,last') { throw 'An earlier failure stopped later frameworks' }
            $checks++
        }
    }

    foreach ($kind in @('missing', 'malformed', 'empty', 'zero-tests')) {
        New-TestReport (Join-Path $temporary 'check-first.trx') # Stale success must not mask this run.
        $failed = $false
        try {
            Invoke-FrameworkChecks -Frameworks @('first') -ResultsDirectory $temporary -ResultPrefix 'check' -Action {
                param($currentFramework)
                & $pwsh -NoProfile -File $testScript -Framework $currentFramework -ReportKind $kind -ResultsDirectory $temporary
                if ($LASTEXITCODE -ne 0) { throw "Native exit $LASTEXITCODE" }
            }
        } catch { $failed = $true }
        if (-not $failed) { throw "Exit zero masked a $kind report" }
        $checks++
    }
    $failed = $false
    try { Invoke-FrameworkChecks -Frameworks @() -Action { throw 'Must not run' } } catch { $failed = $true }
    if (-not $failed) { throw 'An empty framework selection passed' }
    $checks++

    # Exercise the actual cleanup query, including experimental and unrelated artifact names.
    $workflow = Get-Content -LiteralPath "$PSScriptRoot/../workflows/test.yml" -Raw
    $query = [regex]::Match($workflow, "--jq '([^']+)'").Groups[1].Value
    if (-not $query) { throw 'Artifact cleanup query was not found' }
    $artifacts = Join-Path $temporary 'artifacts.json'
    [System.IO.File]::WriteAllText($artifacts, '{"artifacts":[{"id":1,"name":"build-output-ubuntu-Debug"},{"id":2,"name":"test-results-dotnet-windows-x86-Release"},{"id":3,"name":"experimental-test-results-dotnet-windows-x86-Release"},{"id":4,"name":"other-evidence"}]}')
    $previousSuccess = $env:TEST_RESULTS_SUCCEEDED
    try {
        foreach ($succeeded in @('false', 'true')) {
            $env:TEST_RESULTS_SUCCEEDED = $succeeded
            $selected = @(& jq -r $query $artifacts)
            if ($LASTEXITCODE -ne 0) { throw 'Artifact cleanup query failed' }
            $expected = if ($succeeded -eq 'true') { '1,2' } else { '1' }
            if (($selected -join ',') -ne $expected) { throw "Wrong artifact retention for succeeded=$succeeded" }
            $checks++
        }
    } finally { $env:TEST_RESULTS_SUCCEEDED = $previousSuccess }
    Write-Host "CI completeness regressions passed: $checks"
} finally {
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
