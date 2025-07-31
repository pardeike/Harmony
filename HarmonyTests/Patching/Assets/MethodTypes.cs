using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace HarmonyLibTests.Assets
{
	public class MethodTypesClass
	{
		static MethodTypesClass()
		{
			try { } catch (Exception e) { _ = e; }
		}

		public MethodTypesClass()
		{
			try { } catch (Exception e) { _ = e; }
		}

		~MethodTypesClass()
		{
			try { } catch (Exception e) { _ = e; }
		}

		public string TestGetterSetter
		{
			get => ""; set => _ = value;
		}

		public string TestMethod(string s) => s;
	}

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(nameof(MethodTypesClass.TestMethod), MethodType.Normal)]
	public static class MethodTypes_Normal_Patch
	{
		public static void Prefix() { }
	}

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(nameof(MethodTypesClass.TestGetterSetter), MethodType.Getter)]
	public static class MethodTypes_Getter_Patch
	{
		public static void Prefix() { }
	}

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(nameof(MethodTypesClass.TestGetterSetter), MethodType.Setter)]
	public static class MethodTypes_Setter_Patch
	{
		public static void Prefix() { }
	}


}