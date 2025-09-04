# Harmony API Complete Reference

Generated: 2025-09-04 20:58:39 UTC
Purpose: Complete API reference for AI consumption
Source: Harmony 2.4.1.0
URL: https://github.com/pardeike/Harmony

## Summary

Harmony is a .NET library for runtime method patching. Core concepts:
- Harmony Instance: Entry point for patching operations
- Patches: Prefix, Postfix, Transpiler, Finalizer modifications  
- Attributes: Declarative patches using C# attributes
- AccessTools: Reflection utilities for private member access
- Traverse: Safe reflection wrapper for data access
- CodeMatcher: IL manipulation for transpilers

## Quick Start

### Basic Usage
```csharp
var harmony = new Harmony("com.company.project.product");
var assembly = Assembly.GetExecutingAssembly();
			harmony.PatchAll(assembly);

			// or implying current assembly:
			harmony.PatchAll();
```

### Manual Patching  
```csharp
// add null checks to the following lines, they are omitted for clarity
				// when possible, don't use string and instead use nameof(...)
				var original = typeof(TheClass).GetMethod("TheMethod");
				var prefix = typeof(MyPatchClass1).GetMethod("SomeMethod");
				var postfix = typeof(MyPatchClass2).GetMethod("SomeMethod");

				harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));

				// You can use named arguments to specify certain patch types only:
				harmony.Patch(original, postfix: new HarmonyMethod(postfix));
				harmony.Patch(original, prefix: new HarmonyMethod(prefix), transpiler: new HarmonyMethod(transpiler));
```

### Attribute-Based Patching
```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
class PatchClass {
    static bool Prefix() => true;  // Allow original to run
    static void Postfix() { }      // Run after original
}
```

## Complete API Reference

### Core

#### Harmony
The Harmony instance is the main entry to Harmony. After creating one with an unique identifier, it is used to patch and query the current application domain

Methods:
- `void PatchAll()` - Searches the current assembly for Harmony annotations and uses them to create patches
- `PatchProcessor CreateProcessor(MethodBase original) => new(this, original);` - Creates a empty patch processor for an original method
- `PatchClassProcessor CreateClassProcessor(Type type) => new(this, type);` - Creates a patch class processor from a class
- `ReversePatcher CreateReversePatcher(MethodBase original, HarmonyMethod standin) => new(this, original, standin);` - Creates a reverse patcher for one of your stub methods
- `void PatchAll(Assembly assembly) => AccessTools.GetTypesFromAssembly(assembly).DoIf(type => type.HasHarmonyAttribute(), type => CreateClassProcessor(type).Patch());` - Searches an assembly for HarmonyPatch-annotated classes/structs and uses them to create patches
- `void PatchAllUncategorized()` - Searches an assembly for HarmonyPatch-annotated classes/structs without category annotations and uses them to create patches
- `void PatchAllUncategorized(Assembly assembly)` - Searches an assembly for HarmonyPatch-annotated classes/structs without category annotations and uses them to create patches
- `void PatchCategory(string category)` - Searches the current assembly for Harmony annotations with a specific category and uses them to create patches
- `void PatchCategory(Assembly assembly, string category)` - Searches an assembly for HarmonyPatch-annotated classes/structs with a specific category and uses them to create patches
- `MethodInfo Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null/*, HarmonyMethod infix = null*/)` - Creates patches by manually specifying the methods
- `MethodInfo ReversePatch(MethodBase original, HarmonyMethod standin, MethodInfo transpiler = null) => PatchFunctions.ReversePatch(standin, original, transpiler);` - Patches a foreign method onto a stub method of yours and optionally applies transpilers during the process
- `void UnpatchAll(string harmonyID = null)` - Unpatches methods by patching them with zero patches. Fully unpatching is not supported. Be careful, unpatching is global
- `void Unpatch(MethodBase original, HarmonyPatchType type, string harmonyID = "*")` - Unpatches a method by patching it with zero patches. Fully unpatching is not supported. Be careful, unpatching is global
- `void Unpatch(MethodBase original, MethodInfo patch)` - Unpatches a method by patching it with zero patches. Fully unpatching is not supported. Be careful, unpatching is global
- `void UnpatchCategory(string category)` - Searches the current assembly for types with a specific category annotation and uses them to unpatch existing patches. Fully unpatching is not supported. Be careful, unpatching is global
- `void UnpatchCategory(Assembly assembly, string category)` - Searches an assembly for HarmonyPatch-annotated classes/structs with a specific category annotation and uses them to unpatch existing patches. Fully unpatching is not supported. Be careful, unpatching is global
- `bool HasAnyPatches(string harmonyID)` - Test for patches from a specific Harmony ID
- `Patches GetPatchInfo(MethodBase method) => PatchProcessor.GetPatchInfo(method);` - Gets patch information for a given original method
- `IEnumerable<MethodBase> GetPatchedMethods()` - Gets the methods this instance has patched
- `IEnumerable<MethodBase> GetAllPatchedMethods() => PatchProcessor.GetAllPatchedMethods();` - Gets all patched original methods in the appdomain
- `MethodBase GetOriginalMethod(MethodInfo replacement)` - Gets the original method from a given replacement method
- `MethodBase GetMethodFromStackframe(StackFrame frame)` - Tries to get the method from a stackframe including dynamic replacement methods
- `MethodBase GetOriginalMethodFromStackframe(StackFrame frame)` - Gets the original method from the stackframe and uses original if method is a dynamic replacement
- `Dictionary<string, Version> VersionInfo(out Version currentVersion)` - Gets Harmony version for all active Harmony instances

Properties:
- `string Id get; set; }` - The unique identifier

Fields:
- `bool DEBUG;` - Set to true before instantiating Harmony to debug Harmony or use an environment variable to set HARMONY_DEBUG to '1' like this: cmd /C "set HARMONY_DEBUG=1 &amp;&amp; game.exe"

### Patching

#### Patch
Methods:
- `MethodInfo GetMethod(MethodBase original)` - Get the patch method or a DynamicMethod if original patch method is a patch factory
- `bool Equals(object obj) => ((obj is not null) && (obj is Patch) && (PatchMethod == ((Patch)obj).PatchMethod));` - Determines whether patches are equal
- `int CompareTo(object obj) => PatchInfoSerialization.PriorityComparer(obj, index, priority);` - Determines how patches sort
- `int GetHashCode() => PatchMethod.GetHashCode();` - Hash function

Fields:
- `readonly int index;` - Zero-based index
- `readonly string owner;` - The owner (Harmony ID)
- `readonly int priority;` - The priority, see 
- `readonly string[] before;` - Keep this patch before the patches indicated in the list of Harmony IDs
- `readonly string[] after;` - Keep this patch after the patches indicated in the list of Harmony IDs
- `readonly bool debug;` - A flag that will log the replacement method via every time this patch is used to build the replacement, even in the future
- `readonly InnerMethod innerMethod;` - For an infix patch, this defines the inner method that we will apply the patch to

#### PatchClassProcessor
A PatchClassProcessor used to turn on a class/type into patches

Methods:
- `List<MethodInfo> Patch()` - Applies the patches
- `void Unpatch()` - REmoves the patches

Properties:
- `string Category get; set; }`

