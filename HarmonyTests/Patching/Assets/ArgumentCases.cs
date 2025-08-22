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
		public void Object_2_Object(ArgumentTypes.Object p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Object_2_ObjectRef(ArgumentTypes.Object p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ObjectRef_2_Object(ref ArgumentTypes.Object p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ObjectRef_2_ObjectRef(ref ArgumentTypes.Object p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_Value(ArgumentTypes.Value p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_Boxing(ArgumentTypes.Value p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_ValueRef(ArgumentTypes.Value p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_BoxingRef(ArgumentTypes.Value p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_Value(ref ArgumentTypes.Value p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_Boxing(ref ArgumentTypes.Value p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_ValueRef(ref ArgumentTypes.Value p) => TestTools.WriteLine("ok", false);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_BoxingRef(ref ArgumentTypes.Value p) => TestTools.WriteLine("ok", false);
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

	public class DifferingStateTypes
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method() { }
	}
	[HarmonyPatch(typeof(DifferingStateTypes), nameof(DifferingStateTypes.Method))]
	public static class DifferingStateTypesSuccessPatch
	{
		public static List<string> log = [];
		public static bool Prefix(ref string __state)
		{
			log.Add("Hello");
			__state = "Hello2";
			return false;
		}

		[HarmonyPostfix]
		public static void PostfixSucceed(string __state) => log.Add(__state);

		[HarmonyPostfix, HarmonyPriority(Priority.First)]
		public static void PostfixSucceed2(ref object __state)
		{
			log.Add(__state.ToString());
			__state = "Hello3";
		}

		[HarmonyPostfix, HarmonyPriority(Priority.Last)]
		public static void PostfixSucceed3(object __state) => log.Add(__state.ToString());
	}

	[HarmonyPatch(typeof(DifferingStateTypes), nameof(DifferingStateTypes.Method))]
	public static class DifferingStateTypesFailurePatch
	{
		public static List<string> log = [];

		public static bool Prefix(ref string __state)
		{
			log.Add("Hello");
			__state = "Hello2";
			return false;
		}

		[HarmonyPostfix]
		public static void PostfixFail(int __state) => log.Add(__state.ToString());
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

		public enum ShorterThanNormal : sbyte
		{
			a,
			b,
			y
		}
		public enum LongerThanNormal : ulong
		{
			c,
			d,
			z
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Method(
			int n1, ref int n2, out int n3,
			string s1, ref string s2, out string s3,
			SomeStruct st1, ref SomeStruct st2, out SomeStruct st3,
			float[] f1, ref float[] f2, out float[] f3,
			bool b1, ref bool b2, out bool b3,
			ShorterThanNormal e1, ref ShorterThanNormal e2, out ShorterThanNormal e3,
			LongerThanNormal e4, ref LongerThanNormal e5, out LongerThanNormal e6,
			UIntPtr p1, ref UIntPtr p2, out UIntPtr p3,
			nuint m1, ref nuint m2, out nuint m3,
			DateTime d1, ref DateTime d2, out DateTime d3,
			decimal k1, ref decimal k2, out decimal k3
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
			b2 = true;
			b3 = false;
			e2 = ShorterThanNormal.b;
			e3 = ShorterThanNormal.a;
			e5 = LongerThanNormal.d;
			e6 = LongerThanNormal.c;
			p2 = new(5);
			p3 = new(6);
			m2 = 11;
			m3 = 22;
			d2 = new(7);
			d3 = new(8);
			k2 = 444M;
			k3 = 555M;
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

			__args[13] = false;
			__args[14] = true;

			__args[16] = ArgumentArrayMethods.ShorterThanNormal.a;
			__args[17] = ArgumentArrayMethods.ShorterThanNormal.b;

			__args[19] = ArgumentArrayMethods.LongerThanNormal.c;
			__args[20] = ArgumentArrayMethods.LongerThanNormal.d;

			__args[22] = new UIntPtr(1);
			__args[23] = new UIntPtr(2);

			__args[25] = (nuint)789;
			__args[26] = (nuint)101;

			__args[28] = new DateTime(3);
			__args[29] = new DateTime(4);

			__args[31] = 666M;
			__args[32] = 777M;

			return false;
		}

		public static void Postfix(
			int n1, int n2, int n3,
			string s1, string s2, string s3,
			ArgumentArrayMethods.SomeStruct st1, ArgumentArrayMethods.SomeStruct st2, ArgumentArrayMethods.SomeStruct st3,
			float[] f1, float[] f2, float[] f3,
			bool b1, bool b2, bool b3,
			ArgumentArrayMethods.ShorterThanNormal e1, ArgumentArrayMethods.ShorterThanNormal e2, ArgumentArrayMethods.ShorterThanNormal e3,
			ArgumentArrayMethods.LongerThanNormal e4, ArgumentArrayMethods.LongerThanNormal e5, ArgumentArrayMethods.LongerThanNormal e6,
			UIntPtr p1, UIntPtr p2, UIntPtr p3,
			nuint m1, nuint m2, nuint m3,
			DateTime d1, DateTime d2, DateTime d3,
			decimal k1, decimal k2, decimal k3
		)
		{
			postfixInput =
			[
				n1, n2, n3,
				s1, s2, s3,
				st1, st2, st3,
				f1, f2, f3,
				b1, b2, b3,
				e1, e2, e3,
				e4, e5, e6,
				p1, p2, p3,
				m1, m2, m3,
				d1, d2, d3,
				k1, k2, k3
			];
		}
	}
}
