#!/usr/bin/env python3
"""
Script to clean TRX files by sanitizing XML content and removing extra content.
This fixes XML parsing errors caused by invalid characters or extra content in TRX files.
"""

import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
import xml.sax.saxutils

def sanitize_xml_content(content):
    """Sanitize content to be XML-safe"""
    # Escape XML special characters in text content
    # Note: We need to be careful not to escape content that's already within XML tags
    
    # First, let's find and sanitize content within <StdOut> tags
    stdout_pattern = r'(<StdOut>)(.*?)(</StdOut>)'
    
    def sanitize_stdout_content(match):
        start_tag = match.group(1)
        stdout_content = match.group(2)
        end_tag = match.group(3)
        
        # Remove or replace characters that might cause XML parsing issues
        # Remove control characters except for tab, newline, and carriage return
        stdout_content = re.sub(r'[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]', '', stdout_content)
        
        # Escape XML special characters in the stdout content
        stdout_content = xml.sax.saxutils.escape(stdout_content, {'"': '&quot;', "'": '&apos;'})
        
        return start_tag + stdout_content + end_tag
    
    # Apply sanitization to StdOut sections
    content = re.sub(stdout_pattern, sanitize_stdout_content, content, flags=re.DOTALL)
    
    return content

def clean_trx_file(file_path):
    """Clean a single TRX file and remove it if it cannot be fixed."""
    try:
        with open(file_path, 'rb') as f:
            raw = f.read()
        try:
            content = raw.decode('utf-8')
        except UnicodeDecodeError:
            content = raw.decode('utf-16')

        original_content = content
        modified = False

        # Sanitize XML content
        content = sanitize_xml_content(content)
        if content != original_content:
            modified = True
            print(f"Sanitized XML content in {file_path}")

        # Remove anything after the last </TestRun>
        end_tag = '</TestRun>'
        end_index = content.rfind(end_tag)
        if end_index == -1:
            print(f"Warning: {file_path} missing {end_tag}; removing file")
            os.remove(file_path)
            return True

        expected_end = end_index + len(end_tag)
        if content[expected_end:].strip():
            content = content[:expected_end]
            modified = True
            print(f"Trimmed content after {end_tag} in {file_path}")

        # Validate XML; delete file if still invalid
        try:
            ET.fromstring(content)
        except ET.ParseError as e:
            print(f"Invalid XML in {file_path}: {e}; removing file")
            os.remove(file_path)
            return True

        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            return True

        print(f"No cleaning needed for {file_path}")
        return False

    except Exception as e:
        print(f"Error cleaning {file_path}: {e}; removing file")
        try:
            os.remove(file_path)
        except Exception:
            pass
        return True

def main():
    """Clean all TRX files found recursively"""
    cleaned_count = 0
    total_count = 0
    
    # Find all TRX files recursively
    trx_files = glob.glob('**/*.trx', recursive=True)
    
    if not trx_files:
        print("No TRX files found")
        return 0
    
    print(f"Found {len(trx_files)} TRX files to check")
    
    for trx_file in trx_files:
        total_count += 1
        if clean_trx_file(trx_file):
            cleaned_count += 1
    
    print(f"Processed {total_count} TRX files, cleaned {cleaned_count}")
    return 0

if __name__ == '__main__':
    sys.exit(main())