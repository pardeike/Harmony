using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace HarmonyLib
{
    public static class CodeParser
    {
        public static readonly OpCode AnyOpcode = (OpCode)typeof(OpCode).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0].Invoke(new object[] { 256, -257283419 });
        public static readonly object AnyOprand = "*";
        public static readonly OpCode LocalvarOpcode = (OpCode)typeof(OpCode).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0].Invoke(new object[] { 257, 279317166 });
        public static readonly OpCode LabelOpcode = (OpCode)typeof(OpCode).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0].Invoke(new object[] { 258, 279317166 });
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
                opcodestr = opcodestr.ToLower();
                if (opcodestr == "localvar") return new CodeInstruction(LocalvarOpcode, String2Type(parts[1]));
                if (opcodestr == "label") return new CodeInstruction(LabelOpcode, Convert.ToInt32(parts[1]));
                opcodestr = opcodestr.Replace('.', '_');
                opcode = (OpCode)typeof(OpCodes).GetField(opcodestr, BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public).GetValue(null);
            }
            if (parts.Length == 1 || opcode.OperandType == OperandType.InlineNone)
                return new CodeInstruction(opcode);
            else
            {
                string oprandstr = parts[1];
                if (oprandstr == "*")
                    return new CodeInstruction(opcode, AnyOprand);
                object obj = null;
                switch (opcode.OperandType)
                {
                    case OperandType.InlineMethod:
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
                        break;
                    case OperandType.InlineField:
                        parts = oprandstr.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        obj = AccessTools.Field(String2Type(parts[0]), parts[1]);
                        break;
                    case OperandType.InlineString:
                        obj = oprandstr;
                        break;
                    case OperandType.InlineType:
                        obj = String2Type(oprandstr);
                        break;
                    case OperandType.InlineI:
                    case OperandType.InlineBrTarget:
                    case OperandType.ShortInlineBrTarget:
                        obj = Convert.ToInt32(oprandstr);
                        break;
                    case OperandType.InlineVar:
                    case OperandType.ShortInlineI:
                        obj = Convert.ToInt16(oprandstr);
                        break;
                    case OperandType.ShortInlineVar:
                    case OperandType.InlineI8:
                        obj = Convert.ToByte(oprandstr);
                        break;
                    case OperandType.InlineR:
                        obj = Convert.ToDouble(oprandstr);
                        break;
                    case OperandType.ShortInlineR:
                        obj = Convert.ToSingle(oprandstr);
                        break;
                }
                if (obj == null)
                    throw new Exception("Unknown OperandType or Wrong operand");
                return new CodeInstruction(opcode, obj);
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
        IEnumerable<CodeInstruction> TransMethod(TranspilerFactory factory);
    }
    class SearchDeleteTranspiler : ITranspiler
    {
        List<CodeInstruction> search;
        public SearchDeleteTranspiler(List<CodeInstruction> codes)
        {
            if (codes == null || codes.Count <= 0) throw new Exception();
            search = codes;
        }
        public IEnumerable<CodeInstruction> TransMethod(TranspilerFactory factory)
        {
            CodeInstruction current;
            Queue<CodeInstruction> queue = new Queue<CodeInstruction>();
            var instructions = factory.CodeEnumerator;
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
    class SearchTranspiler : ITranspiler
    {
        List<CodeInstruction> search;
        public SearchTranspiler(List<CodeInstruction> codes)
        {
            if (codes == null || codes.Count <= 0) throw new Exception();
            search = codes;
        }
        public IEnumerable<CodeInstruction> TransMethod(TranspilerFactory factory)
        {
            CodeInstruction current;
            Queue<CodeInstruction> queue = new Queue<CodeInstruction>();
            var instructions = factory.CodeEnumerator;
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
    class InsertTranspiler : ITranspiler
    {
        List<CodeInstruction> list;
        public InsertTranspiler(List<CodeInstruction> codes)
        {
            if (codes == null || codes.Count <= 0) throw new Exception();
            list = codes;
        }
        public IEnumerable<CodeInstruction> TransMethod(TranspilerFactory factory)
        {
            var instructions = factory.CodeEnumerator;
            var generator = factory.Generator;
            var locals = factory.Locals;
            var labels = factory.Labels;
            foreach (var code in list)
            {
                var no = code.opcode.Value;
                if (code.opcode == CodeParser.LocalvarOpcode) locals.Add(generator.DeclareLocal((Type)code.operand));
                else if (code.opcode == CodeParser.LabelOpcode)
                {
                    var index = (int)code.operand;
                    for (int i = labels.Count - 1; i < index; i++) labels.Add(generator.DefineLabel());
                    var t = new List<Label>() { labels[index] };
                    yield return new CodeInstruction(OpCodes.Nop) { labels = t };
                }
                else if (no == 17 || no == 18 || no == 19 || no == -500 || no == -499 || no == -498)
                {
                    code.operand = locals[Convert.ToInt32(code.operand)];
                    yield return code;
                }
                else if (code.opcode.OperandType == OperandType.InlineBrTarget || code.opcode.OperandType == OperandType.ShortInlineBrTarget)
                {
                    var index = (int)code.operand;
                    for (int i = labels.Count - 1; i < index; i++) labels.Add(generator.DefineLabel());
                    code.operand = labels[index];
                    yield return code;
                }
                else yield return code;
            }
        }
    }
    class EndingTranspiler : ITranspiler
    {
        public EndingTranspiler() { }
        public IEnumerable<CodeInstruction> TransMethod(TranspilerFactory factory)
        {
            var instructions = factory.CodeEnumerator;
            while (instructions.MoveNext()) yield return instructions.Current;
        }
    }
    class SkipTranspiler : ITranspiler
    {
        int count;
        public SkipTranspiler(int num)
        {
            if (num <= 0) throw new Exception();
            count = num;
        }
        public IEnumerable<CodeInstruction> TransMethod(TranspilerFactory factory)
        {
            var t = count;
            var instructions = factory.CodeEnumerator;
            while (t > 0 && instructions.MoveNext()) t--;
            return null;
        }
    }
    public delegate IEnumerable<CodeInstruction> TranspilerDelegate(ILGenerator generator, IEnumerable<CodeInstruction> instructions);
    public class TranspilerFactory
    {
        List<ITranspiler> transpilers;
        TranspilerDelegate GetTranspiler;
        internal ILGenerator Generator;
        internal IEnumerator<CodeInstruction> CodeEnumerator;
        internal List<LocalBuilder> Locals;
        internal List<Label> Labels;
        public TranspilerFactory()
        {
            transpilers = new List<ITranspiler>();
            GetTranspiler = new TranspilerDelegate(Transpiler);
        }
        public TranspilerFactory Search(string str)
        {
            transpilers.Add(new SearchTranspiler(CodeParser.ParseMutiple(str)));
            return this;
        }
        public TranspilerFactory Replace(string from, string to)
        {
            transpilers.Add(new SearchDeleteTranspiler(CodeParser.ParseMutiple(from)));
            transpilers.Add(new InsertTranspiler(CodeParser.ParseMutiple(to)));
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
        public HarmonyMethod GetTranspilerMethod() => new HarmonyMethod(GetTranspiler.Method);
        public IEnumerable<CodeInstruction> Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instr)
        {
            if (transpilers.Count == 0 || !(transpilers[transpilers.Count - 1] is EndingTranspiler)) transpilers.Add(new EndingTranspiler());
            Generator = generator;
            CodeEnumerator = instr.GetEnumerator();
            Locals = new List<LocalBuilder>();
            Labels = new List<Label>();
            foreach (var t in transpilers)
            {
                foreach (var code in t.TransMethod(this))
                {
                    yield return code;
                }
            }
        }
    }
}
