# Scripts Directory

This directory contains utility scripts for building, testing, and maintaining the Harmony project.

## Scripts Overview

### `build-with-local-monomod.sh`
Build script that cleans, restores, and builds the project with local MonoMod configuration.

**Usage:**
```bash
./scripts/build-with-local-monomod.sh
```


### `pack.ps1`
PowerShell script for packaging and creating release builds of the Harmony library across multiple configurations.

**Usage:**
```powershell
.\scripts\pack.ps1
```

### `test.sh`
Simple test runner script that executes the project's test suite with optimized settings.

**Usage:**
```bash
./scripts/test.sh
```

## Requirements

- **Bash scripts**: Require bash shell (Linux/macOS/WSL)
- **PowerShell scripts**: Require PowerShell (Windows/PowerShell Core)
- **All scripts**: Require .NET SDK and should be run from the project root directory