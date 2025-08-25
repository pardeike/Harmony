# TODO: Harmony Bugs and Issues to Fix

This document contains a comprehensive list of bugs, limitations, and issues found in the Harmony codebase that need to be addressed.

## Critical Bugs

### 1. Nullable Result Handling Crash (High Priority)
**Location:** `HarmonyTests/Patching/Arguments.cs:513`
**Issue:** Test crashes with NullReferenceException in some configurations
**Details:** 
- Test: `Test_NullableResults()` marked as `[Explicit]` due to crashes
- Related to nullable result types handling in patches
- Discord reference: https://discord.com/channels/131466550938042369/674571535570305060/1319451813975687269
**Impact:** Patching methods that return nullable types may fail unpredictably

### 2. StackTrace.GetFrame() Reliability Issues (High Priority)
**Location:** `Harmony/Public/Harmony.cs:78, 111, 130, 231`
**Issue:** StackTrace.GetFrame(1) can be unreliable when methods are inlined
**Details:**
- Used in `PatchAll()`, `PatchAllUncategorized()`, `PatchCategory()`, `UnpatchCategory()`
- Can point to wrong method/assembly when the calling method is inlined
- Documentation already warns about this issue
**Impact:** Automatic assembly detection fails, leading to patches not being applied
**Suggested Fix:** Always recommend explicit assembly parameter versions

### 3. Missing Null Checks in Reflection Code
**Location:** `Harmony/Internal/HarmonySharedState.cs:57, 63, 68, 73`
**Issue:** GetField() calls don't handle null return values safely
**Details:**
- `type.GetField("version")` and similar calls could return null
- Would cause NullReferenceException when calling GetValue() or SetValue()
**Impact:** Runtime crashes when field reflection fails

## Threading and Concurrency Issues

### 4. Static Mutable Collections Without Proper Protection
**Location:** `Harmony/Internal/HarmonySharedState.cs:39-41`
**Issue:** Static dictionaries may have race conditions
**Details:**
- `Dictionary<MethodBase, byte[]> state` and others are static
- While some operations use locks, initialization might not be thread-safe
- Multiple Harmony instances could conflict
**Impact:** Data corruption in multi-threaded scenarios

### 5. Buffer Sharing in FileLog
**Location:** `Harmony/Tools/FileLog.cs:71`
**Issue:** Static buffer shared across all instances
**Details:**
- `static List<string> buffer = [];` is shared globally
- While operations are locked, this design could cause issues
**Impact:** Log entries could be mixed between different Harmony instances

## Unimplemented Features

### 6. Infix Patches Not Implemented (Medium Priority)
**Location:** `Harmony/Internal/Infix.cs:33`
**Issue:** Infix patches are designed but not implemented
**Details:**
- `Apply()` method contains only `// TODO: implement`
- Infrastructure exists but core functionality is missing
- Commented out in public API: `Harmony.cs:160`
**Impact:** Feature advertised in code but non-functional

### 7. Filter Exception Blocks Unsupported
**Location:** `docs/articles/patching-transpiler-codes.html:103`
**Issue:** Exception filter blocks cannot be created dynamically
**Details:**
- `ExceptionBlockType.BeginExceptFilterBlock` marked as not supported
- Limitation of current IL generation approach
**Impact:** Cannot patch methods with exception filters

## Type System Limitations

### 8. Generic Type Support Issues (Medium Priority)
**Location:** `docs/articles/patching-edgecases.html:110-117`
**Issue:** Generic method patching has multiple limitations
**Details:**
- Generic methods share implementations across reference types
- Value types may or may not share implementations
- Generic type information can be lost during patching
- Behavior varies by .NET runtime
**Impact:** Unreliable behavior when patching generic methods

### 9. Inlining Makes Methods Unpatchable
**Location:** `docs/articles/patching-edgecases.html:74-76`
**Issue:** Inlined methods cannot be patched
**Details:**
- JIT optimization removes method calls
- No warning when patch fails due to inlining
- Detection of inlined methods is not implemented
**Impact:** Silent patch failures

## Exception Handling Issues

