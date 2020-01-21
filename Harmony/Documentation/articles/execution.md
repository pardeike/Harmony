# Execution Flow

Patching a method does not override any previous patches that other users of Harmony apply to the same method. Instead, **Prefix**, **Postfix**, **Transpiler** and **Finalizer** patches are executed around and inside code from the original method in an adaptive and prioritised way.

**Prefix** patches can return a boolean that, if false, skips prefixes that alter the result of the original and skips the execution of the original method too. In contrast, **Postfix** patches are executed all the time except when the the original or any patch to it throws an exception.

All the original code is inserted into the replacement method together with all calls to all patches. The original code can be changed by **Transpiler** patches that are applied to the code in sequence before its inserted.

If you need guaranteed execution after the original or want to catch exceptions or alter thrown exceptions, you use a **Finalizer** patch.

The overall structure of the replacement method (original after patching) can be explained best by looking at a pseudo code example of the method that will replace the original anytime someone adds or removes a patch:

### Anatomy of a patched method

##### Without Finalizer patches

The basic logic of a Harmony replacement method consists of calling all Prefix methods first, then calling the (possibly transpiled) original and then all Postfix methods.

To skip the Original, a prefix can return `false` to skip the Original and all other Prefix methods that come after it and that have some effect on the Original.

Prefix methods that return `void` and have no `ref` arguments are considered side effect free and are always run, regardless of their position. Postfix methods are always executed.

Exceptions thrown in a Prefix, a Postfix or in the modified Original method will not be caught by default and will reach the caller of the Original method. If you want to handle exceptions, you need to use Finalizer patches.

```csharp

// while patching, the method ModifiedOriginal is created by chaining
// all transpilers. This happens only once when you patch, not during runtime
//
// var codes = GetCodeFromOriginal(originalMethod);
// codes = Transpiler1(codes);
// codes = Transpiler2(codes);
// codes = Transpiler3(codes);
// static ModifiedOriginal = GenerateDynamicMethod(codes);

static R ReplacementMethod(T optionalThisArgument, ...arguments)
{
	R result = default;
	bool run = true;

	// Harmony separates all Prefix patches into those that change the
	// original methods result/execution and those who have no side efects
	// Lets call all prefixes with no side effect "SimplePrefix" and add
	// a number to them that indicates their sort order after applying
	// priorities to them:

	SimplePrefix1(arguments);
	if (run) run = Prefix2();
	SimplePrefix3(arguments);
	SimplePrefix4(arguments);
	if (run) Prefix5(ref someArgument, ref result);
	// ...

	if (run) result = ModifiedOriginal(arguments);

	Postfix1(ref result)
	result = Postfix2(result, ...arguments)
	Postfix3()
	// ...

	return result
}
```

##### With Finalizer patches

Normally, Harmony does not introduce the overhead of try/catch to the replacement method. When you start adding Finalizer patches the overall logic becomes a lot more complicated which is illustrated in the following pseudeo code example.

For simplicity, Prefix and Postfix patches can be considered part of the Original and are not shown here:

```csharp
static R ReplacementMethod(T optionalThisArgument, ...arguments)
{
	R result = default;
	bool finalized = false;
	Exception ex = null;

	// All this code is generated dynamically, which means that
	// Harmony can build it depending on
	//
	// - if there are any finalizers (otherwise, skip try-catch)
	//
	// - re-throwing can be dynamic too depending on if at least
	//   one finalizer returns a non-void result

	try
	{
		result = Original(arguments);

		// finalizers get all the arguments a prefix could get too
		// plus one new one: "Exception __exception"
		// they SHOULD NOT edit the passed exception but instead
		// signal to Harmony that they change it by returning it

		// here finalizers are called without try-catch so they are
		// allowed to throw exceptions. note, that it is perfectly
		// fine to get null passed into the exception argument

		SimpleFinalizer(ref result);
		ex = EditFinalizer(ex, ref result);
		finalized = true;

		if (ex != null) throw ex;
		return result;
	}
	catch(Exception e)
	{
		ex = e;

		// finalizers will get another chance here, so they are
		// guaranteed to run even if their first invocation threw
		// an exception

		if (!finalized)
		{
			try { SimpleFinalizer(ref result); } catch { }
			try { ex = EditFinalizer(ex, ref result); } catch { }
		}

		// alternative 1: all finalizers are returning void
		throw;

		// alternative 2: at least one non-void finalizer
		if (ex != null) throw ex;

		return result;
	}
}

// given the following signatures:
public static String Original() { return "original"; }
public static void SimpleFinalizer(ref string result) { }
public static Exception EditFinalizer(Exception ex, ref string result) { return ex; }
```
