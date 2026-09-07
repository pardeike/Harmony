function Test-TrxSucceeded([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load((Resolve-Path -LiteralPath $Path).Path)
        $summaries = $document.SelectNodes('/*[local-name()="TestRun"]/*[local-name()="ResultSummary"]')
        if ($summaries.Count -ne 1 -or $summaries[0].GetAttribute('outcome') -ne 'Completed') { return $false }
        $counters = $summaries[0].SelectNodes('*[local-name()="Counters"]')
        if ($counters.Count -ne 1) { return $false }
        $counts = @{}
        foreach ($name in @('total', 'executed', 'passed', 'failed', 'error', 'timeout', 'aborted', 'passedButRunAborted', 'notRunnable', 'disconnected', 'inProgress', 'pending')) {
            $value = 0L
            if (-not [long]::TryParse($counters[0].GetAttribute($name), [ref]$value) -or $value -lt 0) { return $false }
            $counts[$name] = $value
        }
        if ($counts.executed -le 0 -or $counts.passed -le 0 -or $counts.executed -gt $counts.total -or $counts.passed -gt $counts.executed) { return $false }
        foreach ($name in @('failed', 'error', 'timeout', 'aborted', 'passedButRunAborted', 'notRunnable', 'disconnected', 'inProgress', 'pending')) {
            if ($counts[$name] -ne 0) { return $false }
        }
        $results = $document.SelectNodes('/*[local-name()="TestRun"]/*[local-name()="Results"]//*[local-name()="UnitTestResult"]')
        if ($results.Count -eq 0) { return $false }
        $passed = @($results | Where-Object { $_.GetAttribute('outcome') -eq 'Passed' }).Count
        $inconclusive = @($results | Where-Object { $_.GetAttribute('outcome') -eq 'Inconclusive' }).Count
        $unexpected = @($results | Where-Object { $_.GetAttribute('outcome') -notin @('Passed', 'NotExecuted', 'Inconclusive') }).Count
        return $results.Count -eq $counts.total -and $passed -eq $counts.passed `
            -and ($passed + $inconclusive) -eq $counts.executed -and $unexpected -eq 0
    } catch {
        return $false
    }
}

# Every framework runs even after an earlier failure. Test invocations must also produce their own completed report.
function Invoke-FrameworkChecks(
    [string[]]$Frameworks,
    [scriptblock]$Action,
    [string]$Operation = 'Tests',
    [string]$ResultPrefix,
    [string]$ResultsDirectory = 'TestResults'
) {
    if ($Frameworks.Count -eq 0) { throw "$Operation selected no target frameworks" }
    $failures = @()
    foreach ($framework in $Frameworks) {
        try {
            $report = if ($ResultPrefix) { Join-Path $ResultsDirectory "$ResultPrefix-$framework.trx" }
            # A previous attempt must not supply the success report for this invocation.
            if ($report -and (Test-Path -LiteralPath $report)) { Remove-Item -LiteralPath $report }
            & $Action $framework
            if ($report -and -not (Test-TrxSucceeded $report)) {
                throw "Missing, incomplete or unsuccessful test results: $report"
            }
        } catch {
            $failures += $framework
            Write-Warning "$Operation failed for ${framework}: $_"
        }
    }
    if ($failures.Count -gt 0) { throw "$Operation failed for: $($failures -join ', ')" }
}
