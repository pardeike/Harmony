using HarmonyLib;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.IL
{
	public struct Vec3
	{
		public int v1;
		public int v2;
		public int v3;

		public Vec3(int v1, int v2, int v3)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;
		}

		public static Vec3 Zero => new Vec3(0, 0, 0);

		override public string ToString()
		{
			return v1 + "," + v2 + "," + v3;
		}
	}

	public static class TestMethods1
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void Test1(out string s)
		{
			try
			{
				s = "Test1";
			}
			finally
			{
			}
		}
	}

	public class TestMethods2
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public string Test2(int n, string s)
		{
			try
			{
				return s;
			}
			finally
			{
			}
		}
	}

	public class TestMethods3
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static List<int> Test3(Vec3 v, List<int> list)
		{
			try
			{
				return new List<int>();
			}
			finally
			{
			}
		}
	}

	[TestFixture]
	public class DynamicArgumentPatches : TestLogger
	{
		static readonly List<string> log = new List<string>();

		static bool General(string typeName, int token, object instance, object[] args)
		{
			var method = AccessTools.TypeByName(typeName).Module.ResolveMethod(token);
			log.Add(method.Name);
			log.Add(instance?.GetType().Name ?? "NULL");
			if (args is object)
				foreach (var arg in args)
					log.Add(arg?.ToString() ?? "NULL");
			return true;
		}

		static readonly MethodInfo m_General = SymbolExtensions.GetMethodInfo(() => General("", 0, null, new object[0]));
		static readonly MethodInfo m_Transpiler = SymbolExtensions.GetMethodInfo(() => Transpiler(null, null, null));
		static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator gen)
		{
			int idx;
			var label = gen.DefineLabel();
			var parameter = original.GetParameters();

			idx = 0;
			foreach (var pInfo in parameter)
			{
				var argIndex = idx++ + (original.IsStatic ? 0 : 1);
				var pType = pInfo.ParameterType;
				if (pInfo.IsOut || pInfo.IsRetval)
				{
					yield return new CodeInstruction(OpCodes.Ldarg, argIndex);
					yield return CreateDefaultCodes(gen, pType).Last();
					if (AccessTools.IsClass(pType))
						yield return new CodeInstruction(OpCodes.Stind_Ref);
					if (AccessTools.IsValue(pType))
					{
						if (pType == typeof(float))
							yield return new CodeInstruction(OpCodes.Stind_R4, (float)0);
						else if (pType == typeof(double))
							yield return new CodeInstruction(OpCodes.Stind_R8, (double)0);
						else if (pType == typeof(long))
							yield return new CodeInstruction(OpCodes.Stind_I8, (long)0);
						else
							yield return new CodeInstruction(OpCodes.Stind_I4, 0);
					}
				}
			}

			yield return new CodeInstruction(OpCodes.Ldstr, original.DeclaringType.FullName);
			yield return new CodeInstruction(OpCodes.Ldc_I4, original.MetadataToken);
			// Note: While some .NET runtimes allow representing 0 (via ldc.i*.0) as null, at least mono doesn't allow that - just use ldnull instead.
			yield return new CodeInstruction(original.IsStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);

			yield return new CodeInstruction(OpCodes.Ldc_I4, parameter.Length);
			yield return new CodeInstruction(OpCodes.Newarr, typeof(object));

			idx = 0;
			var arrayIdx = 0;
			foreach (var pInfo in parameter)
			{
				var argIndex = idx++ + (original.IsStatic ? 0 : 1);
				var pType = pInfo.ParameterType;
				yield return new CodeInstruction(OpCodes.Dup);
				yield return new CodeInstruction(OpCodes.Ldc_I4, arrayIdx++);
				yield return new CodeInstruction(OpCodes.Ldarg, argIndex);
				if (pInfo.IsOut || pInfo.IsRetval)
				{
					if (pType.IsValueType)
						yield return new CodeInstruction(OpCodes.Ldobj, pType);
					else
						yield return new CodeInstruction(OpCodes.Ldind_Ref);
				}
				if (pType.IsValueType)
					yield return new CodeInstruction(OpCodes.Box, pType);
				yield return new CodeInstruction(OpCodes.Stelem_Ref);
			}
			yield return new CodeInstruction(OpCodes.Call, m_General);
			yield return new CodeInstruction(OpCodes.Brtrue, label);
			foreach (var code in CreateDefaultCodes(gen, AccessTools.GetReturnedType(original)))
				yield return code;
			yield return new CodeInstruction(OpCodes.Ret);

			var list = instructions.ToList();
			list.First().labels.Add(label);
			foreach (var instruction in list)
				yield return instruction;
		}

		static IEnumerable<CodeInstruction> CreateDefaultCodes(ILGenerator generator, Type type)
		{
			if (type.IsByRef) type = type.GetElementType();

			if (AccessTools.IsClass(type))
			{
				yield return new CodeInstruction(OpCodes.Ldnull);
				yield break;
			}
			if (AccessTools.IsStruct(type))
			{
				var v = generator.DeclareLocal(type);
				yield return new CodeInstruction(OpCodes.Ldloca, v);
				yield return new CodeInstruction(OpCodes.Initobj, type);
				yield break;
			}
			if (AccessTools.IsValue(type))
			{
				if (type == typeof(float))
					yield return new CodeInstruction(OpCodes.Ldc_R4, (float)0);
				else if (type == typeof(double))
					yield return new CodeInstruction(OpCodes.Ldc_R8, (double)0);
				else if (type == typeof(long))
					yield return new CodeInstruction(OpCodes.Ldc_I8, (long)0);
				else
					yield return new CodeInstruction(OpCodes.Ldc_I4, 0);
				yield break;
			}
		}

		static readonly MethodInfo[] methods = new MethodInfo[]
		{
			AccessTools.Method(typeof(TestMethods1), "Test1"),
			SymbolExtensions.GetMethodInfo(() => new TestMethods2().Test2(0, "")),
			SymbolExtensions.GetMethodInfo(() => TestMethods3.Test3(Vec3.Zero, null))
		};

		[Test]
		public void Test_SendingArguments()
		{
			var harmony = new Harmony("test");
			methods.Do(m =>
			{
				_ = harmony.Patch(m, transpiler: new HarmonyMethod(m_Transpiler));
			});

			TestMethods1.Test1(out var s);
			_ = new TestMethods2().Test2(123, "hello");
			_ = TestMethods3.Test3(new Vec3(2, 4, 6), new[] { 100, 200, 300 }.ToList());

			var n = 0;
			Assert.AreEqual(11, log.Count);

			Assert.AreEqual(log[n++], "Test1");
			Assert.AreEqual(log[n++], "NULL");
			Assert.AreEqual(log[n++], "NULL");

			Assert.AreEqual(log[n++], "Test2");
			Assert.AreEqual(log[n++], "TestMethods2");
			Assert.AreEqual(log[n++], "123");
			Assert.AreEqual(log[n++], "hello");

			Assert.AreEqual(log[n++], "Test3");
			Assert.AreEqual(log[n++], "NULL");
			Assert.AreEqual(log[n++], "2,4,6");
			Assert.AreEqual(log[n++], "System.Collections.Generic.List`1[System.Int32]");
		}
	}
}
