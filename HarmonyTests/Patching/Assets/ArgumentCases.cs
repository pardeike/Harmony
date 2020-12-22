using HarmonyLib;
using System;
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
		public void Object_2_Object(ArgumentTypes.Object p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Object_2_ObjectRef(ArgumentTypes.Object p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ObjectRef_2_Object(ref ArgumentTypes.Object p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ObjectRef_2_ObjectRef(ref ArgumentTypes.Object p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_Value(ArgumentTypes.Value p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_Boxing(ArgumentTypes.Value p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_ValueRef(ArgumentTypes.Value p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Value_2_BoxingRef(ArgumentTypes.Value p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_Value(ref ArgumentTypes.Value p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_Boxing(ref ArgumentTypes.Value p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_ValueRef(ref ArgumentTypes.Value p)
		{
			Console.WriteLine("ok");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ValueRef_2_BoxingRef(ref ArgumentTypes.Value p)
		{
			Console.WriteLine("ok");
		}
	}

	public static class ArgumentPatchMethods
	{
		public static string result;

		public static void Reset()
		{
			result = "";
		}

		public static void To_Object(ArgumentTypes.Object p)
		{
			result += p.GetType().Name[0];
		}

		public static void To_Value(ArgumentTypes.Value p)
		{
			result += p.GetType().Name[0];
		}

		public static void To_Boxing(object p)
		{
			result += p.GetType().Name[0];
		}

		public static void To_ObjectRef(ref ArgumentTypes.Object p)
		{
			result += p.GetType().Name[0];
		}

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
}
