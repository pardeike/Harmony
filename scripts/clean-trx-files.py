#!/usr/bin/env python3
"""
Script to clean TRX files by sanitizing XML content and removing extra content.
This fixes XML parsing errors caused by invalid characters or extra content in TRX files.
"""

import os
import glob
import sys
import re
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
    """Clean a single TRX file by sanitizing content and removing extra content after </TestRun>"""
    try:
        with open(file_path, 'r', encoding='utf-8', errors='replace') as f:
            content = f.read()
        
        original_content = content
        modified = False
        
        # First, sanitize XML content
        content = sanitize_xml_content(content)
        if content != original_content:
            modified = True
            print(f"Sanitized XML content in {file_path}")
        
        # Find the closing TestRun tag
        end_tag = '</TestRun>'
        end_index = content.find(end_tag)
        
        if end_index == -1:
            print(f"Warning: {file_path} does not contain {end_tag} tag")
            return False
        
        # Check if there's extra content after the closing tag (whitespace is OK)
        expected_end = end_index + len(end_tag)
        remaining_content = content[expected_end:].strip()
        
        if remaining_content:
            # Trim everything after the closing tag
            content = content[:expected_end]
            modified = True
            print(f"Removed extra content after {end_tag} in {file_path}")
        
        # Write the cleaned content back if modified
        if modified:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
            return True
        else:
            print(f"No cleaning needed for {file_path}")
            return False
            
    except Exception as e:
        print(f"Error cleaning {file_path}: {e}")
        return False

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