#### PatchInfo
Methods:
- `bool Debugging => prefixes.Any(p => p.debug)`
- `void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddPrefixes(owner, new HarmonyMethod(patch, priority, before, after, debug));`
- `void RemovePrefix(string owner) => prefixes = Remove(owner, prefixes);` - Removes prefixes
- `void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddPostfixes(owner, new HarmonyMethod(patch, priority, before, after, debug));`
- `void RemovePostfix(string owner) => postfixes = Remove(owner, postfixes);` - Removes postfixes
- `void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddTranspilers(owner, new HarmonyMethod(patch, priority, before, after, debug));`
- `void RemoveTranspiler(string owner) => transpilers = Remove(owner, transpilers);` - Removes transpilers
- `void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddFinalizers(owner, new HarmonyMethod(patch, priority, before, after, debug));`
- `void RemoveFinalizer(string owner) => finalizers = Remove(owner, finalizers);` - Removes finalizers
- `void RemoveInnerPrefix(string owner) => innerprefixes = Remove(owner, innerprefixes);` - Removes inner prefixes
- `void RemoveInnerPostfix(string owner) => innerpostfixes = Remove(owner, innerpostfixes);` - Removes inner postfixes
- `void RemovePatch(MethodInfo patch)` - Removes a patch using its method

Fields:
- `int VersionCount = 0;`

#### PatchProcessor
A PatchProcessor handles patches on a method/constructor

Methods:
- `PatchProcessor AddPrefix(HarmonyMethod prefix)` - Adds a prefix
- `PatchProcessor AddPrefix(MethodInfo fixMethod)` - Adds a prefix
- `PatchProcessor AddPostfix(HarmonyMethod postfix)` - Adds a postfix
- `PatchProcessor AddPostfix(MethodInfo fixMethod)` - Adds a postfix
- `PatchProcessor AddTranspiler(HarmonyMethod transpiler)` - Adds a transpiler
- `PatchProcessor AddTranspiler(MethodInfo fixMethod)` - Adds a transpiler
- `PatchProcessor AddFinalizer(HarmonyMethod finalizer)` - Adds a finalizer
- `PatchProcessor AddFinalizer(MethodInfo fixMethod)` - Adds a finalizer
- `PatchProcessor AddInnerPrefix(HarmonyMethod innerPrefix)` - Adds an inner prefix
- `PatchProcessor AddInnerPrefix(MethodInfo fixMethod)` - Adds an inner prefix
- `PatchProcessor AddInnerPostfix(HarmonyMethod innerPostfix)` - Adds an inner postfix
- `PatchProcessor AddInnerPostfix(MethodInfo fixMethod)` - Adds an inner postfix
- `IEnumerable<MethodBase> GetAllPatchedMethods()` - Gets all patched original methods in the appdomain
- `MethodInfo Patch()` - Applies all registered patches
- `PatchProcessor Unpatch(HarmonyPatchType type, string harmonyID)` - Unpatches patches of a given type and/or Harmony ID
- `PatchProcessor Unpatch(MethodInfo patch)` - Unpatches a specific patch
- `Patches GetPatchInfo(MethodBase method)` - Gets patch information on an original
- `List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches) => PatchFunctions.GetSortedPatchMethods(original, patches, false);` - Sort patch methods by their priority rules
- `Dictionary<string, Version> VersionInfo(out Version currentVersion)` - Gets Harmony version for all active Harmony instances
- `ILGenerator CreateILGenerator()` - Creates a new empty generator to use when reading method bodies
- `ILGenerator CreateILGenerator(MethodBase original)` - Creates a new generator matching the method/constructor to use when reading method bodies
- `List<CodeInstruction> GetOriginalInstructions(MethodBase original, ILGenerator generator = null) => MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, 0);` - Returns the methods unmodified list of code instructions
- `List<CodeInstruction> GetOriginalInstructions(MethodBase original, out ILGenerator generator)` - Returns the methods unmodified list of code instructions
- `List<CodeInstruction> GetCurrentInstructions(MethodBase original, int maxTranspilers = int.MaxValue, ILGenerator generator = null) => MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, maxTranspilers);` - Returns the methods current list of code instructions after all existing transpilers have been applied
- `List<CodeInstruction> GetCurrentInstructions(MethodBase original, out ILGenerator generator, int maxTranspilers = int.MaxValue)` - Returns the methods current list of code instructions after all existing transpilers have been applied
- `IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBody(MethodBase method)` - A low level way to read the body of a method. Used for quick searching in methods
- `IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBody(MethodBase method, ILGenerator generator)` - A low level way to read the body of a method. Used for quick searching in methods

#### Patches
A group of patches

Fields:
- `readonly ReadOnlyCollection<Patch> Prefixes;` - A collection of prefix 
- `readonly ReadOnlyCollection<Patch> Postfixes;` - A collection of postfix 
- `readonly ReadOnlyCollection<Patch> Transpilers;` - A collection of transpiler 
- `readonly ReadOnlyCollection<Patch> Finalizers;` - A collection of finalizer 
- `readonly ReadOnlyCollection<Patch> InnerPrefixes;` - A collection of inner prefix 
- `readonly ReadOnlyCollection<Patch> InnerPostfixes;` - A collection of inner postfix 

#### ReversePatcher
A reverse patcher

Methods:
- `MethodInfo Patch(HarmonyReversePatchType type = HarmonyReversePatchType.Original)` - Applies the patch

### Attributes

#### MethodType
Specifies the type of method

