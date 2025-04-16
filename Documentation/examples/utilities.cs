namespace Utilities
{
	using HarmonyLib;
	using System;

	class Example
	{
		// <example>
		class Foo
		{
			struct Bar
			{
				static string secret = "hello";

				public string ModifiedSecret() => secret.ToUpper();
			}

			Bar MyBar
			{
				get
				{
					return new Bar();
				}
			}

			public string GetSecret() => MyBar.ModifiedSecret();

			Foo()
			{
			}

			static Foo MakeFoo() => new();
		}

		void Test()
		{
			var foo = Traverse.Create<Foo>().Method("MakeFoo").GetValue<Foo>();
			Traverse.Create(foo).Property("MyBar").Field("secret").SetValue("world");
			Console.WriteLine(foo.GetSecret()); // outputs WORLD
		}
		// </example>
	}
}
