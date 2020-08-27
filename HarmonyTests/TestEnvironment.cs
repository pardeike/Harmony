using HarmonyLib;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HarmonyLibTests
{
	// Logs info on what's running these tests (useful for debugging CI).
	[TestFixture]
	public class TestEnvironment : TestLogger
	{
		[Test]
		public void OutputRuntimeInfo()
		{
			var runtimeInformationType = AccessTools.TypeByName("System.Runtime.InteropServices.RuntimeInformation");
			var executingAssembly = Assembly.GetExecutingAssembly();

			TestTools.Log("Environment.OSVersion: " + Environment.OSVersion);
			TestTools.Log("RuntimeInformation.OSDescription: " + GetProperty(runtimeInformationType, "OSDescription"));

			TestTools.Log("IntPtr.Size: " + IntPtr.Size);
			TestTools.Log("Environment.Is64BitProcess: " + GetProperty(typeof(Environment), "Is64BitProcess"));
			TestTools.Log("Environment.Is64BitOperatingSystem: " + GetProperty(typeof(Environment), "Is64BitOperatingSystem"));
			TestTools.Log("RuntimeInformation.ProcessArchitecture: " + GetProperty(runtimeInformationType, "ProcessArchitecture"));
			TestTools.Log("RuntimeInformation.OSArchitecture: " + GetProperty(runtimeInformationType, "OSArchitecture"));

			TestTools.Log("RuntimeInformation.FrameworkDescription: " + GetProperty(runtimeInformationType, "FrameworkDescription"));
			TestTools.Log("Mono.Runtime.DisplayName: " + CallGetter(Type.GetType("Mono.Runtime"), "GetDisplayName"));
			TestTools.Log("RuntimeEnvironment.RuntimeDirectory: " + RuntimeEnvironment.GetRuntimeDirectory());
			TestTools.Log("RuntimeEnvironment.SystemVersion: " + RuntimeEnvironment.GetSystemVersion());
			TestTools.Log("Environment.Version: " + Environment.Version);

			TestTools.Log("Core Assembly: " + typeof(object).Assembly);
			TestTools.Log("Executing Assembly's ImageRuntimeVersion: " + executingAssembly.ImageRuntimeVersion);
			TestTools.Log("Executing Assembly's TargetFrameworkAttribute: " + (executingAssembly.GetCustomAttributes(true)
				.Where(attr => attr.GetType().Name is "TargetFrameworkAttribute")
				.Select(attr => Traverse.Create(attr).Property("FrameworkName").GetValue<string>())
				.FirstOrDefault() ?? "null"));
		}

		static string GetProperty(Type type, string propertyName)
		{
			return AccessTools.Property(type, propertyName)?.GetValue(null, new object[0])?.ToString() ?? "null";
		}

		static string CallGetter(Type type, string methodName)
		{
			return AccessTools.Method(type, methodName)?.Invoke(null, new object[0])?.ToString() ?? "null";
		}

		[Test]
		public void OutputEnvironmentVariables()
		{
			TestTools.Log(Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process)
				.Cast<System.Collections.DictionaryEntry>().Join(entry => $"{entry.Key}: {entry.Value}", "\n"), indentLevelAfterNewLine: 1);
		}

		[Test]
		public void OutputAssemblies()
		{
			TestTools.Log(AppDomain.CurrentDomain.GetAssemblies().Join(delimiter: "\n"), indentLevelAfterNewLine: 1);
		}

		[Test]
		public void OutputProcessInfo()
		{
			var process = Process.GetCurrentProcess();
			TestTools.Log("ProcessName: " + process.ProcessName);
			TestTools.Log("Modules:\n" + process.Modules.Cast<ProcessModule>().Join(module => $"{module.ModuleName}: {module.FileName}", "\n"));
		}
	}
}
