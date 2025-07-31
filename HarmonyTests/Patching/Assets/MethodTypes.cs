using HarmonyLib;
using System;
using System.Collections.Generic;

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
using System.Threading.Tasks;
#endif

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
			get => "";
			set => _ = value;
		}

		public string TestMethod(string s) => s;

		public IEnumerable<int> TestEnumerator()
		{
			yield return 1;
			yield return 2;
		}

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
		public async Task<string> TestAsync(string s)
		{
			await Task.Delay(10);
			return s;
		}
#endif

		public event EventHandler TestEvent
		{
			add { }
			remove { }
		}

		public static MethodTypesClass operator +(MethodTypesClass a, MethodTypesClass b) => a;
	}

	//

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

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(MethodType.Constructor)]
	public static class MethodTypes_Constructor_Patch
	{
		public static void Prefix() { }
	}

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(MethodType.StaticConstructor)]
	public static class MethodTypes_StaticConstructor_Patch
	{
		public static void Prefix() { }
	}

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(nameof(MethodTypesClass.TestEnumerator), MethodType.Enumerator)]
	public static class MethodTypes_Enumerator_Patch
	{
		public static void Prefix() { }
	}

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(nameof(MethodTypesClass.TestAsync), MethodType.Async)]
	public static class MethodTypes_Async_Patch
	{
		public static void Prefix() { }
	}
#endif

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(MethodType.Finalizer)]
	public static class MethodTypes_Finalizer_Patch
	{
		public static void Prefix() { }
	}

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(nameof(MethodTypesClass.TestEvent), MethodType.EventAdd)]
	public static class MethodTypes_EventAdd_Patch
	{
		public static void Prefix() { }
	}

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(nameof(MethodTypesClass.TestEvent), MethodType.EventRemove)]
	public static class MethodTypes_EventRemove_Patch
	{
		public static void Prefix() { }
	}

	[HarmonyPatch(typeof(MethodTypesClass))]
	[HarmonyPatch(MethodType.OperatorAddition)]
	public static class MethodTypes_OperatorAddition_Patch
	{
		public static void Prefix() { }
	}
}
