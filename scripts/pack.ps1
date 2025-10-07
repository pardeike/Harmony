# Ensure the script is executed in the root of the Harmony project
if (-not (Test-Path .git) -or -not (Test-Path Harmony.sln)) {
    Write-Host "This script must be run from the root of the Harmony project."
    exit
}

# Clear out old pack results from Harmony\bin and clean the project once for all configurations
Write-Host "Cleaning Lib.Harmony\bin directory and project..."
if (Test-Path Lib.Harmony\bin) {
    Remove-Item Lib.Harmony\bin -Recurse
}
New-Item -ItemType Directory -Path Lib.Harmony\bin | Out-Null

Write-Host "Cleaning Lib.Harmony.Ref\bin directory and project..."
if (Test-Path Lib.Harmony.Ref\bin) {
    Remove-Item Lib.Harmony.Ref\bin -Recurse
}
New-Item -ItemType Directory -Path Lib.Harmony.Ref\bin | Out-Null

Write-Host "Cleaning Lib.Harmony.Thin\bin directory and project..."
if (Test-Path Lib.Harmony.Thin\bin) {
    Remove-Item Lib.Harmony.Thin\bin -Recurse
}
New-Item -ItemType Directory -Path Lib.Harmony.Thin\bin | Out-Null

# Clean the project
dotnet clean --nologo --verbosity minimal

# Build Solution 
dotnet build --nologo --configuration Release --verbosity minimal
dotnet pack --nologo --configuration Release --verbosity minimal --no-restore --no-build

# Validate build outputs for metadata integrity
Write-Host "`nValidating build outputs for Unity Burst compatibility..." -ForegroundColor Cyan
& "$PSScriptRoot/validate-build.ps1" -BuildPath "Lib.Harmony/bin/Release"
if ($LASTEXITCODE -ne 0) {
	Write-Host "`nWARNING: Build validation detected metadata issues that may cause problems with Unity Burst compiler." -ForegroundColor Yellow
	Write-Host "The fat builds (Lib.Harmony) are known to have metadata compatibility issues with Unity's Burst compiler." -ForegroundColor Yellow
	Write-Host "For Unity/Burst projects, use Lib.Harmony.Thin instead, which ships dependencies separately." -ForegroundColor Yellow
	Write-Host "See: https://github.com/pardeike/Harmony/issues for more information.`n" -ForegroundColor Yellow
}
