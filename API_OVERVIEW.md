# Harmony API Overview for AI

> **Generated on:** 2025-09-04 20:43:47 UTC
> **Purpose:** Dense API reference optimized for AI context windows
> **Source:** Harmony 2.4.1.0 - https://github.com/pardeike/Harmony

## Quick Reference

Harmony is a .NET library for runtime patching of methods. Key concepts:
- **Harmony Instance**: Entry point for all patching operations
- **Patches**: Prefix, Postfix, Transpiler, and Finalizer modifications
- **Attributes**: Declarative patch definitions using C# attributes
- **AccessTools**: Reflection utilities for accessing private members
- **Traverse**: Safe reflection wrapper for reading/writing private data
- **CodeMatcher**: IL manipulation for transpilers

## Core Categories

### Core

#### Harmony
*The Harmony instance is the main entry to Harmony. After creating one with an unique identifier, it is used to patch and query the current application domain*

**Key Methods:**
- `void PatchAll()` - Searches the current assembly for Harmony annotations and uses them to create patches
- `PatchProcessor CreateProcessor(MethodBase original) => new(this, original);` - Creates a empty patch processor for an original method
- `PatchClassProcessor CreateClassProcessor(Type type) => new(this, type);` - Creates a patch class processor from a class
- `ReversePatcher CreateReversePatcher(MethodBase original, HarmonyMethod standin) => new(this, original, standin);` - Creates a reverse patcher for one of your stub methods
- `void PatchAll(Assembly assembly) => AccessTools.GetTypesFromAssembly(assembly).DoIf(type => type.HasHarmonyAttribute(), type => CreateClassProcessor(type).Patch());` - Searches an assembly for HarmonyPatch-annotated classes/structs and uses them to create patches
- `void PatchAllUncategorized()` - Searches an assembly for HarmonyPatch-annotated classes/structs without category annotations and uses them to create patches
- `void PatchAllUncategorized(Assembly assembly)` - Searches an assembly for HarmonyPatch-annotated classes/structs without category annotations and uses them to create patches
- `void PatchCategory(string category)` - Searches the current assembly for Harmony annotations with a specific category and uses them to create patches
- *...and 16 more methods*

**Properties:**
- `string Id get; set; }` - The unique identifier

**Fields:**
- `bool DEBUG;` - Set to true before instantiating Harmony to debug Harmony or use an environment variable to set HARMONY_DEBUG to '1' like this: cmd /C "set HARMONY_DEBUG=1 &amp;&amp; game.exe"

### Patching

#### Patch
**Key Methods:**
- `MethodInfo GetMethod(MethodBase original)` - Get the patch method or a DynamicMethod if original patch method is a patch factory
- `int CompareTo(object obj) => PatchInfoSerialization.PriorityComparer(obj, index, priority);` - Determines how patches sort

**Fields:**
- `readonly int index;` - Zero-based index
- `readonly string owner;` - The owner (Harmony ID)
- `readonly int priority;` - The priority, see 
- `readonly string[] before;` - Keep this patch before the patches indicated in the list of Harmony IDs
- `readonly string[] after;` - Keep this patch after the patches indicated in the list of Harmony IDs
- `readonly bool debug;` - A flag that will log the replacement method via every time this patch is used to build the replacement, even in the future
- `readonly InnerMethod innerMethod;` - For an infix patch, this defines the inner method that we will apply the patch to

#### PatchClassProcessor
*A PatchClassProcessor used to turn on a class/type into patches*

**Key Methods:**
- `List<MethodInfo> Patch()` - Applies the patches
- `void Unpatch()` - REmoves the patches

**Properties:**
- `string Category get; set; }`

