using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;

namespace HarmonyLib
{
    public static class CodeParser
    {
        public static readonly OpCode AnyOpcode = (OpCode)typeof(OpCode).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0].Invoke(new object[] { 256, -257283419 });
        public static readonly object AnyOprand = "*";
        public static readonly Regex MatchMethod = new Regex(@"^(.*?)::?(.*?)(?:\((.*?)\))?$");
        public static readonly Dictionary<string, Type> KeywordTypes = new Dictionary<string, Type>{ { "bool", typeof(bool) }, { "byte", typeof(byte) }, { "char", typeof(char) },{"decimal",typeof(decimal)
},{"double",typeof(double)},{"float",typeof(float)},{"int",typeof(int)},{"long",typeof(long)},{"sbyte",typeof(sbyte)},{"short",typeof(short)},{"string",typeof(string)},{"uint",typeof(uint)},{"ulong",typeof(ulong)},{"ushort",typeof(ushort)}};
        public static Type String2Type(string str)
        {
            Type t;
            KeywordTypes.TryGetValue(str, out t);
            if (t == null) t = AccessTools.TypeByName(str);
            return t;
        }
        public static CodeInstruction Parse(string str)
        {
            if (string.IsNullOrEmpty(str))
                throw new Exception("String to Parse is null or empty");
            var parts = str.Split(new char[] { ' ' }, 2);
            string opcodestr = parts[0];
            OpCode opcode = AnyOpcode;
            if (opcodestr != "*")
            {
                opcodestr = opcodestr.Replace('.', '_');
                opcode = (OpCode)typeof(OpCodes).GetField(opcodestr, BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public).GetValue(null);
            }
            if (parts.Length == 1)
                return new CodeInstruction(opcode);
            else
            {
                string oprandstr = parts[1];
                if (oprandstr == "*" || opcode.OperandType == OperandType.InlineNone)
                    return new CodeInstruction(opcode, AnyOprand);
                else
                {
                    object obj = null;
                    if (opcode.OperandType == OperandType.InlineMethod)
                    {
                        var result = MatchMethod.Match(oprandstr);
                        if (!result.Success) obj = null;
                        else
                        {
                            var type = String2Type(result.Groups[1].Value);
                            var method = result.Groups[2].Value;
                            var argstr = result.Groups[3].Value;
                            Type[] args = null;
                            if (argstr != "")
                            {
                                var t = argstr.Split(',');
                                args = new Type[t.Length];
                                for (int i = 0; i < t.Length; i++)
                                {
                                    var s = t[i].Trim();
                                    KeywordTypes.TryGetValue(s, out args[i]);
                                    if (args[i] == null) args[i] = String2Type(s);
                                }
                            }
                            if (opcode == OpCodes.Newobj) obj = AccessTools.Constructor(type, args);
                            else obj = AccessTools.Method(type, method, args);
                        }
                    }
                    else if (opcode.OperandType == OperandType.InlineField)
                    {
                        parts = oprandstr.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        obj = AccessTools.Field(String2Type(parts[0]), parts[1]);
                    }
                    else if (opcode.OperandType == OperandType.InlineString)
                        obj = oprandstr;
                    else if (opcode.OperandType == OperandType.InlineI)
                        obj = Convert.ToInt32(oprandstr);
                    if (obj == null)
                        throw new Exception("Unknown OperandType or Wrong operand");
                    return new CodeInstruction(opcode, obj);
                }
            }
        }
        public static List<CodeInstruction> ParseMutiple(string str)
        {
            var codes = str.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            List<CodeInstruction> result = new List<CodeInstruction>(codes.Length);
            for (int i = 0; i < codes.Length; i++)
                result.Add(Parse(codes[i]));
            return result;
        }

