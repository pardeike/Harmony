# Guides

Install Harmony and choose a patch type, or jump to the feature you need.

## Start here

- [Introduction](intro.md): how Harmony works and what your runtime needs to support.
- [Install and apply patches](basics.md): add the library, register patches, and remove them.
- [Choose a patch type](patching.md): find the right API for the change you want to make.
- [What's new in v3](new.md): Infix, generated bodies, and state across await and yield.

## Patch methods

- [Prefix](patching-prefix.md): change arguments, supply a result, or skip the original.
- [Postfix](patching-postfix.md): read or change the result after completion or a skip.
- [Finalizer](patching-finalizer.md): run cleanup on success or failure and handle exceptions.
- [Reverse patch](reverse-patching.md): call a frozen copy of an implementation from your own stub.

## Infix

- [Patch an operation](patching-infix.md): apply prefixes, postfixes, and finalizers inside a method.
- [Recipes and instruction edits](patching-infix-authoring.md): insert, replace, or remove operations and manage groups of patches.
- [Limits and unusual cases](patching-infix-limits.md): understand selection, argument scope, and state lifetime.

## Transpilers

- [Rewrite instructions](patching-transpiler.md): edit a method's compiled instructions.
- [CodeInstruction](patching-transpiler-codes.md): work with operands, labels, and exception boundaries.
- [CodeMatcher](patching-transpiler-matcher.md): find instruction patterns and edit them.

## Coordinate your patches

- [Annotations and targets](annotations.md): declare target methods and organize patch classes.
- [Injected values](patching-injections.md): access arguments, results, instances, and shared state.
- [Ordering and priorities](priorities.md): control the order of patches from different authors.
- [Execution flow](execution.md): see how Harmony combines the patches on a method.
- [Prepare, target, and clean up](patching-auxiliary.md): control patch installation with helper methods.

## Reference guides

- [Reflection and logging](utilities.md): find members, read private values, and write diagnostic logs.
- [Runtime edge cases](patching-edgecases.md): account for inlining, generics, and native methods.

For public types, signatures, and parameters, use the [API reference](../api/index.md).
