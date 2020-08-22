namespace Execution_Without
{
	class Code
	{
		// <example>

		// while patching, the method ModifiedOriginal is created by chaining
		// all transpilers. This happens only once when you patch, not during runtime
		//
		// var codes = GetCodeFromOriginal(originalMethod);
		// codes = Transpiler1(codes);
		// codes = Transpiler2(codes);
		// codes = Transpiler3(codes);
		// static ModifiedOriginal = GenerateDynamicMethod(codes);

		static R ReplacementMethod(T optionalThisArgument, params object[] arguments)
		{
			R result = default;
			var run = true;

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

			Postfix1(ref result);
			result = Postfix2(result, arguments);
			Postfix3();
			// ...

			return result;
		}
		// </example>

		class T { }
		class R { }
		static int someArgument;

		static void SimplePrefix1(params object[] arguments) { }
		static bool Prefix2() { return true; }
		static void SimplePrefix3(params object[] arguments) { }
		static void SimplePrefix4(params object[] arguments) { }
		static bool Prefix5(ref int i, ref R r) { return true; }
		static R ModifiedOriginal(params object[] arguments) { return null; }
		static void Postfix1(ref R r) { }
		static R Postfix2(R r, params object[] arguments) { return r; }
		static void Postfix3() { }
	}
}
