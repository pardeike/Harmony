using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{

	/// <summary>A harmony annotation</summary>
	public class HarmonyMethod
	{
		/// <summary>The original method</summary>
		public MethodInfo method; // need to be called 'method'

		/// <summary>Declaring class</summary>
		public Type declaringType;
		/// <summary>Method name</summary>
		public string methodName;
		/// <summary>Method type</summary>
		public MethodType? methodType;
		/// <summary>Argument types</summary>
		public Type[] argumentTypes;
		/// <summary>Priority</summary>
		public int priority = -1;
		/// <summary>Before parameter</summary>
		public string[] before;
		/// <summary>After parameter</summary>
		public string[] after;

		/// <summary>Default constructor</summary>
		public HarmonyMethod()
		{
		}

		void ImportMethod(MethodInfo theMethod)
		{
			method = theMethod;
			if (method != null)
			{
				var infos = method.GetHarmonyMethods();
				if (infos != null)
					Merge(infos).CopyTo(this);
			}
		}

		/// <summary>Creates an annotation from a method</summary>
		/// <param name="method">The original method</param>
		///
		public HarmonyMethod(MethodInfo method)
		{
			ImportMethod(method);
		}

		/// <summary>Creates an annotation from a method.</summary>
		/// <param name="type">The type</param>
		/// <param name="name">The method name</param>
		/// <param name="parameters">The optional argument types for overloaded methods</param>
		///
		public HarmonyMethod(Type type, string name, Type[] parameters = null)
		{
			var method = AccessTools.Method(type, name, parameters);
			ImportMethod(method);
		}

		/// <summary>Gets the names of all internal patch info fields</summary>
		/// <returns>A list of field names</returns>
		///
		public static List<string> HarmonyFields()
		{
			return AccessTools
				.GetFieldNames(typeof(HarmonyMethod))
				.Where(s => s != "method")
				.ToList();
		}

		/// <summary>Merges annotations</summary>
		/// <param name="attributes">The annotations</param>
		/// <returns>A merged annotation</returns>
		///
		public static HarmonyMethod Merge(List<HarmonyMethod> attributes)
		{
			var result = new HarmonyMethod();
			if (attributes == null) return result;
			var resultTrv = Traverse.Create(result);
			attributes.ForEach(attribute =>
			{
				var trv = Traverse.Create(attribute);
				HarmonyFields().ForEach(f =>
				{
					var val = trv.Field(f).GetValue();
					if (val != null)
						resultTrv.Field(f).SetValue(val);
				});
			});
			return result;
		}

		/// <summary>Returns a string that represents the annotation</summary>
		/// <returns>A string representation</returns>
		///
		public override string ToString()
		{
			var result = "HarmonyMethod[";
			var trv = Traverse.Create(this);
			HarmonyFields().ForEach(f =>
			{
				result += f + '=' + trv.Field(f).GetValue();
			});
			return result + "]";
		}
	}


	/// <summary>Annotation extensions</summary>
	public static class HarmonyMethodExtensions
	{
		/// <summary>Copies annotation information</summary>
		/// <param name="from">from</param>
		/// <param name="to">to</param>
		///
		public static void CopyTo(this HarmonyMethod from, HarmonyMethod to)
		{
			if (to == null) return;
			var fromTrv = Traverse.Create(from);
			var toTrv = Traverse.Create(to);
			HarmonyMethod.HarmonyFields().ForEach(f =>
			{
				var val = fromTrv.Field(f).GetValue();
				if (val != null) toTrv.Field(f).SetValue(val);
			});
		}

		/// <summary>Clones an annotation</summary>
		/// <param name="original">The annotation to clone</param>
		/// <returns>A copy of the annotation</returns>
		///
		public static HarmonyMethod Clone(this HarmonyMethod original)
		{
			var result = new HarmonyMethod();
			original.CopyTo(result);
			return result;
		}

		/// <summary>Merges annotations</summary>
		/// <param name="master">The master</param>
		/// <param name="detail">The detail</param>
		/// <returns>A new, merged copy</returns>
		///
		public static HarmonyMethod Merge(this HarmonyMethod master, HarmonyMethod detail)
		{
			if (detail == null) return master;
			var result = new HarmonyMethod();
			var resultTrv = Traverse.Create(result);
			var masterTrv = Traverse.Create(master);
			var detailTrv = Traverse.Create(detail);
			HarmonyMethod.HarmonyFields().ForEach(f =>
			{
				var baseValue = masterTrv.Field(f).GetValue();
				var detailValue = detailTrv.Field(f).GetValue();
				resultTrv.Field(f).SetValue(detailValue ?? baseValue);
			});
			return result;
		}

		/// <summary>Gets all annotations on a class</summary>
		/// <param name="type">The class</param>
		/// <returns>All annotations</returns>
		///
		public static List<HarmonyMethod> GetHarmonyMethods(this Type type)
		{
			return type.GetCustomAttributes(true)
						.Where(attr => attr.GetType().Name == nameof(HarmonyAttribute))
						.Select(attr => AccessTools.MakeDeepCopy<HarmonyAttribute>(attr).info)
						.ToList();
		}

		/// <summary>Gets all annotations on a method</summary>
		/// <param name="method">The method</param>
		/// <returns>All annotations</returns>
		///
		public static List<HarmonyMethod> GetHarmonyMethods(this MethodBase method)
		{
			if (method is DynamicMethod) return new List<HarmonyMethod>();
			return method.GetCustomAttributes(true)
						.Where(attr => attr.GetType().Name == nameof(HarmonyAttribute))
						.Select(attr => AccessTools.MakeDeepCopy<HarmonyAttribute>(attr).info)
						.ToList();
		}
	}
}