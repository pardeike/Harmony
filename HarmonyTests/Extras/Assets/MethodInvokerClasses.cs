using Harmony;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyTests.Assets
{
    public class TestMethodInvokerObject
    {
        public int Value;
        public void Method1(int a)
        {
            Value += a;
        }
    }
    public struct TestMethodInvokerStruct
    {
        public int Value;
    }
    public static class MethodInvokerClass
    {

        public static void Method1(int a, ref int b, out int c, out TestMethodInvokerObject d, ref TestMethodInvokerStruct e)
        {
            b = b + 1;
            c = b * 2;
            d = new TestMethodInvokerObject();
            d.Value = a;
            e.Value = a;
        }

    }
}
