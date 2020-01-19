using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace HarmonyLib
{
	internal class PatchSurrogate : ISerializationSurrogate
	{
		public static SurrogateSelector GetSelector()
		{
			var surrogateSelector = new SurrogateSelector();
			surrogateSelector.AddSurrogate(typeof(Patch), new StreamingContext(StreamingContextStates.Persistence), new PatchSurrogate());
			return surrogateSelector;
		}

		public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
		{
			var patch = (Patch)obj;
			foreach (var field in AccessTools.GetDeclaredFields(typeof(Patch)))
				if (field.Name != nameof(Patch.patch))
					info.AddValue(field.Name, field.GetValue(obj));

			var method = patch.patch;
			info.AddValue("token", method.MetadataToken);
			info.AddValue("module", method.Module.FullyQualifiedName);
		}

		public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
		{
			var patch = (Patch)obj;
			foreach (var field in AccessTools.GetDeclaredFields(typeof(Patch)))
				if (field.Name != nameof(Patch.patch))
					field.SetValue(obj, info.GetValue(field.Name, field.FieldType));

			var module = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.FullName.StartsWith("Microsoft.VisualStudio") == false).ToArray()
				.SelectMany(a => a.GetLoadedModules())
				.First(a => a.FullyQualifiedName == info.GetString("module"));

			var method = module.ResolveMethod(info.GetInt32("token"));
			patch.patch = (MethodInfo)method;
			return patch;
		}
	}
}