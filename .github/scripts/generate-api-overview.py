#!/usr/bin/env python3
"""
Generate a dense, AI-optimized overview of the Harmony API.
This script extracts public API information from C# source files and creates
a condensed reference suitable for AI context windows.
"""

import os
import re
from pathlib import Path
from typing import Dict, List, Tuple, Optional
from dataclasses import dataclass
from datetime import datetime

@dataclass
class ApiMember:
    name: str
    type: str  # class, method, property, field, enum, etc.
    signature: str
    summary: str = ""
    parameters: List[Tuple[str, str, str]] = None  # (type, name, description)
    returns: str = ""
    category: str = ""

@dataclass
class ApiClass:
    name: str
    type: str  # class, interface, enum, struct
    namespace: str
    summary: str = ""
    members: List[ApiMember] = None
    category: str = ""

class HarmonyApiExtractor:
    def __init__(self):
        self.api_classes: List[ApiClass] = []
        self.current_file = ""
        
    def extract_xml_doc(self, lines: List[str], start_idx: int) -> Tuple[str, Dict[str, str]]:
        """Extract XML documentation comments preceding a declaration."""
        summary = ""
        params = {}
        returns = ""
        
        # Look backwards for XML doc comments
        i = start_idx - 1
        doc_lines = []
        
        while i >= 0 and (lines[i].strip().startswith("///") or lines[i].strip() == ""):
            if lines[i].strip().startswith("///"):
                doc_lines.insert(0, lines[i].strip()[3:].strip())
            i -= 1
        
        if not doc_lines:
            return "", {}
        
        # Parse XML documentation
        doc_text = " ".join(doc_lines)
        
        # Extract summary
        summary_match = re.search(r'<summary>(.*?)</summary>', doc_text, re.DOTALL)
        if summary_match:
            summary = summary_match.group(1).strip()
            summary = re.sub(r'<[^>]+>', '', summary)  # Remove XML tags
            summary = re.sub(r'\s+', ' ', summary)  # Normalize whitespace
        
        # Extract parameters
        param_matches = re.finditer(r'<param name="([^"]+)">(.*?)</param>', doc_text, re.DOTALL)
        for match in param_matches:
            param_name = match.group(1)
            param_desc = re.sub(r'<[^>]+>', '', match.group(2)).strip()
            param_desc = re.sub(r'\s+', ' ', param_desc)
            params[param_name] = param_desc
        
        # Extract returns
        returns_match = re.search(r'<returns>(.*?)</returns>', doc_text, re.DOTALL)
        if returns_match:
            returns = re.sub(r'<[^>]+>', '', returns_match.group(1)).strip()
            returns = re.sub(r'\s+', ' ', returns)
        
        return summary, {"params": params, "returns": returns}
    
    def categorize_class(self, class_name: str, file_path: str) -> str:
        """Categorize a class based on its name and location."""
        name_lower = class_name.lower()
        path_str = str(file_path).lower()
        
        if "attributes" in path_str or name_lower.startswith("harmony") and ("patch" in name_lower or "prefix" in name_lower or "postfix" in name_lower or "transpiler" in name_lower or "finalizer" in name_lower):
            return "Attributes"
        elif name_lower in ["harmony"]:
            return "Core"
        elif "patch" in name_lower or name_lower in ["patches", "patchinfo", "patchprocessor", "patchclassprocessor"]:
            return "Patching"
        elif "code" in name_lower or "transpiler" in name_lower or name_lower in ["transpilers"]:
            return "Transpiling"
        elif "access" in name_lower or "traverse" in name_lower or name_lower in ["filelog", "fastaccess", "generalextensions", "symbolextensions"]:
            return "Utilities"
        elif "exception" in name_lower or "error" in name_lower:
            return "Exceptions"
        else:
            return "Other"
    
    def extract_method_signature(self, line: str) -> str:
        """Extract a clean method signature from a declaration line."""
        # Remove access modifiers and focus on the essential signature
        line = re.sub(r'\s*(public|private|protected|internal|static|virtual|override|abstract|sealed)\s+', ' ', line)
        line = re.sub(r'^\s+', '', line)  # Remove leading whitespace
        line = re.sub(r'\s+', ' ', line)  # Normalize whitespace
        return line.strip()
    
    def parse_csharp_file(self, file_path: Path):
        """Parse a C# file and extract public API information."""
        self.current_file = str(file_path)
        
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                lines = f.readlines()
        except Exception as e:
            print(f"Error reading {file_path}: {e}")
            return
        
        namespace = "HarmonyLib"  # Default namespace
        current_class = None
        brace_level = 0
        in_class = False
        
        for i, line in enumerate(lines):
            stripped = line.strip()
            
            # Track brace levels
            brace_level += stripped.count('{') - stripped.count('}')
            
            # Extract namespace
            namespace_match = re.match(r'namespace\s+([\w\.]+)', stripped)
            if namespace_match:
                namespace = namespace_match.group(1)
                continue
            
            # Extract public class/interface/enum/struct declarations
            class_match = re.match(r'public\s+(class|interface|enum|struct)\s+(\w+)', stripped)
            if class_match and not in_class:
                class_type = class_match.group(1)
                class_name = class_match.group(2)
                
                summary, _ = self.extract_xml_doc(lines, i)
                category = self.categorize_class(class_name, file_path)
                
                current_class = ApiClass(
                    name=class_name,
                    type=class_type,
                    namespace=namespace,
                    summary=summary,
                    members=[],
                    category=category
                )
                self.api_classes.append(current_class)
                in_class = True
                continue
            
            # Extract public members within a class
            if in_class and current_class and brace_level > 0:
                # Public method
                method_match = re.match(r'public\s+.*?(\w+)\s*\([^)]*\)', stripped)
                if method_match and not re.match(r'public\s+(class|interface|enum|struct)', stripped):
                    method_name = method_match.group(1)
                    if method_name != current_class.name:  # Not a constructor
                        summary, doc_info = self.extract_xml_doc(lines, i)
                        signature = self.extract_method_signature(stripped)
                        
                        member = ApiMember(
                            name=method_name,
                            type="method",
                            signature=signature,
                            summary=summary,
                            returns=doc_info.get("returns", ""),
                            category=current_class.category
                        )
                        current_class.members.append(member)
                        continue
                
                # Public property
                prop_match = re.match(r'public\s+.*?\s+(\w+)\s*\{', stripped)
                if prop_match:
                    prop_name = prop_match.group(1)
                    summary, _ = self.extract_xml_doc(lines, i)
                    signature = self.extract_method_signature(stripped.replace('{', ''))
                    
                    member = ApiMember(
                        name=prop_name,
                        type="property",
                        signature=signature,
                        summary=summary,
                        category=current_class.category
                    )
                    current_class.members.append(member)
                    continue
                
                # Public field
                field_match = re.match(r'public\s+.*?\s+(\w+);', stripped)
                if field_match:
                    field_name = field_match.group(1)
                    summary, _ = self.extract_xml_doc(lines, i)
                    signature = self.extract_method_signature(stripped)
                    
                    member = ApiMember(
                        name=field_name,
                        type="field",
                        signature=signature,
                        summary=summary,
                        category=current_class.category
                    )
                    current_class.members.append(member)
                    continue
            
            # Reset when exiting class
            if in_class and brace_level == 0:
                in_class = False
                current_class = None

    def extract_from_directory(self, directory: Path):
        """Extract API information from all C# files in a directory."""
        if not directory.exists():
            return
        
        for cs_file in directory.glob("**/*.cs"):
            self.parse_csharp_file(cs_file)

    def generate_overview(self) -> str:
        """Generate the AI-optimized API overview."""
        overview = f"""# Harmony API Overview for AI

> **Generated on:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S UTC')}
> **Purpose:** Dense API reference optimized for AI context windows
> **Source:** Harmony {self.get_version()} - https://github.com/pardeike/Harmony

## Quick Reference

Harmony is a .NET library for runtime patching of methods. Key concepts:
- **Harmony Instance**: Entry point for all patching operations
- **Patches**: Prefix, Postfix, Transpiler, and Finalizer modifications
- **Attributes**: Declarative patch definitions using C# attributes
- **AccessTools**: Reflection utilities for accessing private members
- **Traverse**: Safe reflection wrapper for reading/writing private data
- **CodeMatcher**: IL manipulation for transpilers

## Core Categories

"""
        
        # Group classes by category
        categories = {}
        for cls in self.api_classes:
            cat = cls.category
            if cat not in categories:
                categories[cat] = []
            categories[cat].append(cls)
        
        # Sort categories by importance
        category_order = ["Core", "Patching", "Attributes", "Transpiling", "Utilities", "Exceptions", "Other"]
        
        for category in category_order:
            if category not in categories:
                continue
            
            overview += f"### {category}\n\n"
            
            for cls in sorted(categories[category], key=lambda x: x.name):
                overview += f"#### {cls.name}\n"
                if cls.summary:
                    overview += f"*{cls.summary}*\n\n"
                
                # Group members by type
                methods = [m for m in (cls.members or []) if m.type == "method"]
                properties = [m for m in (cls.members or []) if m.type == "property"]
                fields = [m for m in (cls.members or []) if m.type == "field"]
                
                if methods:
                    # Filter out less important methods (getters, setters, ToString, etc.)
                    important_methods = [m for m in methods if not (
                        m.name.lower() in ["tostring", "equals", "gethashcode", "gettype"] or
                        m.signature.startswith("string ToString") or
                        m.signature.startswith("bool Equals") or
                        m.signature.startswith("int GetHashCode")
                    )]
                    
                    overview += "**Key Methods:**\n"
                    for method in important_methods[:8]:  # Show fewer but more important methods
                        overview += f"- `{method.signature}`"
                        if method.summary:
                            overview += f" - {method.summary}"
                        overview += "\n"
                    if len(important_methods) > 8:
                        overview += f"- *...and {len(important_methods) - 8} more methods*\n"
                    overview += "\n"
                
                if properties:
                    overview += "**Properties:**\n"
                    for prop in properties:
                        overview += f"- `{prop.signature}`"
                        if prop.summary:
                            overview += f" - {prop.summary}"
                        overview += "\n"
                    overview += "\n"
                
                if fields:
                    overview += "**Fields:**\n"
                    for field in fields:
                        overview += f"- `{field.signature}`"
                        if field.summary:
                            overview += f" - {field.summary}"
                        overview += "\n"
                    overview += "\n"
        
        # Add usage patterns and FAQ section
        overview += """## Common Usage Patterns

### Basic Patching
```csharp
var harmony = new Harmony("com.example.mod");
harmony.PatchAll();  // Apply all [HarmonyPatch] attributes in assembly
```

### Manual Patching
```csharp
var original = typeof(TargetClass).GetMethod("TargetMethod");
var prefix = typeof(PatchClass).GetMethod("PrefixMethod");
harmony.Patch(original, new HarmonyMethod(prefix));
```

### Attribute-Based Patching
```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
class PatchClass {
    static bool Prefix() => true;  // Allow original to run
    static void Postfix() { }      // Run after original
}
```

### Accessing Private Members
```csharp
var traverse = Traverse.Create(instance);
var privateField = traverse.Field("privateFieldName").GetValue<int>();
traverse.Method("privateMethod", args).GetValue();
```

### Transpilers (IL Manipulation)
```csharp
[HarmonyTranspiler]
static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
    var codes = new CodeMatcher(instructions);
    return codes.MatchForward(/* pattern */).SetAndAdvance(/* replacement */).InstructionEnumeration();
}
```

## Patch Types Quick Reference
- **Prefix**: Runs before original method, can skip original if returns false
- **Postfix**: Runs after original method, can access and modify results
- **Transpiler**: Modifies IL code of original method for advanced patching
- **Finalizer**: Runs after original method regardless of exceptions (like finally)
- **Reverse Patch**: Copy and modify original method logic into your own method

## AI Assistant Guidelines
When helping with Harmony:
1. Always create unique Harmony IDs (e.g., "com.yourname.yourmod")
2. Use try-catch around Harmony operations in production code
3. Prefer attribute-based patches for maintainability
4. Use AccessTools for reflection instead of raw Reflection API
5. Test patches thoroughly - wrong patches can crash applications
6. Use HarmonyDebug attribute for debugging specific patches

## Documentation Links
- Full Documentation: https://harmony.pardeike.net
- GitHub Repository: https://github.com/pardeike/Harmony
- API Reference: https://harmony.pardeike.net/api/

"""
        return overview
    
    def get_version(self) -> str:
        """Extract version from Directory.Build.props if available."""
        try:
            props_file = Path(__file__).parent.parent.parent / "Directory.Build.props"
            if props_file.exists():
                content = props_file.read_text()
                version_match = re.search(r'<HarmonyVersion>(.*?)</HarmonyVersion>', content)
                if version_match:
                    return version_match.group(1)
        except:
            pass
        return "2.x"

def main():
    """Main entry point."""
    # Get the repository root
    repo_root = Path(__file__).parent.parent.parent
    
    # Initialize extractor
    extractor = HarmonyApiExtractor()
    
    # Extract from main API directories
    print("Extracting API information...")
    extractor.extract_from_directory(repo_root / "Harmony" / "Public")
    extractor.extract_from_directory(repo_root / "Harmony" / "Tools")
    
    # Generate overview
    print("Generating API overview...")
    overview = extractor.generate_overview()
    
    # Write to file
    output_file = repo_root / "API_OVERVIEW.md"
    output_file.write_text(overview, encoding='utf-8')
    
    print(f"API overview generated: {output_file}")
    print(f"Extracted {len(extractor.api_classes)} classes")

if __name__ == "__main__":
    main()