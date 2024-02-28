using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace HarmonyLib
{
	/// <summary>Occcurances of a method that is called inside some outer method</summary>
	///
	[Serializable]
	public class InnerMethod
	{
		[NonSerialized]
		private MethodInfo method;
		private int methodToken;
		private string moduleGUID;

		/// <summary>Which occcurances (1-based) of the method, negative numbers are counting from the end, empty array means all occurances</summary>
		/// 
		public int[] positions;

		/// <summary>Creates an InnerMethod</summary>
		/// <param name="method">The inner method</param>
		/// <param name="positions">Which occcurances (1-based) of the method, negative numbers are counting from the end, empty array means all occurances</param>
		/// 
		public InnerMethod(MethodInfo method, params int[] positions)
		{
			Method = method;
			this.positions = positions;
		}

		/// <summary>The inner method</summary>
		///
#if NET5_0_OR_GREATER
		[JsonIgnore]
#endif
		public MethodInfo Method
		{
			get
			{
				if (method is null)
				{
					var mdl = AppDomain.CurrentDomain.GetAssemblies()
						.Where(a => !a.FullName.StartsWith("Microsoft.VisualStudio"))
						.SelectMany(a => a.GetLoadedModules())
						.First(m => m.ModuleVersionId.ToString() == moduleGUID);
					method = (MethodInfo)mdl.ResolveMethod(methodToken);
				}
				return method;
			}
			set
			{
				method = value;
				methodToken = method.MetadataToken;
				moduleGUID = method.Module.ModuleVersionId.ToString();
			}
		}
	}
}
