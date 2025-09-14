#!/usr/bin/env pwsh
# Bump Version Script - Updates version in centralized version file
# Usage: ./bump-version.ps1 -Component "gabs_server" -NewVersion "0.2.0"

param(
    [Parameter(Mandatory=$true)]
    [string]$Component,
    
    [Parameter(Mandatory=$true)]
    [string]$NewVersion,
    
    [Parameter(Mandatory=$false)]
    [string]$VersionFile = ".github/versions.yml",
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

# Validate version format (basic semantic versioning)
if ($NewVersion -notmatch '^\d+\.\d+\.\d+(-.*)?$') {
    Write-Error "Invalid version format. Expected format: X.Y.Z or X.Y.Z-suffix"
    exit 1
}

# Read the version file
if (-not (Test-Path $VersionFile)) {
    Write-Error "Version file not found: $VersionFile"
    exit 1
}

$content = Get-Content $VersionFile
$updated = $false
$newContent = @()

$currentSection = ""

foreach ($line in $content) {
    $originalLine = $line
    $trimmedLine = $line.Trim()
    
    # Handle nested structure
    if ($trimmedLine.EndsWith(":") -and -not $trimmedLine.Contains(" ")) {
        $currentSection = $trimmedLine.TrimEnd(":")
        $newContent += $originalLine
        continue
    }
    
    # Look for our component
    if ($trimmedLine -match "^\s*$Component\s*:\s*`"(.+)`"$") {
        $oldVersion = $matches[1]
        $newLine = $line -replace "`"$oldVersion`"", "`"$NewVersion`""
        $newContent += $newLine
        $updated = $true
        Write-Host "Updated ${Component}: ${oldVersion} -> ${NewVersion}"
        continue
    }
    
    # Handle nested components (e.g., actions.test_execute)
    $componentParts = $Component -split "\."
    if ($componentParts.Length -eq 2 -and $currentSection -eq $componentParts[0]) {
        if ($trimmedLine -match "^\s*$($componentParts[1])\s*:\s*`"(.+)`"$") {
            $oldVersion = $matches[1]
            $newLine = $line -replace "`"$oldVersion`"", "`"$NewVersion`""
            $newContent += $newLine
            $updated = $true
            Write-Host "Updated ${Component}: ${oldVersion} -> ${NewVersion}"
            continue
        }
    }
    
    $newContent += $originalLine
}

if (-not $updated) {
    Write-Error "Component '$Component' not found in version file"
    exit 1
}

if ($DryRun) {
    Write-Host "`nDry run - changes would be:"
    Write-Host "=========================="
    $newContent | Write-Host
} else {
    # Write the updated content back to the file
    $newContent | Set-Content $VersionFile -Encoding UTF8
    Write-Host "Successfully updated version file: $VersionFile"
    
    # Also update Directory.Build.props if it's the GABS server version
    if ($Component -eq "gabs_server") {
        $propsFile = "Directory.Build.props"
        if (Test-Path $propsFile) {
            $propsContent = Get-Content $propsFile -Raw
            $updatedProps = $propsContent -replace '<GABSServerVersion>.*</GABSServerVersion>', "<GABSServerVersion>$NewVersion</GABSServerVersion>"
            Set-Content $propsFile -Value $updatedProps -Encoding UTF8
            Write-Host "Also updated $propsFile with new GABS server version"
        }
    }
}