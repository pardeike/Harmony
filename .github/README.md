# GitHub Actions and Infrastructure

This directory contains GitHub Actions workflows, custom actions, and infrastructure configuration for the Harmony project.

## Centralized Version Management

This repository implements centralized version management to eliminate hardcoded version numbers. All versions are managed from a single source of truth.

### Quick Start

1. **Get a version in a workflow:**
   ```yaml
   - name: Get GABS Server Version
     id: version
     uses: ./.github/actions/get-version
     with:
       component: gabs_server
   
   - name: Use the version
     run: echo "Version: ${{ steps.version.outputs.version }}"
   ```

2. **Update a version:**
   ```bash
   pwsh ./.github/scripts/bump-version.ps1 -Component "gabs_server" -NewVersion "0.2.0"
   ```

3. **Get version in PowerShell:**
   ```powershell
   $version = & ./.github/scripts/get-version.ps1 -Component "gabs_server"
   ```

### Available Components

- `gabs_server` - GABS (GitHub Actions Build Server) version
- `actions.test_execute` - Test execution action version
- `actions.test_upload_result` - Test result upload action version
- `dependencies.checkout` - Checkout action version
- `tools.dotnet_version` - .NET SDK version

### Files

- `versions.yml` - Central version configuration
- `actions/get-version/` - Reusable action to get versions
- `scripts/get-version.ps1` - PowerShell script to read versions
- `scripts/bump-version.ps1` - PowerShell script to update versions
- `VERSION_MANAGEMENT.md` - Detailed documentation

### Migration from Hardcoded Versions

When you need to replace hardcoded version references:

1. Add the version to `versions.yml`
2. Replace hardcoded usage with the centralized version system
3. Use the provided scripts and actions to access versions

This ensures that version updates only require changes in one place, making releases much easier and reducing the risk of version mismatches.