Methods:
- `HarmonyMethod info = new();` - The common information for all attributes
- `HarmonyPatchCategory(string category) => info.category = category;` - Annotation specifying the category
- `HarmonyPatch()` - An empty annotation can be used together with TargetMethod(s)
- `HarmonyPatch(Type declaringType) => info.declaringType = declaringType;` - An annotation that specifies a class to patch
- `HarmonyPatch(Type declaringType, Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, string methodName)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, string methodName, params Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, MethodType methodType)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, MethodType methodType, params Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type declaringType, string methodName, MethodType methodType)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(string methodName) => info.methodName = methodName;` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(string methodName, params Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(string methodName, MethodType methodType)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(MethodType methodType) => info.methodType = methodType;` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(MethodType methodType, params Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type[] argumentTypes) => info.argumentTypes = argumentTypes;` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(Type[] argumentTypes, ArgumentType[] argumentVariations) => ParseSpecialArguments(argumentTypes, argumentVariations);` - An annotation that specifies a method, property or constructor to patch
- `HarmonyPatch(string typeName, string methodName, MethodType methodType = MethodType.Normal)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type declaringType)` - An annotation that specifies a class to patch
- `HarmonyDelegate(Type declaringType, Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type declaringType, string methodName)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type declaringType, string methodName, params Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type declaringType, MethodDispatchType methodDispatchType)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type declaringType, MethodDispatchType methodDispatchType, params Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type declaringType, MethodDispatchType methodDispatchType, Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type declaringType, string methodName, MethodDispatchType methodDispatchType)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(string methodName)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(string methodName, params Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(string methodName, MethodDispatchType methodDispatchType)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(MethodDispatchType methodDispatchType) => info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;` - An annotation that specifies call dispatching mechanics for the delegate
- `HarmonyDelegate(MethodDispatchType methodDispatchType, params Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(MethodDispatchType methodDispatchType, Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type[] argumentTypes)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyDelegate(Type[] argumentTypes, ArgumentType[] argumentVariations)` - An annotation that specifies a method, property or constructor to patch
- `HarmonyReversePatch(HarmonyReversePatchType type = HarmonyReversePatchType.Original) => info.reversePatchType = type;` - An annotation that specifies the type of reverse patching
- `HarmonyPriority(int priority) => info.priority = priority;` - A Harmony annotation to define patch priority
- `HarmonyBefore(params string[] before) => info.before = before;` - A Harmony annotation to define that a patch comes before another patch
- `HarmonyAfter(params string[] after) => info.after = after;` - A Harmony annotation to define that a patch comes after another patch
- `HarmonyDebug() => info.debug = true;` - A Harmony annotation to debug a patch (output uses to log to your Desktop)
- `HarmonyArgument(string originalName) : this(originalName, null)` - An annotation to declare injected arguments by name
- `HarmonyArgument(int index) : this(index, null)` - An annotation to declare injected arguments by index
- `HarmonyArgument(string originalName, string newName)` - An annotation to declare injected arguments by renaming them
- `HarmonyArgument(int index, string name)` - An annotation to declare injected arguments by index and renaming them

Properties:
- `string OriginalName get; set; }` - The name of the original argument
- `int Index get; set; }` - The index of the original argument
- `string NewName get; set; }` - The new name of the original argument

### Transpiling

#### CodeInstruction
An abstract wrapper around OpCode and their operands. Used by transpilers

Methods:
- `CodeInstruction Clone()` - Clones a CodeInstruction and resets its labels and exception blocks
- `CodeInstruction Clone(OpCode opcode)` - Clones a CodeInstruction, resets labels and exception blocks and sets its opcode
- `CodeInstruction Clone(object operand)` - Clones a CodeInstruction, resets labels and exception blocks and sets its operand
- `CodeInstruction Call(Type type, string name, Type[] parameters = null, Type[] generics = null)` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction Call(string typeColonMethodname, Type[] parameters = null, Type[] generics = null)` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction Call(Expression<Action> expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction Call<T>(Expression<Action<T>> expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction Call<T, TResult>(Expression<Func<T, TResult>> expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction Call(LambdaExpression expression) => new(OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));` - Creates a CodeInstruction calling a method (CALL)
- `CodeInstruction LoadField(Type type, string name, bool useAddress = false)` - Creates a CodeInstruction loading a field (LD[S]FLD[A])
- `CodeInstruction StoreField(Type type, string name)` - Creates a CodeInstruction storing to a field (ST[S]FLD)
- `CodeInstruction LoadLocal(int index, bool useAddress = false)` - Creates a CodeInstruction loading a local with the given index, using the shorter forms when possible
- `CodeInstruction StoreLocal(int index)` - Creates a CodeInstruction storing to a local with the given index, using the shorter forms when possible
- `CodeInstruction LoadArgument(int index, bool useAddress = false)` - Creates a CodeInstruction loading an argument with the given index, using the shorter forms when possible
- `CodeInstruction StoreArgument(int index)` - Creates a CodeInstruction storing to an argument with the given index, using the shorter forms when possible
- `bool HasBlock(ExceptionBlockType type)` - Checks if a CodeInstruction contains a given exception block type
- `string ToString()` - Returns a string representation of the code instruction

Fields:
- `OpCode opcode;` - The opcode
- `object operand;` - The operand

#### CodeMatch
A CodeInstruction match

Methods:
- `CodeMatch WithOpcodes(HashSet<OpCode> opcodes, object operand = null, string name = null) => new(null, operand, name) { opcodeSet = opcodes };` - Creates a code match
- `CodeMatch IsLdarg(int? n = null) => new(instruction => instruction.IsLdarg(n));` - Tests for any form of Ldarg*
- `CodeMatch IsLdarga(int? n = null) => new(instruction => instruction.IsLdarga(n));` - Tests for Ldarga/Ldarga_S
- `CodeMatch IsStarg(int? n = null) => new(instruction => instruction.IsStarg(n));` - Tests for Starg/Starg_S
- `CodeMatch IsLdloc(LocalBuilder variable = null) => new(instruction => instruction.IsLdloc(variable));` - Tests for any form of Ldloc*
- `CodeMatch IsStloc(LocalBuilder variable = null) => new(instruction => instruction.IsStloc(variable));` - Tests for any form of Stloc*
- `CodeMatch Calls(MethodInfo method) => WithOpcodes(CodeInstructionExtensions.opcodesCalling, method);` - Tests if the code instruction calls the method/constructor
- `CodeMatch LoadsConstant() => new(instruction => instruction.LoadsConstant());` - Tests if the code instruction loads a constant
- `CodeMatch LoadsConstant(long number) => new(instruction => instruction.LoadsConstant(number));` - Tests if the code instruction loads an integer constant
- `CodeMatch LoadsConstant(double number) => new(instruction => instruction.LoadsConstant(number));` - Tests if the code instruction loads a floating point constant
- `CodeMatch LoadsConstant(Enum e) => new(instruction => instruction.LoadsConstant(e));` - Tests if the code instruction loads an enum constant
- `CodeMatch LoadsConstant(string str) => new(instruction => instruction.LoadsConstant(str));` - Tests if the code instruction loads a string constant
- `CodeMatch LoadsField(FieldInfo field, bool byAddress = false) => new(instruction => instruction.LoadsField(field, byAddress));` - Tests if the code instruction loads a field
- `CodeMatch StoresField(FieldInfo field) => new(instruction => instruction.StoresField(field));` - Tests if the code instruction stores a field
- `CodeMatch Calls(Expression<Action> expression) => new(expression);` - Creates a code match that calls a method
- `CodeMatch Calls(LambdaExpression expression) => new(expression);` - Creates a code match that calls a method
- `CodeMatch LoadsLocal(bool useAddress = false, string name = null) => WithOpcodes(useAddress ? CodeInstructionExtensions.opcodesLoadingLocalByAddress : CodeInstructionExtensions.opcodesLoadingLocalNormal, null, name);` - Creates a code match for local loads
- `CodeMatch StoresLocal(string name = null) => WithOpcodes(CodeInstructionExtensions.opcodesStoringLocal, null, name);` - Creates a code match for local stores
- `CodeMatch LoadsArgument(bool useAddress = false, string name = null) => WithOpcodes(useAddress ? CodeInstructionExtensions.opcodesLoadingArgumentByAddress : CodeInstructionExtensions.opcodesLoadingArgumentNormal, null, name);` - Creates a code match for argument loads
- `CodeMatch StoresArgument(string name = null) => WithOpcodes(CodeInstructionExtensions.opcodesStoringArgument, null, name);` - Creates a code match for argument stores
- `CodeMatch Branches(string name = null) => WithOpcodes(CodeInstructionExtensions.opcodesBranching, null, name);` - Creates a code match for branching
- `string ToString()` - Returns a string that represents the match

Fields:
- `string name;` - The name of the match
- `Func<CodeInstruction, bool> predicate;` - The match predicate

#### CodeMatcher
A CodeInstruction matcher

Methods:
- `delegate bool ErrorHandler(CodeMatcher matcher, string error);` - Delegate for error handling
- `int Remaining => Length - Math.Max(0, Pos);` - Gets the remaining code instructions
- `CodeMatcher Clone()` - Makes a clone of this instruction matcher
- `CodeMatcher Reset(bool atFirstInstruction = true)` - Resets the current position to -1 and clears last matches and errors
- `CodeInstruction InstructionAt(int offset) => codes[Pos + offset];` - Gets instructions at the current position with offset
- `List<CodeInstruction> Instructions() => codes;` - Gets all instructions
- `IEnumerable<CodeInstruction> InstructionEnumeration() => codes.AsEnumerable();` - Gets all instructions as an enumeration
- `List<CodeInstruction> Instructions(int count)` - Gets some instructions counting from current position
- `List<CodeInstruction> InstructionsInRange(int start, int end)` - Gets all instructions within a range
- `List<CodeInstruction> InstructionsWithOffsets(int startOffset, int endOffset) => InstructionsInRange(Pos + startOffset, Pos + endOffset);` - Gets all instructions within a range (relative to current position)
- `List<Label> DistinctLabels(IEnumerable<CodeInstruction> instructions) => [.. instructions.SelectMany(instruction => instruction.labels).Distinct()];` - Gets a list of all distinct labels
- `bool ReportFailure(MethodBase method, Action<string> logger)` - Reports a failure
- `CodeMatcher ThrowIfInvalid(string explanation)` - Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed)
- `CodeMatcher ThrowIfNotMatch(string explanation, params CodeMatch[] matches)` - Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed), or if the matches do not match at current position
- `CodeMatcher ThrowIfNotMatchForward(string explanation, params CodeMatch[] matches)` - Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed), or if the matches do not match at any point between current position and the end
- `CodeMatcher ThrowIfNotMatchBack(string explanation, params CodeMatch[] matches)` - Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed), or if the matches do not match at any point between current position and the start
- `CodeMatcher ThrowIfFalse(string explanation, Func<CodeMatcher, bool> stateCheckFunc)` - Throw an InvalidOperationException if current state is invalid (position out of bounds / last match failed), or if the check function returns false
- `CodeMatcher Do(Action<CodeMatcher> action)` - Runs some code when chaining at the current position
- `CodeMatcher OnError(ErrorHandler errorHandler)` - Registers an error handler that is invoked instead of throwing an exception
- `CodeMatcher SetInstruction(CodeInstruction instruction)` - Sets an instruction at current position
- `CodeMatcher SetInstructionAndAdvance(CodeInstruction instruction)` - Sets instruction at current position and advances
- `CodeMatcher Set(OpCode opcode, object operand)` - Sets opcode and operand at current position
- `CodeMatcher SetAndAdvance(OpCode opcode, object operand)` - Sets opcode and operand at current position and advances
- `CodeMatcher SetOpcodeAndAdvance(OpCode opcode)` - Sets opcode at current position and advances
- `CodeMatcher SetOperandAndAdvance(object operand)` - Sets operand at current position and advances
- `CodeMatcher DeclareLocal(Type variableType, out LocalBuilder localVariable)` - Declares a local variable but does not add it
- `CodeMatcher DefineLabel(out Label label)` - Declares a new label but does not add it
- `CodeMatcher CreateLabel(out Label label)` - Creates a label at current position
- `CodeMatcher CreateLabelAt(int position, out Label label)` - Creates a label at a position
- `CodeMatcher CreateLabelWithOffsets(int offset, out Label label)` - Creates a label at the given offset from the current position
- `CodeMatcher AddLabels(IEnumerable<Label> labels)` - Adds an enumeration of labels to current position
- `CodeMatcher AddLabelsAt(int position, IEnumerable<Label> labels)` - Adds an enumeration of labels at a position
- `CodeMatcher SetJumpTo(OpCode opcode, int destination, out Label label)` - Sets jump to
- `CodeMatcher Insert(params CodeInstruction[] instructions)` - Inserts some instructions at the current position
- `CodeMatcher Insert(IEnumerable<CodeInstruction> instructions)` - Inserts an enumeration of instructions at the current position
- `CodeMatcher InsertBranch(OpCode opcode, int destination)` - Inserts a branch at the current position
- `CodeMatcher InsertAndAdvance(params CodeInstruction[] instructions)` - Inserts some instructions at the current position and advances it
- `CodeMatcher InsertAndAdvance(IEnumerable<CodeInstruction> instructions)` - Inserts an enumeration of instructions at the current position and advances it
- `CodeMatcher InsertBranchAndAdvance(OpCode opcode, int destination)` - Inserts a branch at the current position and advances it
- `CodeMatcher InsertAfter(params CodeInstruction[] instructions)` - Inserts instructions immediately after the current position
- `CodeMatcher InsertAfter(IEnumerable<CodeInstruction> instructions)` - Inserts an enumeration of instructions immediately after the current position
- `CodeMatcher InsertBranchAfter(OpCode opcode, int destination)` - Inserts a branch instruction immediately after the current position
- `CodeMatcher InsertAfterAndAdvance(params CodeInstruction[] instructions)` - Inserts instructions immediately after the current position and advances to the last inserted instruction
- `CodeMatcher InsertAfterAndAdvance(IEnumerable<CodeInstruction> instructions)` - Inserts an enumeration of instructions immediately after the current position and advances to the last inserted instruction
- `CodeMatcher InsertBranchAfterAndAdvance(OpCode opcode, int destination)` - Inserts a branch instruction immediately after the current position and advances the position
- `CodeMatcher RemoveInstruction()` - Removes current instruction
- `CodeMatcher RemoveInstructions(int count)` - Removes some instruction from current position by count
- `CodeMatcher RemoveInstructionsInRange(int start, int end)` - Removes the instructions in a range
- `CodeMatcher RemoveInstructionsWithOffsets(int startOffset, int endOffset) => RemoveInstructionsInRange(Pos + startOffset, Pos + endOffset);` - Removes the instructions in an offset range
- `CodeMatcher Advance(int offset = 1)` - Advances the current position
- `CodeMatcher Start()` - Moves the current position to the start
- `CodeMatcher End()` - Moves the current position to the end
- `CodeMatcher SearchForward(Func<CodeInstruction, bool> predicate) => Search(predicate, 1);` - Searches forward with a predicate and advances position
- `CodeMatcher SearchBackwards(Func<CodeInstruction, bool> predicate) => Search(predicate, -1);` - Searches backwards with a predicate and moves the position
- `CodeMatcher MatchStartForward(params CodeMatch[] matches) => Match(matches, 1, MatchPosition.Start, false);` - Matches forward and advances position to beginning of matching sequence
- `CodeMatcher PrepareMatchStartForward(params CodeMatch[] matches) => Match(matches, 1, MatchPosition.Start, true);` - Prepares matching forward and advancing position to beginning of matching sequence
- `CodeMatcher MatchEndForward(params CodeMatch[] matches) => Match(matches, 1, MatchPosition.End, false);` - Matches forward and advances position to ending of matching sequence
- `CodeMatcher PrepareMatchEndForward(params CodeMatch[] matches) => Match(matches, 1, MatchPosition.End, true);` - Prepares matching forward and advancing position to ending of matching sequence
- `CodeMatcher MatchStartBackwards(params CodeMatch[] matches) => Match(matches, -1, MatchPosition.Start, false);` - Matches backwards and moves the position to beginning of matching sequence
- `CodeMatcher PrepareMatchStartBackwards(params CodeMatch[] matches) => Match(matches, -1, MatchPosition.Start, true);` - Prepares matching backwards and reversing position to beginning of matching sequence
- `CodeMatcher MatchEndBackwards(params CodeMatch[] matches) => Match(matches, -1, MatchPosition.End, false);` - Matches backwards and moves the position to ending of matching sequence
- `CodeMatcher PrepareMatchEndBackwards(params CodeMatch[] matches) => Match(matches, -1, MatchPosition.End, true);` - Prepares matching backwards and reversing position to ending of matching sequence
- `CodeMatcher RemoveSearchForward(Func<CodeInstruction, bool> predicate)` - Removes instructions from the current position forward until a predicate is matched. The matched instruction is not removed
- `CodeMatcher RemoveSearchBackward(Func<CodeInstruction, bool> predicate)` - Removes instructions from the current position backward until a predicate is matched. The matched instruction is not removed
- `CodeMatcher RemoveUntilForward(params CodeMatch[] matches)` - Removes instructions from the current position up to the next match (exclusive)
- `CodeMatcher RemoveUntilBackward(params CodeMatch[] matches)` - Removes instructions backwards from the current position to the previous match (exclusive)
- `CodeMatcher Repeat(Action<CodeMatcher> matchAction, Action<string> notFoundAction = null)` - Repeats a match action until boundaries are met
- `CodeInstruction NamedMatch(string name) => lastMatches[name];` - Gets a match by its name

Properties:
- `int Pos get; set; } = -1;` - The current position

Fields:
- `bool IsValid => Pos >= 0 && Pos < Length;` - Checks whether the position of this CodeMatcher is within bounds
- `bool IsInvalid => Pos < 0 || Pos >= Length;` - Checks whether the position of this CodeMatcher is outside its bounds

### Utilities

#### DelegateTypeFactory
A factory to create delegate types

Methods:
- `Type CreateDelegateType(MethodInfo method)` - Creates a delegate type for a method

#### Traverse
A reflection helper to read and write private elements

Methods:
- `Traverse Create(Type type) => new(type);` - Creates a new traverse instance from a class/type
- `Traverse Create<T>() => Create(typeof(T));` - Creates a new traverse instance from a class T
- `Traverse Create(object root) => new(root);` - Creates a new traverse instance from an instance
- `Traverse CreateWithType(string name) => new(AccessTools.TypeByName(name));` - Creates a new traverse instance from a named type
- `object GetValue()` - Gets the current value
- `object GetValue(params object[] arguments)` - Invokes the current method with arguments and returns the result
- `Traverse SetValue(object value)` - Sets a value of the current field or property
- `Type GetValueType()` - Gets the type of the current field or property
- `Traverse Type(string name)` - Moves the current traverse instance to a inner type
- `Traverse Field(string name)` - Moves the current traverse instance to a field
- `Traverse<T> Field<T>(string name) => new(Field(name));` - Moves the current traverse instance to a field
- `List<string> Fields()` - Gets all fields of the current type
- `Traverse Property(string name, object[] index = null)` - Moves the current traverse instance to a property
- `Traverse<T> Property<T>(string name, object[] index = null) => new(Property(name, index));` - Moves the current traverse instance to a field
- `List<string> Properties()` - Gets all properties of the current type
- `Traverse Method(string name, params object[] arguments)` - Moves the current traverse instance to a method
- `Traverse Method(string name, Type[] paramTypes, object[] arguments = null)` - Moves the current traverse instance to a method
- `List<string> Methods()` - Gets all methods of the current type
- `bool FieldExists() => _info is not null && _info is FieldInfo;` - Checks if the current traverse instance is for a field
- `bool PropertyExists() => _info is not null && _info is PropertyInfo;` - Checks if the current traverse instance is for a property
- `bool MethodExists() => _method is not null;` - Checks if the current traverse instance is for a method
- `bool TypeExists() => _type is not null;` - Checks if the current traverse instance is for a type
- `void IterateFields(object source, Action<Traverse> action)` - Iterates over all fields of the current type and executes a traverse action
- `void IterateFields(object source, object target, Action<Traverse, Traverse> action)` - Iterates over all fields of the current type and executes a traverse action
- `void IterateFields(object source, object target, Action<string, Traverse, Traverse> action)` - Iterates over all fields of the current type and executes a traverse action
- `void IterateProperties(object source, Action<Traverse> action)` - Iterates over all properties of the current type and executes a traverse action
- `void IterateProperties(object source, object target, Action<Traverse, Traverse> action)` - Iterates over all properties of the current type and executes a traverse action
- `void IterateProperties(object source, object target, Action<string, Traverse, Traverse> action)` - Iterates over all properties of the current type and executes a traverse action
- `string ToString()` - Returns a string that represents the current traverse

Fields:
- `Traverse(Traverse traverse) => this.traverse = traverse;` - Creates a traverse instance from an existing instance
- `Traverse(Type type) => _type = type;` - Creates a new traverse instance from a class/type
- `bool IsField => _info is FieldInfo;` - Checks if the current traverse instance is for a field
- `bool IsProperty => _info is PropertyInfo;` - Checks if the current traverse instance is for a property

### Exceptions

#### ExceptionBlockType
Exception block types

Methods:
- `ExceptionBlock(ExceptionBlockType blockType, Type catchType = null)` - Creates a new ExceptionBlock

Fields:
- `ExceptionBlockType blockType;` - Block type
- `Type catchType;` - Catch type

#### HarmonyException
Under Mono, HarmonyException wraps IL compile errors with detailed information about the failure

Methods:
- `List<KeyValuePair<int, CodeInstruction>> GetInstructionsWithOffsets() => [.. instructions.OrderBy(ins => ins.Key)];` - Get a list of IL instructions in pairs of offset+code
- `List<CodeInstruction> GetInstructions() => [.. instructions.OrderBy(ins => ins.Key).Select(ins => ins.Value)];` - Get a list of IL instructions without offsets
- `int GetErrorOffset() => errorOffset;` - Get the error offset of the errornous IL instruction
- `int GetErrorIndex()` - Get the index of the errornous IL instruction

### Other

#### HarmonyMethod
A wrapper around a method to use it as a patch (for example a Prefix)

Methods:
- `List<string> HarmonyFields()` - Gets the names of all internal patch info fields
- `HarmonyMethod Merge(List<HarmonyMethod> attributes)` - Merges annotations
- `string ToString()` - Returns a string that represents the annotation
- `void CopyTo(this HarmonyMethod from, HarmonyMethod to)` - Copies annotation information
- `HarmonyMethod Clone(this HarmonyMethod original)` - Clones an annotation
- `HarmonyMethod Merge(this HarmonyMethod master, HarmonyMethod detail)` - Merges annotations
- `List<HarmonyMethod> GetFromType(Type type)` - Gets all annotations on a class/type
- `HarmonyMethod GetMergedFromType(Type type) => HarmonyMethod.Merge(GetFromType(type));` - Gets merged annotations on a class/type
- `List<HarmonyMethod> GetFromMethod(MethodBase method)` - Gets all annotations on a method
- `HarmonyMethod GetMergedFromMethod(MethodBase method) => HarmonyMethod.Merge(GetFromMethod(method));` - Gets merged annotations on a method

Fields:
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
Fields:
- `int[] positions;` - Which occcurances (1-based) of the method, negative numbers are counting from the end, empty array means all occurances

#### Operand_
Methods:
- `Nop_ Nop => new(OpCodes.Nop);`
- `Break_ Break => new(OpCodes.Break);`
- `Ldarg_0_ Ldarg_0 => new(OpCodes.Ldarg_0);`
- `Ldarg_1_ Ldarg_1 => new(OpCodes.Ldarg_1);`
- `Ldarg_2_ Ldarg_2 => new(OpCodes.Ldarg_2);`
- `Ldarg_3_ Ldarg_3 => new(OpCodes.Ldarg_3);`
- `Ldloc_0_ Ldloc_0 => new(OpCodes.Ldloc_0);`
- `Ldloc_1_ Ldloc_1 => new(OpCodes.Ldloc_1);`
- `Ldloc_2_ Ldloc_2 => new(OpCodes.Ldloc_2);`
- `Ldloc_3_ Ldloc_3 => new(OpCodes.Ldloc_3);`
- `Stloc_0_ Stloc_0 => new(OpCodes.Stloc_0);`
- `Stloc_1_ Stloc_1 => new(OpCodes.Stloc_1);`
- `Stloc_2_ Stloc_2 => new(OpCodes.Stloc_2);`
- `Stloc_3_ Stloc_3 => new(OpCodes.Stloc_3);`
- `Ldarg_S_ Ldarg_S => new(OpCodes.Ldarg_S);`
- `Ldarga_S_ Ldarga_S => new(OpCodes.Ldarga_S);`
- `Starg_S_ Starg_S => new(OpCodes.Starg_S);`
- `Ldloc_S_ Ldloc_S => new(OpCodes.Ldloc_S);`
- `Ldloca_S_ Ldloca_S => new(OpCodes.Ldloca_S);`
- `Stloc_S_ Stloc_S => new(OpCodes.Stloc_S);`
- `Ldnull_ Ldnull => new(OpCodes.Ldnull);`
- `Ldc_I4_M1_ Ldc_I4_M1 => new(OpCodes.Ldc_I4_M1);`
- `Ldc_I4_0_ Ldc_I4_0 => new(OpCodes.Ldc_I4_0);`
- `Ldc_I4_1_ Ldc_I4_1 => new(OpCodes.Ldc_I4_1);`
- `Ldc_I4_2_ Ldc_I4_2 => new(OpCodes.Ldc_I4_2);`
- `Ldc_I4_3_ Ldc_I4_3 => new(OpCodes.Ldc_I4_3);`
- `Ldc_I4_4_ Ldc_I4_4 => new(OpCodes.Ldc_I4_4);`
- `Ldc_I4_5_ Ldc_I4_5 => new(OpCodes.Ldc_I4_5);`
- `Ldc_I4_6_ Ldc_I4_6 => new(OpCodes.Ldc_I4_6);`
- `Ldc_I4_7_ Ldc_I4_7 => new(OpCodes.Ldc_I4_7);`
- `Ldc_I4_8_ Ldc_I4_8 => new(OpCodes.Ldc_I4_8);`
- `Ldc_I4_S_ Ldc_I4_S => new(OpCodes.Ldc_I4_S);`
- `Ldc_I4_ Ldc_I4 => new(OpCodes.Ldc_I4);`
- `Ldc_I8_ Ldc_I8 => new(OpCodes.Ldc_I8);`
- `Ldc_R4_ Ldc_R4 => new(OpCodes.Ldc_R4);`
- `Ldc_R8_ Ldc_R8 => new(OpCodes.Ldc_R8);`
- `Dup_ Dup => new(OpCodes.Dup);`
- `Pop_ Pop => new(OpCodes.Pop);`
- `Jmp_ Jmp => new(OpCodes.Jmp);`
- `Call_ Call => new(OpCodes.Call);`
- `Calli_ Calli => new(OpCodes.Calli);`
- `Ret_ Ret => new(OpCodes.Ret);`
- `Br_S_ Br_S => new(OpCodes.Br_S);`
- `Brfalse_S_ Brfalse_S => new(OpCodes.Brfalse_S);`
- `Brtrue_S_ Brtrue_S => new(OpCodes.Brtrue_S);`
- `Beq_S_ Beq_S => new(OpCodes.Beq_S);`
- `Bge_S_ Bge_S => new(OpCodes.Bge_S);`
- `Bgt_S_ Bgt_S => new(OpCodes.Bgt_S);`
- `Ble_S_ Ble_S => new(OpCodes.Ble_S);`
- `Blt_S_ Blt_S => new(OpCodes.Blt_S);`
- `Bne_Un_S_ Bne_Un_S => new(OpCodes.Bne_Un_S);`
- `Bge_Un_S_ Bge_Un_S => new(OpCodes.Bge_Un_S);`
- `Bgt_Un_S_ Bgt_Un_S => new(OpCodes.Bgt_Un_S);`
- `Ble_Un_S_ Ble_Un_S => new(OpCodes.Ble_Un_S);`
- `Blt_Un_S_ Blt_Un_S => new(OpCodes.Blt_Un_S);`
- `Br_ Br => new(OpCodes.Br);`
- `Brfalse_ Brfalse => new(OpCodes.Brfalse);`
- `Brtrue_ Brtrue => new(OpCodes.Brtrue);`
- `Beq_ Beq => new(OpCodes.Beq);`
- `Bge_ Bge => new(OpCodes.Bge);`
- `Bgt_ Bgt => new(OpCodes.Bgt);`
- `Ble_ Ble => new(OpCodes.Ble);`
- `Blt_ Blt => new(OpCodes.Blt);`
- `Bne_Un_ Bne_Un => new(OpCodes.Bne_Un);`
- `Bge_Un_ Bge_Un => new(OpCodes.Bge_Un);`
- `Bgt_Un_ Bgt_Un => new(OpCodes.Bgt_Un);`
- `Ble_Un_ Ble_Un => new(OpCodes.Ble_Un);`
- `Blt_Un_ Blt_Un => new(OpCodes.Blt_Un);`
- `Switch_ Switch => new(OpCodes.Switch);`
- `Ldind_I1_ Ldind_I1 => new(OpCodes.Ldind_I1);`
- `Ldind_U1_ Ldind_U1 => new(OpCodes.Ldind_U1);`
- `Ldind_I2_ Ldind_I2 => new(OpCodes.Ldind_I2);`
- `Ldind_U2_ Ldind_U2 => new(OpCodes.Ldind_U2);`
- `Ldind_I4_ Ldind_I4 => new(OpCodes.Ldind_I4);`
- `Ldind_U4_ Ldind_U4 => new(OpCodes.Ldind_U4);`
- `Ldind_I8_ Ldind_I8 => new(OpCodes.Ldind_I8);`
- `Ldind_I_ Ldind_I => new(OpCodes.Ldind_I);`
- `Ldind_R4_ Ldind_R4 => new(OpCodes.Ldind_R4);`
- `Ldind_R8_ Ldind_R8 => new(OpCodes.Ldind_R8);`
- `Ldind_Ref_ Ldind_Ref => new(OpCodes.Ldind_Ref);`
- `Stind_Ref_ Stind_Ref => new(OpCodes.Stind_Ref);`
- `Stind_I1_ Stind_I1 => new(OpCodes.Stind_I1);`
- `Stind_I2_ Stind_I2 => new(OpCodes.Stind_I2);`
- `Stind_I4_ Stind_I4 => new(OpCodes.Stind_I4);`
- `Stind_I8_ Stind_I8 => new(OpCodes.Stind_I8);`
- `Stind_R4_ Stind_R4 => new(OpCodes.Stind_R4);`
- `Stind_R8_ Stind_R8 => new(OpCodes.Stind_R8);`
- `Add_ Add => new(OpCodes.Add);`
- `Sub_ Sub => new(OpCodes.Sub);`
- `Mul_ Mul => new(OpCodes.Mul);`
- `Div_ Div => new(OpCodes.Div);`
- `Div_Un_ Div_Un => new(OpCodes.Div_Un);`
- `Rem_ Rem => new(OpCodes.Rem);`
- `Rem_Un_ Rem_Un => new(OpCodes.Rem_Un);`
- `And_ And => new(OpCodes.And);`
- `Or_ Or => new(OpCodes.Or);`
- `Xor_ Xor => new(OpCodes.Xor);`
- `Shl_ Shl => new(OpCodes.Shl);`
- `Shr_ Shr => new(OpCodes.Shr);`
- `Shr_Un_ Shr_Un => new(OpCodes.Shr_Un);`
- `Neg_ Neg => new(OpCodes.Neg);`
- `Not_ Not => new(OpCodes.Not);`
- `Conv_I1_ Conv_I1 => new(OpCodes.Conv_I1);`
- `Conv_I2_ Conv_I2 => new(OpCodes.Conv_I2);`
- `Conv_I4_ Conv_I4 => new(OpCodes.Conv_I4);`
- `Conv_I8_ Conv_I8 => new(OpCodes.Conv_I8);`
- `Conv_R4_ Conv_R4 => new(OpCodes.Conv_R4);`
- `Conv_R8_ Conv_R8 => new(OpCodes.Conv_R8);`
- `Conv_U4_ Conv_U4 => new(OpCodes.Conv_U4);`
- `Conv_U8_ Conv_U8 => new(OpCodes.Conv_U8);`
- `Callvirt_ Callvirt => new(OpCodes.Callvirt);`
- `Cpobj_ Cpobj => new(OpCodes.Cpobj);`
- `Ldobj_ Ldobj => new(OpCodes.Ldobj);`
- `Ldstr_ Ldstr => new(OpCodes.Ldstr);`
- `Newobj_ Newobj => new(OpCodes.Newobj);`
- `Castclass_ Castclass => new(OpCodes.Castclass);`
- `Isinst_ Isinst => new(OpCodes.Isinst);`
- `Conv_R_Un_ Conv_R_Un => new(OpCodes.Conv_R_Un);`
- `Unbox_ Unbox => new(OpCodes.Unbox);`
- `Throw_ Throw => new(OpCodes.Throw);`
- `Ldfld_ Ldfld => new(OpCodes.Ldfld);`
- `Ldflda_ Ldflda => new(OpCodes.Ldflda);`
- `Stfld_ Stfld => new(OpCodes.Stfld);`
- `Ldsfld_ Ldsfld => new(OpCodes.Ldsfld);`
- `Ldsflda_ Ldsflda => new(OpCodes.Ldsflda);`
- `Stsfld_ Stsfld => new(OpCodes.Stsfld);`
- `Stobj_ Stobj => new(OpCodes.Stobj);`
- `Conv_Ovf_I1_Un_ Conv_Ovf_I1_Un => new(OpCodes.Conv_Ovf_I1_Un);`
- `Conv_Ovf_I2_Un_ Conv_Ovf_I2_Un => new(OpCodes.Conv_Ovf_I2_Un);`
- `Conv_Ovf_I4_Un_ Conv_Ovf_I4_Un => new(OpCodes.Conv_Ovf_I4_Un);`
- `Conv_Ovf_I8_Un_ Conv_Ovf_I8_Un => new(OpCodes.Conv_Ovf_I8_Un);`
- `Conv_Ovf_U1_Un_ Conv_Ovf_U1_Un => new(OpCodes.Conv_Ovf_U1_Un);`
- `Conv_Ovf_U2_Un_ Conv_Ovf_U2_Un => new(OpCodes.Conv_Ovf_U2_Un);`
- `Conv_Ovf_U4_Un_ Conv_Ovf_U4_Un => new(OpCodes.Conv_Ovf_U4_Un);`
- `Conv_Ovf_U8_Un_ Conv_Ovf_U8_Un => new(OpCodes.Conv_Ovf_U8_Un);`
- `Conv_Ovf_I_Un_ Conv_Ovf_I_Un => new(OpCodes.Conv_Ovf_I_Un);`
- `Conv_Ovf_U_Un_ Conv_Ovf_U_Un => new(OpCodes.Conv_Ovf_U_Un);`
- `Box_ Box => new(OpCodes.Box);`
- `Newarr_ Newarr => new(OpCodes.Newarr);`
- `Ldlen_ Ldlen => new(OpCodes.Ldlen);`
- `Ldelema_ Ldelema => new(OpCodes.Ldelema);`
- `Ldelem_I1_ Ldelem_I1 => new(OpCodes.Ldelem_I1);`
- `Ldelem_U1_ Ldelem_U1 => new(OpCodes.Ldelem_U1);`
- `Ldelem_I2_ Ldelem_I2 => new(OpCodes.Ldelem_I2);`
- `Ldelem_U2_ Ldelem_U2 => new(OpCodes.Ldelem_U2);`
- `Ldelem_I4_ Ldelem_I4 => new(OpCodes.Ldelem_I4);`
- `Ldelem_U4_ Ldelem_U4 => new(OpCodes.Ldelem_U4);`
- `Ldelem_I8_ Ldelem_I8 => new(OpCodes.Ldelem_I8);`
- `Ldelem_I_ Ldelem_I => new(OpCodes.Ldelem_I);`
- `Ldelem_R4_ Ldelem_R4 => new(OpCodes.Ldelem_R4);`
- `Ldelem_R8_ Ldelem_R8 => new(OpCodes.Ldelem_R8);`
- `Ldelem_Ref_ Ldelem_Ref => new(OpCodes.Ldelem_Ref);`
- `Stelem_I_ Stelem_I => new(OpCodes.Stelem_I);`
- `Stelem_I1_ Stelem_I1 => new(OpCodes.Stelem_I1);`
- `Stelem_I2_ Stelem_I2 => new(OpCodes.Stelem_I2);`
- `Stelem_I4_ Stelem_I4 => new(OpCodes.Stelem_I4);`
- `Stelem_I8_ Stelem_I8 => new(OpCodes.Stelem_I8);`
- `Stelem_R4_ Stelem_R4 => new(OpCodes.Stelem_R4);`
- `Stelem_R8_ Stelem_R8 => new(OpCodes.Stelem_R8);`
- `Stelem_Ref_ Stelem_Ref => new(OpCodes.Stelem_Ref);`
- `Ldelem_ Ldelem => new(OpCodes.Ldelem);`
- `Stelem_ Stelem => new(OpCodes.Stelem);`
- `Unbox_Any_ Unbox_Any => new(OpCodes.Unbox_Any);`
- `Conv_Ovf_I1_ Conv_Ovf_I1 => new(OpCodes.Conv_Ovf_I1);`
- `Conv_Ovf_U1_ Conv_Ovf_U1 => new(OpCodes.Conv_Ovf_U1);`
- `Conv_Ovf_I2_ Conv_Ovf_I2 => new(OpCodes.Conv_Ovf_I2);`
- `Conv_Ovf_U2_ Conv_Ovf_U2 => new(OpCodes.Conv_Ovf_U2);`
- `Conv_Ovf_I4_ Conv_Ovf_I4 => new(OpCodes.Conv_Ovf_I4);`
- `Conv_Ovf_U4_ Conv_Ovf_U4 => new(OpCodes.Conv_Ovf_U4);`
- `Conv_Ovf_I8_ Conv_Ovf_I8 => new(OpCodes.Conv_Ovf_I8);`
- `Conv_Ovf_U8_ Conv_Ovf_U8 => new(OpCodes.Conv_Ovf_U8);`
- `Refanyval_ Refanyval => new(OpCodes.Refanyval);`
- `Ckfinite_ Ckfinite => new(OpCodes.Ckfinite);`
- `Mkrefany_ Mkrefany => new(OpCodes.Mkrefany);`
- `Ldtoken_ Ldtoken => new(OpCodes.Ldtoken);`
- `Conv_U2_ Conv_U2 => new(OpCodes.Conv_U2);`
- `Conv_U1_ Conv_U1 => new(OpCodes.Conv_U1);`
- `Conv_I_ Conv_I => new(OpCodes.Conv_I);`
- `Conv_Ovf_I_ Conv_Ovf_I => new(OpCodes.Conv_Ovf_I);`
- `Conv_Ovf_U_ Conv_Ovf_U => new(OpCodes.Conv_Ovf_U);`
- `Add_Ovf_ Add_Ovf => new(OpCodes.Add_Ovf);`
- `Add_Ovf_Un_ Add_Ovf_Un => new(OpCodes.Add_Ovf_Un);`
- `Mul_Ovf_ Mul_Ovf => new(OpCodes.Mul_Ovf);`
- `Mul_Ovf_Un_ Mul_Ovf_Un => new(OpCodes.Mul_Ovf_Un);`
- `Sub_Ovf_ Sub_Ovf => new(OpCodes.Sub_Ovf);`
- `Sub_Ovf_Un_ Sub_Ovf_Un => new(OpCodes.Sub_Ovf_Un);`
- `Endfinally_ Endfinally => new(OpCodes.Endfinally);`
- `Leave_ Leave => new(OpCodes.Leave);`
- `Leave_S_ Leave_S => new(OpCodes.Leave_S);`
- `Stind_I_ Stind_I => new(OpCodes.Stind_I);`
- `Conv_U_ Conv_U => new(OpCodes.Conv_U);`
- `Prefix7_ Prefix7 => new(OpCodes.Prefix7);`
- `Prefix6_ Prefix6 => new(OpCodes.Prefix6);`
- `Prefix5_ Prefix5 => new(OpCodes.Prefix5);`
- `Prefix4_ Prefix4 => new(OpCodes.Prefix4);`
- `Prefix3_ Prefix3 => new(OpCodes.Prefix3);`
- `Prefix2_ Prefix2 => new(OpCodes.Prefix2);`
- `Prefix1_ Prefix1 => new(OpCodes.Prefix1);`
- `Prefixref_ Prefixref => new(OpCodes.Prefixref);`
- `Arglist_ Arglist => new(OpCodes.Arglist);`
- `Ceq_ Ceq => new(OpCodes.Ceq);`
- `Cgt_ Cgt => new(OpCodes.Cgt);`
- `Cgt_Un_ Cgt_Un => new(OpCodes.Cgt_Un);`
- `Clt_ Clt => new(OpCodes.Clt);`
- `Clt_Un_ Clt_Un => new(OpCodes.Clt_Un);`
- `Ldftn_ Ldftn => new(OpCodes.Ldftn);`
- `Ldvirtftn_ Ldvirtftn => new(OpCodes.Ldvirtftn);`
- `Ldarg_ Ldarg => new(OpCodes.Ldarg);`
- `Ldarga_ Ldarga => new(OpCodes.Ldarga);`
- `Starg_ Starg => new(OpCodes.Starg);`
- `Ldloc_ Ldloc => new(OpCodes.Ldloc);`
- `Ldloca_ Ldloca => new(OpCodes.Ldloca);`
- `Stloc_ Stloc => new(OpCodes.Stloc);`
- `Localloc_ Localloc => new(OpCodes.Localloc);`
- `Endfilter_ Endfilter => new(OpCodes.Endfilter);`
- `Unaligned_ Unaligned => new(OpCodes.Unaligned);`
- `Volatile_ Volatile => new(OpCodes.Volatile);`
- `Tailcall_ Tailcall => new(OpCodes.Tailcall);`
- `Initobj_ Initobj => new(OpCodes.Initobj);`
- `Constrained_ Constrained => new(OpCodes.Constrained);`
- `Cpblk_ Cpblk => new(OpCodes.Cpblk);`
- `Initblk_ Initblk => new(OpCodes.Initblk);`
- `Rethrow_ Rethrow => new(OpCodes.Rethrow);`
- `Sizeof_ Sizeof => new(OpCodes.Sizeof);`
- `Refanytype_ Refanytype => new(OpCodes.Refanytype);`
- `Readonly_ Readonly => new(OpCodes.Readonly);`

## Usage Patterns

### Accessing Private Members
```csharp
var originalMethods = Harmony.GetAllPatchedMethods();
			foreach (var method in originalMethods) { }
```

### Postfix Examples
```csharp
public class OriginalCode
		{
			public string GetName() => name; // ...
		}

		[HarmonyPatch(typeof(OriginalCode), nameof(OriginalCode.GetName))]
		class Patch
		{
			static void Postfix(ref string __result)
			{
				if (__result == "foo")
					__result = "bar";
			}
		}
```

### Transpiler Example
```csharp
static FieldInfo f_someField = AccessTools.Field(typeof(SomeType), nameof(SomeType.someField));
		static MethodInfo m_MyExtraMethod = SymbolExtensions.GetMethodInfo(() => Tools.MyExtraMethod());

		// looks for STDFLD someField and inserts CALL MyExtraMethod before it
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var found = false;
			foreach (var instruction in instructions)
			{
				if (instruction.StoresField(f_someField))
				{
					yield return new CodeInstruction(OpCodes.Call, m_MyExtraMethod);
					found = true;
				}
				yield return instruction;
			}
			if (found is false)
				ReportError("Cannot find <Stdfld someField> in OriginalType.OriginalMethod");
		}
