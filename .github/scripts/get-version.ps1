#!/usr/bin/env pwsh
# Get Version Script - Reads versions from centralized version file
# Usage: ./get-version.ps1 -Component "gabs_server" -OutputVariable "GABS_VERSION"

param(
    [Parameter(Mandatory=$true)]
    [string]$Component,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputVariable = "VERSION",
    
    [Parameter(Mandatory=$false)]
    [string]$VersionFile = ".github/versions.yml"
)

# Read the version file
if (-not (Test-Path $VersionFile)) {
    Write-Error "Version file not found: $VersionFile"
    exit 1
}

# Parse YAML (simple approach for this structure)
$content = Get-Content $VersionFile -Raw
$lines = $content -split "`n"

$currentSection = ""
$version = $null

foreach ($line in $lines) {
    $line = $line.Trim()
    
    # Skip comments and empty lines
    if ($line.StartsWith("#") -or $line -eq "") {
        continue
    }
    
    # Handle nested structure
    if ($line.EndsWith(":") -and -not $line.Contains(" ")) {
        $currentSection = $line.TrimEnd(":")
        continue
    }
    
    # Look for our component
    if ($line -match "^\s*$Component\s*:\s*`"(.+)`"$") {
        $version = $matches[1]
        break
    }
    
    # Handle nested components (e.g., actions.test_execute)
    $componentParts = $Component -split "\."
    if ($componentParts.Length -eq 2 -and $currentSection -eq $componentParts[0]) {
        if ($line -match "^\s*$($componentParts[1])\s*:\s*`"(.+)`"$") {
            $version = $matches[1]
            break
        }
    }
}

if ($null -eq $version) {
    Write-Error "Component '$Component' not found in version file"
    exit 1
}

Write-Host "Found version for '$Component': $version"

# Set GitHub Actions output if we're in a GitHub Actions environment
if ($env:GITHUB_OUTPUT) {
    "$OutputVariable=$version" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}

# Also set as environment variable
Set-Item -Path "env:$OutputVariable" -Value $version

Write-Output $version