#### PatchInfo
**Key Methods:**
- `bool Debugging => prefixes.Any(p => p.debug)`
- `void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddPrefixes(owner, new HarmonyMethod(patch, priority, before, after, debug));`
- `void RemovePrefix(string owner) => prefixes = Remove(owner, prefixes);` - Removes prefixes
- `void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddPostfixes(owner, new HarmonyMethod(patch, priority, before, after, debug));`
- `void RemovePostfix(string owner) => postfixes = Remove(owner, postfixes);` - Removes postfixes
- `void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddTranspilers(owner, new HarmonyMethod(patch, priority, before, after, debug));`
- `void RemoveTranspiler(string owner) => transpilers = Remove(owner, transpilers);` - Removes transpilers
- `void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddFinalizers(owner, new HarmonyMethod(patch, priority, before, after, debug));`
- *...and 4 more methods*

**Fields:**
- `int VersionCount = 0;`

#### PatchProcessor
*A PatchProcessor handles patches on a method/constructor*

**Key Methods:**
- `PatchProcessor AddPrefix(HarmonyMethod prefix)` - Adds a prefix
- `PatchProcessor AddPrefix(MethodInfo fixMethod)` - Adds a prefix
- `PatchProcessor AddPostfix(HarmonyMethod postfix)` - Adds a postfix
- `PatchProcessor AddPostfix(MethodInfo fixMethod)` - Adds a postfix
- `PatchProcessor AddTranspiler(HarmonyMethod transpiler)` - Adds a transpiler
- `PatchProcessor AddTranspiler(MethodInfo fixMethod)` - Adds a transpiler
- `PatchProcessor AddFinalizer(HarmonyMethod finalizer)` - Adds a finalizer
- `PatchProcessor AddFinalizer(MethodInfo fixMethod)` - Adds a finalizer
- *...and 19 more methods*

#### Patches
*A group of patches*

**Fields:**
- `readonly ReadOnlyCollection<Patch> Prefixes;` - A collection of prefix 
- `readonly ReadOnlyCollection<Patch> Postfixes;` - A collection of postfix 
- `readonly ReadOnlyCollection<Patch> Transpilers;` - A collection of transpiler 
- `readonly ReadOnlyCollection<Patch> Finalizers;` - A collection of finalizer 
- `readonly ReadOnlyCollection<Patch> InnerPrefixes;` - A collection of inner prefix 
- `readonly ReadOnlyCollection<Patch> InnerPostfixes;` - A collection of inner postfix 

#### ReversePatcher
*A reverse patcher*

**Key Methods:**
- `MethodInfo Patch(HarmonyReversePatchType type = HarmonyReversePatchType.Original)` - Applies the patch

### Attributes

#### MethodType
*Specifies the type of method*

