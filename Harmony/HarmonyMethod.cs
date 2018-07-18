using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony
{
	public class HarmonyMethod
	{
		public MethodInfo method; // need to be called 'method'

		public Type declaringType;
		public string methodName;
		public MethodType? methodType;
		public Type[] argumentTypes;
		public int prioritiy = -1;
		public string[] before;
		public string[] after;

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

		public HarmonyMethod(MethodInfo method)
		{
			ImportMethod(method);
		}

		public HarmonyMethod(Type type, string name, Type[] parameters = null)
		{
			var method = AccessTools.Method(type, name, parameters);
			ImportMethod(method);
		}

		public static List<string> HarmonyFields()
		{
			return AccessTools
				.GetFieldNames(typeof(HarmonyMethod))
				.Where(s => s != "method")
				.ToList();
		}

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

	public static class HarmonyMethodExtensions
	{
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

		public static HarmonyMethod Clone(this HarmonyMethod original)
		{
			var result = new HarmonyMethod();
			original.CopyTo(result);
			return result;
		}

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

		public static List<HarmonyMethod> GetHarmonyMethods(this Type type)
		{
			return type.GetCustomAttributes(true)
						.Where(attr => attr is HarmonyAttribute)
						.Cast<HarmonyAttribute>()
						.Select(attr => attr.info)
						.ToList();
		}

		public static List<HarmonyMethod> GetHarmonyMethods(this MethodBase method)
		{
			if (method is DynamicMethod) return new List<HarmonyMethod>();
			return method.GetCustomAttributes(true)
						.Where(attr => attr is HarmonyAttribute)
						.Cast<HarmonyAttribute>()
						.Select(attr => attr.info)
						.ToList();
		}
	}
}