### 10. Generic Exception Swallowing
**Location:** `Harmony/Internal/PatchFunctions.cs:43, 87`
**Issue:** Broad exception catching may hide specific bugs
**Details:**
- `catch (Exception ex)` catches all exceptions
- Could hide important debugging information
- Stack traces may be lost or modified
**Impact:** Difficult debugging when patches fail

### 11. Empty Catch Block in FileLog
**Location:** `Harmony/Tools/FileLog.cs:55`
**Issue:** `finally { }` with empty block suggests missing error handling
**Details:**
- Desktop path creation has empty finally block
- Suggests error handling was planned but not implemented
**Impact:** Errors in log path creation are silently ignored

## Resource Management Issues

### 12. Potential Resource Leaks in Module Creation
**Location:** `Harmony/Internal/HarmonySharedState.cs:97`, `MethodCopier.cs:188`
**Issue:** Module creation uses `using` correctly, but error paths may leak
**Details:**
- Complex module creation process
- Multiple failure points before proper disposal
**Impact:** Memory leaks in error scenarios

## API Design Issues

### 13. Inconsistent State Type Validation
**Location:** `Harmony/Internal/MethodCreator.cs:88`
**Issue:** __state type mismatches cause runtime exceptions
**Details:**
- Type compatibility checked at patch time, not compile time
- Error messages provide good detail but crash is still unexpected
**Impact:** Patch application failures

### 14. Unsafe LINQ Operations
**Location:** `Harmony/Internal/MethodCreator.cs:329`
**Issue:** `FirstOrDefault()` used without null checking
**Details:**
- `var firstFixParam = fix.GetParameters().FirstOrDefault();`
- Result used without null check in some contexts
**Impact:** Potential NullReferenceException

### 15. Unsafe Casting Operations
**Location:** Multiple files using `.Cast<>()` operations
**Issue:** LINQ Cast operations can throw InvalidCastException
**Details:**
- `instructions.Cast<CodeInstruction>()` and similar operations
- No validation that cast will succeed
**Impact:** Runtime exceptions when types don't match

## Documentation and Maintenance Issues

### 16. Incomplete Unpatching Support
**Location:** Multiple locations in API documentation
**Issue:** "Fully unpatching is not supported" mentioned in several places
**Details:**
- Partial implementation of unpatching functionality
- May leave system in inconsistent state
**Impact:** Memory leaks and unexpected behavior after unpatching

### 17. Experimental Features in Production
**Location:** `docs/articles/patching-transpiler-codes.html:92`
**Issue:** SignatureHelper support marked as "experimental at best"
**Details:**
- Production code includes experimental features
- No clear migration path or stability guarantees
**Impact:** Unreliable behavior in production environments

## Performance Issues

### 18. Excessive String Concatenation in Logging
**Location:** `Harmony/Tools/FileLog.cs` various methods
**Issue:** String concatenation in hot paths
**Details:**
- Multiple string operations without StringBuilder
- Could impact performance during heavy logging
**Impact:** Performance degradation

### 19. Reflection-Heavy Code Paths
**Location:** Throughout codebase, especially in patching logic
**Issue:** Heavy use of reflection without caching
**Details:**
- Method lookups, field access, etc. repeated without caching
- Could benefit from reflection result caching
**Impact:** Performance overhead

## Recommended Actions

### Immediate (High Priority)
1. Fix nullable result handling crash
2. Add null checks for reflection operations  
3. Improve StackTrace.GetFrame error handling
4. Review and fix thread safety issues

### Short Term (Medium Priority)
1. Implement or remove infix patches feature
2. Add better error handling for generic type issues
3. Improve exception handling specificity
4. Add validation for unsafe LINQ operations

### Long Term (Low Priority)
1. Implement full unpatching support
2. Add inlining detection and warnings
3. Performance optimization for reflection-heavy paths
4. Stabilize experimental features

## Testing Recommendations

1. Add comprehensive tests for nullable result scenarios
2. Add multi-threading stress tests
3. Add tests for generic type edge cases
4. Add negative tests for error conditions
5. Add performance benchmarks for critical paths

---

**Note:** This TODO list was generated through systematic code analysis. Each item should be reviewed and prioritized based on project needs and user impact.