```

### Debug and Logging
```csharp
Harmony.DEBUG = true;
FileLog.Log("something");
			// or buffered:
			FileLog.LogBuffered("A");
			FileLog.LogBuffered("B");
			FileLog.FlushBuffer(); /* don't forget to flush */
```

### Patch Management
```csharp
// get the MethodBase of the original
				var original = typeof(TheClass).GetMethod("TheMethod");

				// retrieve all patches
				var patches = Harmony.GetPatchInfo(original);
				if (patches is null) return; // not patched

				// get a summary of all different Harmony ids involved
				FileLog.Log("all owners: " + patches.Owners);

				// get info about all Prefixes/Postfixes/Transpilers
				foreach (var patch in patches.Prefixes)
				{
					FileLog.Log("index: " + patch.index);
					FileLog.Log("owner: " + patch.owner);
					FileLog.Log("patch method: " + patch.PatchMethod);
					FileLog.Log("priority: " + patch.priority);
					FileLog.Log("before: " + patch.before);
					FileLog.Log("after: " + patch.after);
				}
```

## Patch Types Reference
- **Prefix**: Runs before original, can skip original if returns false
- **Postfix**: Runs after original, can access/modify results  
- **Transpiler**: Modifies IL code of original for advanced patching
- **Finalizer**: Runs after original regardless of exceptions
- **Reverse Patch**: Copy/modify original method logic

## Important Notes for AI Assistants
1. Always create unique Harmony IDs (e.g., "com.yourname.yourmod")
2. Use try-catch around Harmony operations in production
3. Prefer attribute-based patches for maintainability
4. Use AccessTools for reflection instead of raw Reflection API
5. Test patches thoroughly - wrong patches can crash applications
6. Use HarmonyDebug attribute for debugging specific patches

## Full Documentation
https://harmony.pardeike.net | https://github.com/pardeike/Harmony
