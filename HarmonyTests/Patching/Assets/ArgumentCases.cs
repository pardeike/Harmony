using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Assets
{
	public class ArgumentTypes
	{
		public class Object { }
		public struct Value { public int n; }
	}

	public class ArgumentOriginalMethods
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Object_2_Object(ArgumentTypes.Object p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Object_2_ObjectRef(ArgumentTypes.Object p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ObjectRef_2_Object(ref ArgumentTypes.Object p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ObjectRef_2_ObjectRef(ref ArgumentTypes.Object p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_Value(ArgumentTypes.Value p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_Boxing(ArgumentTypes.Value p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_ValueRef(ArgumentTypes.Value p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_BoxingRef(ArgumentTypes.Value p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_Value(ref ArgumentTypes.Value p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_Boxing(ref ArgumentTypes.Value p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_ValueRef(ref ArgumentTypes.Value p) => Console.WriteLine("ok");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_BoxingRef(ref ArgumentTypes.Value p) => Console.WriteLine("ok");
	}

	public static class ArgumentPatchMethods
	{
		public static string result;

		public static void Reset() => result = "";

		public static void To_Object(ArgumentTypes.Object p) => result += p.GetType().Name[0];

		public static void To_Value(ArgumentTypes.Value p) => result += p.GetType().Name[0];

		public static void To_Boxing(object p) => result += p.GetType().Name[0];

		public static void To_ObjectRef(ref ArgumentTypes.Object p) => result += p.GetType().Name[0];

		public static void To_ValueRef(ref ArgumentTypes.Value p)
		{
			result += p.GetType().Name[0];
			p.n = 101;
		}

		public static void To_BoxingRef(ref object p)
		{
			result += p.GetType().Name[0];
			_ = Traverse.Create(p).Field("n").SetValue(102);
		}
	}

	public class SimpleArgumentArrayUsage
	{
		public static int n;
		public static string s;
		public static SomeStruct st;
		public static float[] f;

		public struct SomeStruct
		{
			public int n;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method(int n, string s, SomeStruct st, float[] f)
		{
			SimpleArgumentArrayUsage.n = n;
			SimpleArgumentArrayUsage.s = s;
			SimpleArgumentArrayUsage.st = st;
			SimpleArgumentArrayUsage.f = f;
		}
	}

	[HarmonyPatch(typeof(SimpleArgumentArrayUsage), nameof(SimpleArgumentArrayUsage.Method))]
	public static class SimpleArgumentArrayUsagePatch
	{
		public static void Prefix(object[] __args)
		{
			__args[0] = 123;
			__args[1] = "patched";
			__args[2] = new SimpleArgumentArrayUsage.SomeStruct() { n = 456 };
			__args[3] = new float[] { 1.2f, 3.4f, 5.6f };
		}
	}

	public class RenamedArguments
	{
		public string val;

		public RenamedArguments() => val = "val";

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method(string name) => RenamedArgumentsPatch.log.Add(name);
	}

	[HarmonyPatch(typeof(RenamedArguments), nameof(RenamedArguments.Method))]
	[HarmonyArgument("foo2", "__state")]
	public static class RenamedArgumentsPatch
	{
		public static List<string> log = [];

		[HarmonyArgument("foo1", "name")]
		[HarmonyArgument("instance", "__instance")]
		public static void Prefix(RenamedArguments instance, ref string foo1, out string foo2)
		{
			log.Add(instance.val + "1");
			foo1 = "patched";
			foo2 = "hello";
		}

		public static void Postfix([HarmonyArgument("__instance")] RenamedArguments foo2, [HarmonyArgument("__state")] string foo3)
		{
			log.Add(foo2.val + "2");
			log.Add(foo3);
		}
	}

	public class NullableResults
	{
		private string s = "foo";

		[MethodImpl(MethodImplOptions.NoInlining)]
		public bool? Method()
		{
			_ = s;
			return false;
		}
	}

	[HarmonyPatch(typeof(NullableResults), nameof(NullableResults.Method))]
	public static class NullableResultsPatch
	{
		public static bool Prefix(ref bool? __result)
		{
			__result = true;
			return false;
		}
	}

	public class ArgumentArrayMethods
	{
		public struct SomeStruct
		{
			public int n;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method(
			int n1, ref int n2, out int n3,
			string s1, ref string s2, out string s3,
			SomeStruct st1, ref SomeStruct st2, out SomeStruct st3,
			float[] f1, ref float[] f2, out float[] f3
		)
		{
			n2 = 12;
			n3 = 45;
			s2 = "ab";
			s3 = "de";
			st2 = new SomeStruct() { n = 12 };
			st3 = new SomeStruct() { n = 45 };
			f2 = [1f, 3f, 5f];
			f3 = [2f, 4f, 6f];
		}
	}

	[HarmonyPatch(typeof(ArgumentArrayMethods), nameof(ArgumentArrayMethods.Method))]
	public static class ArgumentArrayPatches
	{
		public static object[] prefixInput;
		public static object[] postfixInput;

		public static bool Prefix(object[] __args)
		{
			prefixInput = (object[])Array.CreateInstance(typeof(object), __args.Length);
			Array.Copy(__args, prefixInput, __args.Length);

			__args[1] = 123;
			__args[2] = 456;

			__args[4] = "abc";
			__args[5] = "def";

			__args[7] = new ArgumentArrayMethods.SomeStruct() { n = 123 };
			__args[8] = new ArgumentArrayMethods.SomeStruct() { n = 456 };

			__args[10] = new float[] { 1.2f, 3.4f, 5.6f };
			__args[11] = new float[] { 2.1f, 4.3f, 6.5f };

			return false;
		}

		public static void Postfix(
			int n1, int n2, int n3,
			string s1, string s2, string s3,
			ArgumentArrayMethods.SomeStruct st1, ArgumentArrayMethods.SomeStruct st2, ArgumentArrayMethods.SomeStruct st3,
			float[] f1, float[] f2, float[] f3
		)
		{
			postfixInput =
			[
				n1, n2, n3,
				s1, s2, s3,
				st1, st2, st3,
				f1, f2, f3
			];
		}
	}
}
