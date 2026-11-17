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

	// Declares every operator that MethodType names. C# has no comma operator, so op_Comma is an ordinary method.
	public class MethodTypesOperators
	{
		public static implicit operator MethodTypesOperators(int value) => new();
		public static explicit operator int(MethodTypesOperators x) => 0;
		public static explicit operator checked int(MethodTypesOperators x) => 0;
		public static MethodTypesOperators operator +(MethodTypesOperators x) => x;
		public static MethodTypesOperators operator -(MethodTypesOperators x) => x;
		public static MethodTypesOperators operator checked -(MethodTypesOperators x) => x;
		public static MethodTypesOperators operator !(MethodTypesOperators x) => x;
		public static MethodTypesOperators operator ~(MethodTypesOperators x) => x;
		public static MethodTypesOperators operator ++(MethodTypesOperators x) => x;
		public static MethodTypesOperators operator checked ++(MethodTypesOperators x) => x;
		public static MethodTypesOperators operator --(MethodTypesOperators x) => x;
		public static MethodTypesOperators operator checked --(MethodTypesOperators x) => x;
		public static bool operator true(MethodTypesOperators x) => true;
		public static bool operator false(MethodTypesOperators x) => false;
		public static MethodTypesOperators operator +(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator checked +(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator -(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator checked -(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator *(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator checked *(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator /(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator checked /(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator %(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator &(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator |(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator ^(MethodTypesOperators x, MethodTypesOperators y) => x;
		public static MethodTypesOperators operator <<(MethodTypesOperators x, int count) => x;
		public static MethodTypesOperators operator >>(MethodTypesOperators x, int count) => x;
		public static MethodTypesOperators operator >>>(MethodTypesOperators x, int count) => x;
		public static bool operator ==(MethodTypesOperators x, MethodTypesOperators y) => false;
		public static bool operator !=(MethodTypesOperators x, MethodTypesOperators y) => false;
		public static bool operator >(MethodTypesOperators x, MethodTypesOperators y) => false;
		public static bool operator <(MethodTypesOperators x, MethodTypesOperators y) => false;
		public static bool operator >=(MethodTypesOperators x, MethodTypesOperators y) => false;
		public static bool operator <=(MethodTypesOperators x, MethodTypesOperators y) => false;
		public static MethodTypesOperators op_Comma(MethodTypesOperators x, MethodTypesOperators y) => y;
		public void operator +=(MethodTypesOperators y) { }
		public void operator checked +=(MethodTypesOperators y) { }
		public void operator -=(MethodTypesOperators y) { }
		public void operator checked -=(MethodTypesOperators y) { }
		public void operator *=(MethodTypesOperators y) { }
		public void operator checked *=(MethodTypesOperators y) { }
		public void operator /=(MethodTypesOperators y) { }
		public void operator checked /=(MethodTypesOperators y) { }
		public void operator %=(MethodTypesOperators y) { }
		public void operator &=(MethodTypesOperators y) { }
		public void operator |=(MethodTypesOperators y) { }
		public void operator ^=(MethodTypesOperators y) { }
		public void operator <<=(int count) { }
		public void operator >>=(int count) { }
		public void operator >>>=(int count) { }
		public void operator ++() { }
		public void operator checked ++() { }
		public void operator --() { }
		public void operator checked --() { }
		public override bool Equals(object obj) => false;
		public override int GetHashCode() => 0;
	}
}

#if !NET7_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
	// The compiler marks compound assignment operators with this attribute. Frameworks before .NET 7 lack it.
	[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
	internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
	{
		public string FeatureName { get; } = featureName;
		public bool IsOptional { get; set; }
	}
}
#endif