        public static bool isMatchWith(this CodeInstruction CodeWithMatchOption, CodeInstruction instr)
        {
            bool result = (CodeParser.AnyOpcode == CodeWithMatchOption.opcode) || CodeWithMatchOption.opcode == instr.opcode;
            result &= (CodeParser.AnyOprand == CodeWithMatchOption.operand) || (CodeWithMatchOption.operand != null ? CodeWithMatchOption.operand.Equals(instr.operand) : null == instr.operand);
            return result;
        }
    }
    interface ITranspiler
    {
        IEnumerable<CodeInstruction> TransMethod(IEnumerator<CodeInstruction> instructions);
    }
    public class SearchDeleteTranspiler : ITranspiler
    {
        public List<CodeInstruction> search;
        public SearchDeleteTranspiler(List<CodeInstruction> codes)
        {
            if (codes == null || codes.Count <= 0) throw new Exception();
            search = codes;
        }
        public IEnumerable<CodeInstruction> TransMethod(IEnumerator<CodeInstruction> instructions)
        {
            CodeInstruction current;
            Queue<CodeInstruction> queue = new Queue<CodeInstruction>();
            while (instructions.MoveNext())
            {
                current = instructions.Current;
                queue.Enqueue(current);
                if (queue.Count > search.Count) yield return queue.Dequeue();
                if (queue.Count == search.Count)
                {
                    int count = 0;
                    foreach (var item in queue)
                    {
                        if (search[count].isMatchWith(item)) count++;
                        else break;
                    }
                    if (count >= search.Count)
                    {
                        queue.Clear();
                        yield break;
                    }
                }
            }
            while (queue.Count > 0) yield return queue.Dequeue();
        }
    }
    public class SearchTranspiler : ITranspiler
    {
        public List<CodeInstruction> search;
        public SearchTranspiler(List<CodeInstruction> codes)
        {
            if (codes == null || codes.Count <= 0) throw new Exception();
            search = codes;
        }
        public IEnumerable<CodeInstruction> TransMethod(IEnumerator<CodeInstruction> instructions)
        {
            CodeInstruction current;
            Queue<CodeInstruction> queue = new Queue<CodeInstruction>();
            while (instructions.MoveNext())
            {
                current = instructions.Current;
                queue.Enqueue(current);
                if (queue.Count > search.Count) yield return queue.Dequeue();
                if (queue.Count == search.Count)
                {
                    int count = 0;
                    foreach (var item in queue)
                    {
                        if (search[count].isMatchWith(item)) count++;
                        else break;
                    }
                    if (count >= search.Count)
                    {
                        while (queue.Count > 0) yield return queue.Dequeue();
                        yield break;
                    }
                }
            }
            while (queue.Count > 0) yield return queue.Dequeue();
        }
    }
    public class SearchReplaceTranspiler : ITranspiler
    {
        public List<CodeInstruction> search;
        public List<CodeInstruction> replace;
        public SearchReplaceTranspiler(List<CodeInstruction> codes, List<CodeInstruction> toreplace)
        {
            if (codes == null || codes.Count <= 0) throw new Exception();
            search = codes;
            if (toreplace == null || toreplace.Count <= 0) throw new Exception();
            replace = toreplace;
        }
        public IEnumerable<CodeInstruction> TransMethod(IEnumerator<CodeInstruction> instructions)
        {
            CodeInstruction current;
            Queue<CodeInstruction> queue = new Queue<CodeInstruction>();
            while (instructions.MoveNext())
            {
                current = instructions.Current;
                queue.Enqueue(current);
                if (queue.Count > search.Count) yield return queue.Dequeue();
                if (queue.Count == search.Count)
                {
                    int count = 0;
                    foreach (var item in queue)
                    {
                        if (search[count].isMatchWith(item)) count++;
                        else break;
                    }
                    if (count >= search.Count)
                    {
                        queue.Clear();
                        foreach (var i in replace) yield return i;
                        yield break;
                    }
                }
            }
            while (queue.Count > 0) yield return queue.Dequeue();
        }
    }
    public class InsertTranspiler : ITranspiler
    {
        public List<CodeInstruction> list;
        public InsertTranspiler(List<CodeInstruction> codes)
        {
            if (codes == null || codes.Count <= 0) throw new Exception();
            list = codes;
        }
        public IEnumerable<CodeInstruction> TransMethod(IEnumerator<CodeInstruction> instructions)
        {
            return list;
        }
    }
    public class EndingTranspiler : ITranspiler
    {
        public EndingTranspiler() { }
        public IEnumerable<CodeInstruction> TransMethod(IEnumerator<CodeInstruction> instructions)
        {
            while (instructions.MoveNext()) yield return instructions.Current;
        }
    }
    public class SkipTranspiler : ITranspiler
    {
        public int count;
        public SkipTranspiler(int num)
        {
            if (num <= 0) throw new Exception();
            count = num;
        }
        public IEnumerable<CodeInstruction> TransMethod(IEnumerator<CodeInstruction> instructions)
        {
            var t = count;
            while (t > 0 && instructions.MoveNext()) ;
            return null;
        }
    }
    public delegate IEnumerable<CodeInstruction> TranspilerMethod(ILGenerator generator, IEnumerable<CodeInstruction> instructions);
    public class TranspilerFactory
    {
        List<ITranspiler> transpilers;
        public TranspilerFactory()
        {
            transpilers = new List<ITranspiler>();
            GetTranspiler = new TranspilerType(Transpiler);
        }
        public TranspilerFactory Search(string str)
        {
            transpilers.Add(new SearchTranspiler(CodeParser.ParseMutiple(str)));
            return this;
        }
        public TranspilerFactory Replace(string from, string to)
        {
            transpilers.Add(new SearchReplaceTranspiler(CodeParser.ParseMutiple(from), CodeParser.ParseMutiple(to)));
            return this;
        }
        public TranspilerFactory Insert(string str)
        {
            transpilers.Add(new InsertTranspiler(CodeParser.ParseMutiple(str)));
            return this;
        }
        public TranspilerFactory Delete(string str)
        {
            transpilers.Add(new SearchDeleteTranspiler(CodeParser.ParseMutiple(str)));
            return this;
        }
        public TranspilerFactory Delete(int num)
        {
            transpilers.Add(new SkipTranspiler(num));
            return this;
        }
        public delegate IEnumerable<CodeInstruction> TranspilerType(ILGenerator generator, IEnumerable<CodeInstruction> instr);
        private TranspilerType GetTranspiler;
        public HarmonyMethod GetTranspilerMethod() => new HarmonyMethod(GetTranspiler.Method);
        public IEnumerable<CodeInstruction> Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instr)
        {
            var last = transpilers[transpilers.Count - 1];
            if (!(last is EndingTranspiler)) transpilers.Add(new EndingTranspiler());
            var iter = instr.GetEnumerator();
            foreach (var t in transpilers)
            {
                foreach (var code in t.TransMethod(iter))
                {
                    yield return code;
                }
            }
        }
    }
}
