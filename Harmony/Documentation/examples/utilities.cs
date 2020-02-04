using System;
using HarmonyLib;

class Example
{
	// <example>
	class Foo
	{
		struct Bar
		{
			static string secret = "hello";

			public string ModifiedSecret()
			{
				return secret.ToUpper();
			}
		}

		Bar myBar
		{
			get
			{
				return new Bar();
			}
		}

		public string GetSecret()
		{
			return myBar.ModifiedSecret();
		}

		Foo()
		{
		}

		static Foo MakeFoo()
		{
			return new Foo();
		}
	}

	void Test()
	{
		var foo = Traverse.Create<Foo>().Method("MakeFoo").GetValue<Foo>();
		Traverse.Create(foo).Property("myBar").Field("secret").SetValue("world");
		Console.WriteLine(foo.GetSecret()); // outputs WORLD
	}
	// </example>
}
