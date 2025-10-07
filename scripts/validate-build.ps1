#!/usr/bin/env pwsh
# Script to validate Harmony build outputs for metadata integrity
# This checks for "coded rid out of range" errors that can cause issues with Unity Burst compiler

param(
	[Parameter(Mandatory=$false)]
	[string]$BuildPath = "Lib.Harmony/bin/Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Harmony Build Metadata Validator" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Build the validator if needed
$validatorPath = "tools/MetadataValidator/bin/Release/net8.0/MetadataValidator.dll"
if (-not (Test-Path $validatorPath)) {
	Write-Host "Building MetadataValidator..." -ForegroundColor Yellow
	dotnet build tools/MetadataValidator/MetadataValidator.csproj -c Release --verbosity quiet
	if ($LASTEXITCODE -ne 0) {
		Write-Error "Failed to build MetadataValidator"
		exit 1
	}
}

# Find all 0Harmony.dll files
$harmonyDlls = Get-ChildItem -Path $BuildPath -Filter "0Harmony.dll" -Recurse -ErrorAction SilentlyContinue

if ($harmonyDlls.Count -eq 0) {
	Write-Host "No 0Harmony.dll files found in $BuildPath" -ForegroundColor Yellow
	Write-Host "Skipping validation (no builds to validate)" -ForegroundColor Yellow
	exit 0
}

Write-Host "Found $($harmonyDlls.Count) 0Harmony.dll file(s) to validate" -ForegroundColor Green
Write-Host ""

$failedValidations = @()
$succeededValidations = @()

foreach ($dll in $harmonyDlls) {
	$relativePath = $dll.FullName.Replace((Get-Location).Path, "").TrimStart("/\")
	Write-Host "Validating: $relativePath" -ForegroundColor Cyan
	
	$result = & dotnet $validatorPath $dll.FullName
	$exitCode = $LASTEXITCODE
	
	if ($exitCode -eq 0) {
		Write-Host "  ✓ PASSED" -ForegroundColor Green
		$succeededValidations += $relativePath
	} else {
		Write-Host "  ✗ FAILED" -ForegroundColor Red
		$failedValidations += $relativePath
		Write-Host "  Output:" -ForegroundColor Red
		$result | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
	}
	Write-Host ""
}

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Validation Summary" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Passed: $($succeededValidations.Count)" -ForegroundColor Green
Write-Host "Failed: $($failedValidations.Count)" -ForegroundColor Red
Write-Host ""

if ($failedValidations.Count -gt 0) {
	Write-Host "Failed validations:" -ForegroundColor Red
	$failedValidations | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
	Write-Host ""
	Write-Host "These builds have metadata corruption that will cause issues with Unity Burst compiler." -ForegroundColor Red
	Write-Host "For Unity/Burst projects, use Lib.Harmony.Thin instead." -ForegroundColor Yellow
	exit 1
}

Write-Host "All validations passed! ✓" -ForegroundColor Green
exit 0
