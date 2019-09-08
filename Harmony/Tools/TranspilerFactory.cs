using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;

#pragma warning disable CS1591
namespace HarmonyLib
{
    /// <summary>
    /// Extend the CodeInstruction class to hold some options
    /// </summary>
    public class CodeInstructionEx : CodeInstruction
    {
        /// <summary>
        /// options
        /// </summary>
        public enum Options
        {
            /// <summary>
            /// Same as original
            /// </summary>
            None,
            /// <summary>
            /// Matched with any opcode and any operand.
            /// </summary>
            AnyOpcode,
            /// <summary>
            /// Matched with specific opcode and arbitrary operand.
            /// </summary>
            AnyOperand,
            /// <summary>
            /// Declare a local var
            /// </summary>
            DeclareVar,
            /// <summary>
            /// Mark a label. You can used any label of number without defining it.
            /// </summary>
            DeclareLabel
        }
        public Options option;
        public CodeInstructionEx(CodeInstruction instruction, Options opt = Options.None) : base(instruction) => option = opt;
        public CodeInstructionEx(OpCode opcode, object operand = null, Options opt = Options.None) : base(opcode, operand) => option = opt;
        public CodeInstructionEx(OpCode opcode, Options opt) : base(opcode, null) => option = opt;
        public CodeInstructionEx(Options opt, object operand = null) : base(OpCodes.Nop, operand)
        {
            option = opt;
            if (opt == Options.None || opt == Options.AnyOperand) throw new Exception();
        }
        public override string ToString()
        {
            switch (option)
            {
                case Options.AnyOpcode:
                    return "*";
                case Options.AnyOperand:
                    return opcode.ToString() + " *";
                case Options.DeclareVar:
                    return "Localvar " + operand.ToString();
                case Options.DeclareLabel:
                    return "Label " + operand.ToString();
                default:
                    if (opcode.OperandType == OperandType.InlineNone) return opcode.ToString();
                    return base.ToString();
            }
        }
        static readonly OpCode[] array = { OpCodes.Ldloc_S, OpCodes.Ldloca_S, OpCodes.Stloc_S, OpCodes.Ldloc, OpCodes.Ldloca, OpCodes.Stloc };
        public bool OperandIsVar { get => Array.IndexOf<OpCode>(array, opcode) != -1; }
        public bool OperandIsLabel { get => opcode.OperandType == OperandType.InlineBrTarget || opcode.OperandType == OperandType.ShortInlineBrTarget; }
        /// <summary>
        /// Matching CodeInstruction with the option
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public bool Match(CodeInstruction code)
        {
            switch (option)
            {
                case Options.None:
                    return opcode == code.opcode && operand == code.operand;
                case Options.AnyOpcode:
                    return true;
                case Options.AnyOperand:
                    return opcode == code.opcode;
                default:
                    throw new Exception("Declare CodeInstruction cannot match with others");
            }
        }
    }
    /// <summary>
    /// Parse input string with wildcards to CodeInstructionEx(s). Case insensitive. 
    /// Better not type any extra space !!!
    /// </summary>
    /// <remarks>
    /// The format of MethodName is [type name][one or two colon][method name]([parameter types seperated by comma])(optional)
    /// The format of FieldName is [type name][one or two colon][field name]
    /// declare local var is localvar [type name]
    /// declare label is label [zero-based number]
    /// </remarks>
    /// <example>
    /// call string::Concat(string, string)
    /// br 0
    /// label 0
    /// localvar int
    /// </example>
    public static class Parser
    {
        static readonly Regex MatchMethod = new Regex(@"^(.*?)(?:::?(.*?))?(?:\((.*?)\))?$");
        public static MethodBase ParseMethod(string str)
        {
            var match = MatchMethod.Match(str);
            if (!match.Success) return null;
            MethodBase method;
            var type = AccessTools.TypeByName(match.Groups[1].Value);
            var methodstr = match.Groups[2].Value;
            var argstr = match.Groups[3].Value;
            Type[] args = null;
            if (argstr != "")
            {
                var t = argstr.Split(',');
                args = new Type[t.Length];
                for (int i = 0; i < t.Length; i++) args[i] = AccessTools.TypeByName(t[i].Trim());
            }
            if (methodstr == "") method = AccessTools.Constructor(type, args);
            else method = AccessTools.Method(type, methodstr, args);
            return method;
        }
        static readonly Regex MatchField = new Regex(@"^(.*?)::?(.*?)$");
        public static FieldInfo ParseField(string str)
        {
            var match = MatchField.Match(str);
            if (!match.Success) return null;
            var type = AccessTools.TypeByName(match.Groups[1].Value);
            var fieldstr = match.Groups[2].Value;
            return AccessTools.Field(type, fieldstr);
        }
        public static CodeInstructionEx ParseSingle(string str)
        {
            if (string.IsNullOrEmpty(str) || str == "*") return new CodeInstructionEx(CodeInstructionEx.Options.AnyOpcode);
            var parts = str.Split(new char[] { ' ' }, 2);
            string opcodestr = parts[0].ToLower();
            if (opcodestr == "*") return new CodeInstructionEx(CodeInstructionEx.Options.AnyOpcode);
            if (opcodestr == "localvar")
                return new CodeInstructionEx(CodeInstructionEx.Options.DeclareVar, AccessTools.TypeByName(parts[1]));
            if (opcodestr == "label")
                return new CodeInstructionEx(CodeInstructionEx.Options.DeclareLabel, Convert.ToInt32(parts[1]));
            opcodestr = opcodestr.Replace('.', '_');
            var opcode = (OpCode)typeof(OpCodes).GetField(opcodestr, BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public).GetValue(null);
            if (parts.Length == 1 || opcode.OperandType == OperandType.InlineNone) return new CodeInstructionEx(opcode);
            string oprandstr = parts[1];
            if (oprandstr == "*")
                return new CodeInstructionEx(opcode, CodeInstructionEx.Options.AnyOperand);
            object obj = null;
            switch (opcode.OperandType)
            {
                case OperandType.InlineMethod:
                    obj = ParseMethod(oprandstr);
                    break;
                case OperandType.InlineField:
                    obj = ParseField(oprandstr);
                    break;
                case OperandType.InlineString:
                    obj = oprandstr;
                    break;
                case OperandType.InlineType:
                    obj = AccessTools.TypeByName(oprandstr);
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
            return new CodeInstructionEx(opcode, obj);
        }
        public static List<CodeInstructionEx> Parse(string str)
        {
            var codes = str.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            List<CodeInstructionEx> result = new List<CodeInstructionEx>(codes.Length);
            for (int i = 0; i < codes.Length; i++)
                result.Add(ParseSingle(codes[i]));
            return result;
        }
    }
    interface ITranspiler
    {
        IEnumerable<CodeInstruction> TransMethod(TranspilerFactory factory);
    }
    class SearchDeleteTranspiler : ITranspiler
    {
        List<CodeInstructionEx> search;
        public SearchDeleteTranspiler(List<CodeInstructionEx> codes)
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
                        if (search[count].Match(item)) count++;
                        else break;
                    }
                    if (count >= search.Count)
                    {
                        queue.Clear();
                        factory.TaskCount--;
                        yield break;
                    }
                }
            }
            while (queue.Count > 0) yield return queue.Dequeue();
        }
    }
    class SearchTranspiler : ITranspiler
    {
        List<CodeInstructionEx> search;
        public SearchTranspiler(List<CodeInstructionEx> codes)
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
                        if (search[count].Match(item)) count++;
                        else break;
                    }
                    if (count >= search.Count)
                    {
                        while (queue.Count > 0) yield return queue.Dequeue();
                        factory.TaskCount--;
                        yield break;
                    }
                }
            }
            while (queue.Count > 0) yield return queue.Dequeue();
        }
    }
    class InsertTranspiler : ITranspiler
    {
        List<CodeInstructionEx> list;
        public InsertTranspiler(List<CodeInstructionEx> codes)
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
                int index;
                switch (code.option)
                {
                    case CodeInstructionEx.Options.DeclareVar:
                        locals.Add(generator.DeclareLocal((Type)code.operand));
                        break;
                    case CodeInstructionEx.Options.DeclareLabel:
                        index = (int)code.operand;
                        for (int i = labels.Count - 1; i < index; i++) labels.Add(generator.DefineLabel());
                        var t = new List<Label>() { labels[index] };
                        yield return new CodeInstruction(OpCodes.Nop) { labels = t };
                        break;
                    case CodeInstructionEx.Options.None:
                        CodeInstruction result = code.Clone();
                        if (code.OperandIsVar) result.operand = locals[Convert.ToInt32(code.operand)];
                        else if (code.OperandIsLabel)
                        {
                            index = (int)code.operand;
                            for (int i = labels.Count - 1; i < index; i++) labels.Add(generator.DefineLabel());
                            result.operand = labels[index];
                        }
                        yield return result;
                        break;
                    default:
                        throw new Exception();
                }
            }
            factory.TaskCount--;
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
            factory.TaskCount--;
            return null;
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
    /// <summary>
    /// The delegate type of transpiler used in TranspilerFactory
    /// </summary>
    /// <param name="generator"></param>
    /// <param name="instructions"></param>
    /// <returns></returns>
    public delegate IEnumerable<CodeInstruction> TranspilerDelegate(ILGenerator generator, IEnumerable<CodeInstruction> instructions);
    /// <summary>
    /// A convenient Transpiler Factory to generate transpilers
    /// It consist of a FIFO list of tasks such as Search, Delete and Replace which is executed in order.
    /// </summary>
    /// <example>
    /// new TranspilerFactory().Replace(..., ...).PatchFor(harmony, "someType::someMethod");
    /// </example>
    public class TranspilerFactory
    {
        // Instance
        internal ILGenerator Generator;
        internal IEnumerator<CodeInstruction> CodeEnumerator;
        internal List<LocalBuilder> Locals;
        internal List<Label> Labels;
        List<ITranspiler> tasks = new List<ITranspiler>();
        public int TaskCount = 0;
        void AddTask(ITranspiler task)
        {
            tasks.Add(task);
        }
        /// <summary>
        /// Search the instructions list, the position after search is just after the last element.
        /// </summary>
        /// <param name="codes"></param>
        /// <returns></returns>
        public TranspilerFactory Search(List<CodeInstructionEx> codes) { AddTask(new SearchTranspiler(codes)); return this; }
        /// <summary>
        /// Search the instructions list for certain times, the position after search is just after the last element.
        /// </summary>
        /// <param name="codes"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public TranspilerFactory Search(List<CodeInstructionEx> codes, int num) { for (int i = 0; i < num; i++) Search(codes); return this; }
        /// <summary>
        /// Search the instructions list, the position after search is just after the last element.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public TranspilerFactory Search(string str) { Search(Parser.Parse(str)); return this; }
        /// <summary>
        /// Search the instructions list for certain times, the position after search is just after the last element.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public TranspilerFactory Search(string str, int num) { var codes = Parser.Parse(str); for (int i = 0; i < num; i++) Search(codes); return this; }
        /// <summary>
        /// Insert without moving postion
        /// </summary>
        /// <param name="codes"></param>
        /// <returns></returns>
        public TranspilerFactory Insert(List<CodeInstructionEx> codes) { AddTask(new InsertTranspiler(codes)); return this; }
        /// <summary>
        /// Insert without moving postion
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public TranspilerFactory Insert(string str) { Insert(Parser.Parse(str)); return this; }
        /// <summary>
        /// Search the instructions list and delete it.
        /// </summary>
        /// <param name="codes"></param>
        /// <returns></returns>
        public TranspilerFactory Delete(List<CodeInstructionEx> codes) { AddTask(new SearchDeleteTranspiler(codes)); return this; }
        /// <summary>
        /// Search the instructions list and delete it for certain times
        /// </summary>
        /// <param name="codes"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public TranspilerFactory Delete(List<CodeInstructionEx> codes, int num) { for (int i = 0; i < num; i++) Delete(codes); return this; }
        /// <summary>
        /// Search the instructions list and delete it.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public TranspilerFactory Delete(string str) { Delete(Parser.Parse(str)); return this; }
        /// <summary>
        /// Search the instructions list and delete it for certain times.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public TranspilerFactory Delete(string str, int num) { var codes = Parser.Parse(str); for (int i = 0; i < num; i++) Delete(codes); return this; }
        /// <summary>
        /// Delete certain number of instructions at current position.
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public TranspilerFactory Delete(int num) { AddTask(new SkipTranspiler(num)); return this; }
        /// <summary>
        /// Search the first instructions list and replace with the second.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public TranspilerFactory Replace(List<CodeInstructionEx> from, List<CodeInstructionEx> to) { Delete(from); Insert(to); return this; }
        /// <summary>
        /// Search the first instructions list and replace with the second for certain times.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public TranspilerFactory Replace(List<CodeInstructionEx> from, List<CodeInstructionEx> to, int num) { for (int i = 0; i < num; i++) { Delete(from); Insert(to); } return this; }
        /// <summary>
        /// Search the first instructions list and replace with the second.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public TranspilerFactory Replace(string from, string to) { Replace(Parser.Parse(from), Parser.Parse(to)); return this; }
        /// <summary>
        /// Search the first instructions list and replace with the second for certain times.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="num"></param>
        /// <returns></returns>
        public TranspilerFactory Replace(string from, string to, int num) { var from_ = Parser.Parse(from); var to_ = Parser.Parse(to); for (int i = 0; i < num; i++) Replace(from_, to_); return this; }
        public IEnumerable<CodeInstruction> Transpile(ILGenerator generator, IEnumerable<CodeInstruction> instr)
        {
            if (tasks.Count == 0 || !(tasks[tasks.Count - 1] is EndingTranspiler)) tasks.Add(new EndingTranspiler());
            TaskCount = tasks.Count - 1;
            Generator = generator;
            CodeEnumerator = instr.GetEnumerator();
            Locals = new List<LocalBuilder>();
            Labels = new List<Label>();
            foreach (var t in tasks)
            {
                foreach (var code in t.TransMethod(this))
                {
                    yield return code;
                }
            }
            if (throwOnNotCompleted && TaskCount > 0)
                throw new Exception("TranspilerFactory did not complete the specified number of tasks. ");
        }
        public void PatchFor(Harmony instance, string method)
        {
            factory = this;
            instance.Patch(Parser.ParseMethod(method), transpiler: GetHarmonyMethod());
            factory = null;
        }
        public void PatchFor(Harmony instance, MethodBase original)
        {
            factory = this;
            instance.Patch(original, transpiler: GetHarmonyMethod());
            factory = null;
        }
        // Static
        public static bool throwOnNotCompleted = true;
        public static TranspilerFactory factory;
        static IEnumerable<CodeInstruction> Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions) => factory.Transpile(generator, instructions);
        public static HarmonyMethod GetHarmonyMethod() => new HarmonyMethod(((TranspilerDelegate)Transpiler).Method);

    }
    /// <summary>
    /// A abstract version of TranspilerFactory
    /// Inherent from this and initialize factory in function Prepare
    /// Not recommended for multiple originalMethod (because Prepare will be called multiple times).
    /// </summary>
    /// <example>
    /// [HarmonyPatch(typeof(someType), "someMethod")]
    /// class MyPatch : TranspilerPatch
    /// {
    ///     static void Prepare() => factory.Replace(..., ...);
    /// }
    /// </example>
    public abstract class TranspilerPatch
    {
        protected static TranspilerFactory factory = new TranspilerFactory();
        protected static IEnumerable<CodeInstruction> Transpiler(ILGenerator generator, IEnumerable<CodeInstruction> instructions) => factory.Transpile(generator, instructions);
        protected static void Cleanup() => factory = new TranspilerFactory();
    }
}
