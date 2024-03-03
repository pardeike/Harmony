# Ensure the script is executed in the root of the Harmony project
if (-not (Test-Path .git) -or -not (Test-Path Harmony.sln)) {
    Write-Host "This script must be run from the root of the Harmony project."
    exit
}

# Clear out old pack results from Harmony\bin and clean the project once for all configurations
Write-Host "Cleaning Harmony\bin directory and project..."
if (Test-Path Harmony\bin) {
    Remove-Item Harmony\bin -Recurse
}
New-Item -ItemType Directory -Path Harmony\bin | Out-Null

# Clean the project
dotnet clean --nologo --verbosity minimal

# Define configurations
$configurations = @('ReleaseFat', 'ReleaseThin', 'DebugFat', 'DebugThin')

# Loop through each configuration for building and packing
foreach ($config in $configurations) {
    Write-Host "Processing configuration: $config"
    # Building the project for the specific configuration
    dotnet build --nologo --configuration $config --verbosity minimal
    dotnet pack --nologo --configuration $config --verbosity minimal
}
