# Centralized Version Management

This repository implements centralized version management to avoid hardcoded version numbers throughout the codebase. This provides a single point of control for all version updates.

## Files Overview

### 1. `.github/versions.yml`
The main version configuration file containing all version numbers for:
- GABS (GitHub Actions Build Server) components
- GitHub Actions versions  
- Third-party dependencies
- Build tools

### 2. `Directory.Build.props`
Contains version information for .NET projects, including:
- Harmony library versions
- MonoMod version
- GABS Server version (for .NET integration)

### 3. `.github/actions/get-version/`
A reusable GitHub Action that reads versions from the centralized file.

### 4. `.github/scripts/get-version.ps1`
PowerShell script that parses the version file and extracts specific version numbers.

## Usage Examples

### In GitHub Actions Workflows

```yaml
- name: Get GABS Server Version
  id: gabs-version
  uses: ./.github/actions/get-version
  with:
    component: gabs_server
    output-variable: gabs_version

- name: Use the version
  run: |
    echo "GABS Server Version: ${{ steps.gabs-version.outputs.version }}"
```

### In PowerShell Scripts

```powershell
# Get GABS server version
$gabsVersion = & ./.github/scripts/get-version.ps1 -Component "gabs_server"

# Get nested version (e.g., actions.test_execute)
$testVersion = & ./.github/scripts/get-version.ps1 -Component "actions.test_execute"
```

### In MSBuild/C# Projects

The GABS server version is also available in `Directory.Build.props` as `$(GABSServerVersion)`:

```xml
<PropertyGroup>
  <GABSVersion>$(GABSServerVersion)</GABSVersion>
</PropertyGroup>
```

## Adding New Versions

To add a new component version:

1. Edit `.github/versions.yml`
2. Add your component under the appropriate section
3. Update any references to use the centralized version

### Example Addition

```yaml
versions:
  gabs_server: "0.2.0"  # Updated version
  
  # New component
  api_gateway: "1.0.0"
```

## Updating Versions

To update a version:

1. **Single Point Update**: Change the version in `.github/versions.yml`
2. **Propagation**: The change automatically applies to all workflows and scripts that use the centralized version system
3. **No Hardcoded References**: Eliminates the need to find and update multiple hardcoded version strings

## Benefits

- **Single Source of Truth**: All versions defined in one place
- **Easy Updates**: Change version once, applies everywhere
- **Consistency**: Prevents version mismatches across different components
- **Maintainability**: Reduces risk of forgotten version updates
- **Automation Friendly**: Easily integrated with CI/CD workflows

## Migration from Hardcoded Versions

When migrating from hardcoded versions:

1. Identify all hardcoded version references (e.g., "0.1.0")
2. Add the version to `.github/versions.yml`
3. Replace hardcoded references with calls to the version management system
4. Test to ensure all components still work correctly

## Example Migration

**Before:**
```yaml
# Hardcoded version
image: gabsserver:0.1.0
```

**After:**
```yaml
# Using centralized version
- name: Get GABS Version
  id: version
  uses: ./.github/actions/get-version
  with:
    component: gabs_server

- name: Deploy
  run: |
    docker pull gabsserver:${{ steps.version.outputs.version }}
```

This approach ensures that when you need to release version 0.2.0, you only need to update one file instead of searching through the entire codebase for hardcoded references.