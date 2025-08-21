# TRX File Cleanup Script

This script fixes XML parsing errors in TRX test result files caused by problematic content in the `<StdOut>` sections or extra content after the closing `</TestRun>` tag.

## Problem

The Platform Tests were generating TRX files that caused XML parsing errors:
```
Error processing result file: Extra content at the end of the document, line 1684, column 11
lxml.etree.XMLSyntaxError: Extra content at the end of the document, line 1687, column 11
```

## Root Cause

1. **XML Character Escaping**: NUnit console output within `<StdOut>` sections contained special characters (like single quotes) that weren't properly escaped for XML
2. **Extra Content**: Potential for content to be appended after the `</TestRun>` closing tag

## Solution

The `scripts/clean-trx-files.py` script:

1. **Sanitizes XML content** by properly escaping special characters in `<StdOut>` sections
2. **Removes extra content** after the `</TestRun>` closing tag
3. **Preserves XML validity** while cleaning problematic content

## Integration

The script is automatically run before test result upload in `.github/actions/test-upload-result/action.yml`:

```yaml
- name: Clean TRX files
  run: python3 scripts/clean-trx-files.py
  shell: bash
  if: ${{success() || failure()}}
```

## Usage

To run manually:
```bash
python3 scripts/clean-trx-files.py
```

The script will find all `*.trx` files recursively and clean them as needed.