**Key Methods:**
- `HarmonyMethod info = new();` - The common information for all attributes
- `HarmonyPatchCategory(string category) => info.category = category;` - Annotation specifying the category
- `HarmonyPatch()` - An empty annotation can be used together with TargetMethod(s)
- `HarmonyPatch(Type declaringType) => info.declaringType = declaringType;` - An annotation that specifies a class to patch
- `HarmonyPatch(Type declaringType, Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, string methodName)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, string methodName, params Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- *...and 41 more methods*

**Properties:**
- `string OriginalName get; set; }` - The name of the original argument
- `int Index get; set; }` - The index of the original argument
- `string NewName get; set; }` - The new name of the original argument

### Transpiling

#### CodeInstruction
*An abstract wrapper around OpCode and their operands. Used by transpilers*

**Key Methods:**
- `CodeInstruction Clone()` - Clones a CodeInstruction and resets its labels and exception blocks
- `CodeInstruction Clone(OpCode opcode)` - Clones a CodeInstruction, resets labels and exception blocks and sets its opcode
- `CodeInstruction Clone(object operand)` - Clones a CodeInstruction, resets labels and exception blocks and sets its operand
- `CodeInstruction Call(Type type, string name, Type[] parameters = null, Type[] generics = null)` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction Call(string typeColonMethodname, Type[] parameters = null, Type[] generics = null)` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction Call(Expression<Action> expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction Call<T>(Expression<Action<T>> expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction Call<T, TResult>(Expression<Func<T, TResult>> expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));` - Creates a CodeInstruction calling a method (CALL)
- *...and 8 more methods*

**Fields:**
- `OpCode opcode;` - The opcode
- `object operand;` - The operand

#### CodeMatch
*A CodeInstruction match*

**Key Methods:**
- `CodeMatch WithOpcodes(HashSet<OpCode> opcodes, object operand = null, string name = null) => new(null, operand, name) { opcodeSet = opcodes };` - Creates a code match
- `CodeMatch IsLdarg(int? n = null) => new(instruction => instruction.IsLdarg(n));` - Tests for any form of Ldarg*
- `CodeMatch IsLdarga(int? n = null) => new(instruction => instruction.IsLdarga(n));` - Tests for Ldarga/Ldarga_S
- `CodeMatch IsStarg(int? n = null) => new(instruction => instruction.IsStarg(n));` - Tests for Starg/Starg_S
- `CodeMatch IsLdloc(LocalBuilder variable = null) => new(instruction => instruction.IsLdloc(variable));` - Tests for any form of Ldloc*
- `CodeMatch IsStloc(LocalBuilder variable = null) => new(instruction => instruction.IsStloc(variable));` - Tests for any form of Stloc*
- `CodeMatch Calls(MethodInfo method) => WithOpcodes(CodeInstructionExtensions.opcodesCalling, method);` - Tests if the code instruction calls the method/constructor
- `CodeMatch LoadsConstant() => new(instruction => instruction.LoadsConstant());` - Tests if the code instruction loads a constant
- *...and 13 more methods*

**Fields:**
- `string name;` - The name of the match
- `Func<CodeInstruction, bool> predicate;` - The match predicate

#### CodeMatcher
*A CodeInstruction matcher*

**Key Methods:**
- `delegate bool ErrorHandler(CodeMatcher matcher, string error);` - Delegate for error handling
- `int Remaining => Length - Math.Max(0, Pos);` - Gets the remaining code instructions
- `CodeMatcher Clone()` - Makes a clone of this instruction matcher
- `CodeMatcher Reset(bool atFirstInstruction = true)` - Resets the current position to -1 and clears last matches and errors
- `CodeInstruction InstructionAt(int offset) => codes[Pos + offset];` - Gets instructions at the current position with offset
- `List<CodeInstruction> Instructions() => codes;` - Gets all instructions
- `IEnumerable<CodeInstruction> InstructionEnumeration() => codes.AsEnumerable();` - Gets all instructions as an enumeration
- `List<CodeInstruction> Instructions(int count)` - Gets some instructions counting from current position
- *...and 60 more methods*

**Properties:**
- `int Pos get; set; } = -1;` - The current position

**Fields:**
- `bool IsValid => Pos >= 0 && Pos < Length;` - Checks whether the position of this CodeMatcher is within bounds
- `bool IsInvalid => Pos < 0 || Pos >= Length;` - Checks whether the position of this CodeMatcher is outside its bounds

### Utilities

#### Traverse
*A reflection helper to read and write private elements*

**Key Methods:**
- `Traverse Create(Type type) => new(type);` - Creates a new traverse instance from a class/type
- `Traverse Create<T>() => Create(typeof(T));` - Creates a new traverse instance from a class T
- `Traverse Create(object root) => new(root);` - Creates a new traverse instance from an instance
- `Traverse CreateWithType(string name) => new(AccessTools.TypeByName(name));` - Creates a new traverse instance from a named type
- `object GetValue()` - Gets the current value
- `object GetValue(params object[] arguments)` - Invokes the current method with arguments and returns the result
- `Traverse SetValue(object value)` - Sets a value of the current field or property
- `Type GetValueType()` - Gets the type of the current field or property
- *...and 20 more methods*

**Fields:**
- `Traverse(Traverse traverse) => this.traverse = traverse;` - Creates a traverse instance from an existing instance
- `Traverse(Type type) => _type = type;` - Creates a new traverse instance from a class/type
- `bool IsField => _info is FieldInfo;` - Checks if the current traverse instance is for a field
- `bool IsProperty => _info is PropertyInfo;` - Checks if the current traverse instance is for a property

### Exceptions

#### ExceptionBlockType
*Exception block types*

**Key Methods:**
- `ExceptionBlock(ExceptionBlockType blockType, Type catchType = null)` - Creates a new ExceptionBlock

**Fields:**
- `ExceptionBlockType blockType;` - Block type
- `Type catchType;` - Catch type

#### HarmonyException
*Under Mono, HarmonyException wraps IL compile errors with detailed information about the failure*

**Key Methods:**
- `List<KeyValuePair<int, CodeInstruction>> GetInstructionsWithOffsets() => [.. instructions.OrderBy(ins => ins.Key)];` - Get a list of IL instructions in pairs of offset+code
- `List<CodeInstruction> GetInstructions() => [.. instructions.OrderBy(ins => ins.Key).Select(ins => ins.Value)];` - Get a list of IL instructions without offsets
- `int GetErrorOffset() => errorOffset;` - Get the error offset of the errornous IL instruction
- `int GetErrorIndex()` - Get the index of the errornous IL instruction

### Other

#### HarmonyMethod
*A wrapper around a method to use it as a patch (for example a Prefix)*

**Key Methods:**
- `List<string> HarmonyFields()` - Gets the names of all internal patch info fields
- `HarmonyMethod Merge(List<HarmonyMethod> attributes)` - Merges annotations
- `void CopyTo(this HarmonyMethod from, HarmonyMethod to)` - Copies annotation information
- `HarmonyMethod Clone(this HarmonyMethod original)` - Clones an annotation
- `HarmonyMethod Merge(this HarmonyMethod master, HarmonyMethod detail)` - Merges annotations
- `List<HarmonyMethod> GetFromType(Type type)` - Gets all annotations on a class/type
- `HarmonyMethod GetMergedFromType(Type type) => HarmonyMethod.Merge(GetFromType(type));` - Gets merged annotations on a class/type
- `List<HarmonyMethod> GetFromMethod(MethodBase method)` - Gets all annotations on a method
- *...and 1 more methods*

**Fields:**
- `MethodInfo method; // need to be called 'method'` - The original method
- `string category = null;` - Patch Category
- `Type declaringType;` - Class/type declaring this patch
- `string methodName;` - Patch method name
- `MethodType? methodType;` - Optional patch 
- `Type[] argumentTypes;` - Array of argument types of the patch method
- `string[] before;` - Install this patch before patches with these Harmony IDs
- `string[] after;` - Install this patch after patches with these Harmony IDs
- `HarmonyReversePatchType? reversePatchType;` - Reverse patch type, see 
- `bool? debug;` - Create debug output for this patch
- `bool nonVirtualDelegate;` - Whether to use (true) or (false) mechanics for -attributed delegate

#### InnerMethod
**Fields:**
- `int[] positions;` - Which occcurances (1-based) of the method, negative numbers are counting from the end, empty array means all occurances

#### Operand_
**Key Methods:**
- `Nop_ Nop => new(OpCodes.Nop);`
- `Break_ Break => new(OpCodes.Break);`
- `Ldarg_0_ Ldarg_0 => new(OpCodes.Ldarg_0);`
- `Ldarg_1_ Ldarg_1 => new(OpCodes.Ldarg_1);`
- `Ldarg_2_ Ldarg_2 => new(OpCodes.Ldarg_2);`
- `Ldarg_3_ Ldarg_3 => new(OpCodes.Ldarg_3);`
- `Ldloc_0_ Ldloc_0 => new(OpCodes.Ldloc_0);`
- `Ldloc_1_ Ldloc_1 => new(OpCodes.Ldloc_1);`
- *...and 218 more methods*

## Common Usage Patterns

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

