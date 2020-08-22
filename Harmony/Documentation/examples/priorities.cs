namespace Priorities
{
	using HarmonyLib;
	using System.Reflection;

	// <foo>
	class Foo
	{
		static string Bar()
		{
			return "secret";
		}
	}
	// </foo>

	class Plugin1
	{
		// <plugin1>
		void Main_Plugin1()
		{
			var harmony = new Harmony("net.example.plugin1");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}

		[HarmonyPatch(typeof(Foo))]
		[HarmonyPatch("Bar")]
		class MyPatch
		{
			static void Postfix(ref string result)
			{
				result = "new secret 1";
			}
		}
		// </plugin1>
	}

	class Plugin1b
	{
		// <plugin1b>
		void Main_Plugin1b()
		{
			var harmony = new Harmony("net.example.plugin1");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}

		[HarmonyPatch(typeof(Foo))]
		[HarmonyPatch("Bar")]
		class MyPatch
		{
			[HarmonyAfter(new string[] { "net.example.plugin2" })]
			static void Postfix(ref string result)
			{
				result = "new secret 1";
			}
		}
		// </plugin1b>
	}

	class Plugin2
	{
		// <plugin2>
		void Main_Plugin2()
		{
			var harmony = new Harmony("net.example.plugin2");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}

		[HarmonyPatch(typeof(Foo))]
		[HarmonyPatch("Bar")]
		class MyPatch
		{
			static void Postfix(ref string result)
			{
				result = "new secret 2";
			}
		}
		// </plugin2>
	}
}
