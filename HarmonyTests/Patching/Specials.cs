using HarmonyLib;
using HarmonyLibTests.Assets;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;

namespace HarmonyLibTests
{
	[TestFixture]
	public class Specials
	{
		/*
		 * Classes inheriting from MarshalByRefObject are very different beasts
		 * They cannot simply be patched at runtime because .NET will treat them
		 * as proxy objects and the resulting assembler as a result is unusual
		 * and not enough documented to be redirected correctly
		 * 
		[Test]
		public void TestHttpWebRequestGetResponse()
		{
			var t_WebRequest = typeof(HttpWebRequest);
			Assert.IsNotNull(t_WebRequest);
			var m_GetResponse = t_WebRequest.GetMethod("GetResponse");
			Assert.IsNotNull(m_GetResponse);

			var t_HttpWebRequestPatches = typeof(HttpWebRequestPatches);
			var prefix = t_HttpWebRequestPatches.GetMethod("Prefix");
			Assert.IsNotNull(prefix);
			var postfix = t_HttpWebRequestPatches.GetMethod("Postfix");
			Assert.IsNotNull(postfix);

			var instance = new Harmony("test");
			Assert.IsNotNull(instance);

			var patcher = new PatchProcessor(instance, new List<MethodBase> { m_GetResponse }, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
			Assert.IsNotNull(patcher);

			patcher.Patch();

			HttpWebRequestPatches.ResetTest();
			var request = WebRequest.Create("http://google.com");
			Assert.AreEqual(request.GetType(), t_WebRequest);
			var response = request.GetResponse();
			Assert.IsNotNull(response);
			Assert.True(HttpWebRequestPatches.prefixCalled);
			Assert.True(HttpWebRequestPatches.postfixCalled);
		}
		*/
	}
}
