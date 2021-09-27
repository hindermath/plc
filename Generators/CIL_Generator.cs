#nullable enable
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PLC
{
    public class CIL_Generator
    {
        private Procedure main = new Procedure() { Name = "Main" };
        private Procedure _proc;
        private Dictionary<string, int> Prefixes = new();

        public CIL_Generator(ParsedProgram program)
        {
            Program = program;
            foreach (Identity constant in program.Block.Constants) {
                program.Globals.Add(constant);
            }
            foreach (Identity variable in program.Block.Variables) {
                program.Globals.Add(variable);
            }
            _proc = main;
        }
        
        public ParsedProgram Program { get; set; }

        public int Compile()
        {
            return 1;
        }
        public IEnumerable<string> Generate()
        {
            yield return ".assembly extern mscorlib";
            yield return "{";
            yield return "    .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )";
            yield return "    .ver 4:0:0:0";
            yield return "}";
            yield return "";
            yield return ".assembly Program { }";
            yield return "";
            yield return ".class public auto ansi beforefieldinit plc extends [mscorlib]System.Object";
            yield return "{";
            
            // Generate constant declarations
            foreach (Identity constant in Program.Block.Constants) {
                yield return String.Format("    .field private static literal int32 {0} = int32({1})", constant.Name, constant.Value);
            }
            
            // Variable declarations
            foreach (Identity variable in Program.Block.Variables) {
                yield return "    .field private static int32 " + variable.Name;
            }
            yield return String.Empty; // Just makes it look prettier
            
            foreach (Procedure proc in Program.Block.Procedures)
            {
                _proc = proc;
                yield return "    .method private hidebysig static void " + proc.Name + "() cil managed";
                yield return "    {";
                yield return "        .maxstack 32";
                foreach (string s in GenerateBlock(proc.Block))
                {
                    yield return "    " + s;
                }

                yield return "        ret";
                yield return "    }";
                yield return String.Empty;    // Just makes it prettier
            }
            
            _proc = main;
            // Generate the Main body of the program
            yield return "    .method public hidebysig static void Main() cil managed";
            yield return "    {";
            yield return "        .entrypoint";
            yield return "        .maxstack 32";

            foreach (string s in GenerateStatement(Program.Block.Statement))
            {
                yield return "        " + s;
            }

            yield return "        ret";
            yield return "    }";
            
            yield return String.Empty;    // Just makes it prettier
            
            yield return "    .method public hidebysig specialname rtspecialname instance void .ctor() cil managed";
            yield return "    {";
            yield return "        .maxstack 8";
            yield return "        ldarg.0";
            yield return "        call instance void [mscorlib]System.Object::.ctor()";
            yield return "        nop";
            yield return "        ret";
            yield return "    }";
            
            yield return "}";
        }
        public IEnumerable<string> GenerateBlock(Block block)
        {
            string constants = GenerateConstantDeclarations(block.Constants);
            if (constants != String.Empty)
            {
                yield return constants;
            }

            foreach (Identity variable in block.Variables)
            {
                _proc.Locals.Add(variable);
            }
            string variables = GenerateVariableDeclarations(block.Variables);
            if (variables != String.Empty)
            {
                yield return variables;
            }
            
            foreach (string s in GenerateStatement(block.Statement))
            {
                yield return "    " + s;
            }
        }

        public string GenerateConstantDeclarations(List<Identity> constants)
        {
            int c = constants.Count;
            if (c == 0)
            {
                return String.Empty;
            }

            StringBuilder sb = new();
            sb.Append("    const int ");
            for (int i = 0; i < c; i++)
            {
                Identity cc = constants.ElementAt(i);
                sb.Append(cc.Name);
                sb.Append(" = ");
                sb.Append(cc.Value);
                if (i < (c - 1))
                {
                    sb.Append(", ");
                }
            }

            sb.Append(";");
            return sb.ToString();
        }

        public string GenerateVariableDeclarations(List<Identity> variables)
        {
            int c = variables.Count;
            if (c == 0)
            {
                return String.Empty;
            }

            StringBuilder sb = new();
            sb.AppendLine("    .locals init (");
            var enumerator = variables.GetEnumerator();
            bool keepGoing = enumerator.MoveNext();
            int position = 0;
            while (keepGoing)
            {
                sb.Append("            [" + position + "] int32 " + enumerator.Current.Name);
                if (enumerator.MoveNext())
                {
                    sb.AppendLine(",");
                    position++;
                }
                else
                {
                    keepGoing = false;
                }
            }
            sb.AppendLine();
            sb.AppendLine("        )");
            return sb.ToString();
        }
        
        public string GenerateLabel(string prefix)
        {
            if (Prefixes.ContainsKey(prefix))
            {
                Prefixes[prefix]++;
            }
            else
            {
                Prefixes.Add(prefix, 1);
            }

            return prefix + Prefixes[prefix];
        }
        public IEnumerable<string> GenerateStatement(Statement statement)
        {
            if (statement is WriteStatement)
            {
                var writeStatement = (WriteStatement) statement;
                if (String.IsNullOrEmpty(writeStatement.Message))
                {
                    foreach (string s in GenerateExpression(writeStatement.Expression))
                    {
                        yield return s;
                    }
                    yield return "call void [mscorlib]System.Console::WriteLine(int32)";
                }
                else
                {
                    yield return "ldstr \"" + writeStatement.Message + "\"";
                    yield return "call void [mscorlib]System.Console::WriteLine(string)";
                }
            }
            else if (statement is ReadStatement)
            {
                var readStatement = (ReadStatement) statement;
                if (!String.IsNullOrEmpty(readStatement.Message))
                {
                    yield return "ldstr \"" + readStatement.Message + "\"";
                    yield return "call void [mscorlib]System.Console::Write(string)";
                }
                yield return "call string [mscorlib]System.Console::ReadLine()";
                string result;
                if (_proc.Locals.Exists(i => i.Name == readStatement.IdentityName))
                {
                    result = "ldloca " + readStatement.IdentityName;
                }
                else
                {
                    result = "ldsflda int32 plc::" + readStatement.IdentityName;
                }
                yield return result;
                yield return "call bool [mscorlib]System.Int32::TryParse(string,int32&)";
                yield return "pop";
            }
            else if (statement is AssignmentStatement)
            {
                var assignmentStatement = (AssignmentStatement) statement;
                foreach (string s in GenerateExpression(assignmentStatement.Expression))
                {
                    yield return s;
                }
                yield return StoreIdentityName(assignmentStatement.IdentityName);
            }
            else if (statement is CallStatement)
            {
                var cs = (CallStatement) statement;
                yield return "call void plc::" + cs.ProcedureName + "()";
            }
            else if (statement is CompoundStatement)
            {
                var bs = (CompoundStatement) statement;
                //Console.WriteLine("Generating block statement");
                foreach (Statement st in bs.Statements)
                {
                    if (!st.SkipGeneration)
                    {
                        foreach (string s in GenerateStatement(st))
                        {
                            yield return s;
                        }
                    }
                }
            }
            else if (statement is IfStatement)
            {
                var iff = (IfStatement) statement;
                string label = GenerateLabel("endif");
                foreach (string s in BranchWhenFalse(iff.Condition, label))
                {
                    yield return s;
                }
                foreach (string s in GenerateStatement(iff.Statement))
                {
                    yield return s;
                }
                yield return label + ":";
            }
            else if (statement is DoWhileStatement)
            {
                var dw = (DoWhileStatement) statement;
                string label = GenerateLabel("startloop");
                yield return label + ":";
                foreach (string s in GenerateStatement(dw.Statement))
                {
                    yield return s;
                }
                foreach (string s in BranchWhenTrue(dw.Condition, label))
                {
                    yield return s;
                }
            }
            else if (statement is WhileStatement)
            {
                string startLabel = GenerateLabel("startloop");
                string endLabel = GenerateLabel("endloop");
                yield return startLabel + ":";
                var ws = (WhileStatement) statement;
                foreach (string s in BranchWhenFalse(ws.Condition, endLabel))
                {
                    yield return s;
                }
                foreach (string s in GenerateStatement(ws.Statement))
                {
                    yield return s;
                }
                yield return "br " + startLabel;
                yield return endLabel + ":";
            }
            else    // Must be empty statement
            {
                //yield return "nop";
            }
        }
        // CIL requires that "literals" ( constants ) be propagated
        // This is not an optimization -- it is a requirement
        private string LoadIdentityName(string identityName)
        {
            //Console.Error.WriteLine("Loading identifier: " + identityName);
            string result;
            //Console.Error.WriteLine("Checking in global constants: " + identityName);
            if (Program.Block.Constants.Exists(i => i.Name == identityName))
            {
                //Console.Error.WriteLine("Found in global constants: " + identityName);
                Identity constant = Program.Block.Constants.Single(i => i.Name == identityName);
                result = LoadConstant(constant.Value);
            }
            /*
            else if (_proc.Block.Constants.Exists(i => i.Name == identityName))
            {
                Console.Error.WriteLine("Found in procedure constants: " + identityName);
                Identity constant = _proc.Block.Constants.Single(i => i.Name == identityName);
                result = LoadConstant(constant.Value);
            }
            */
            else if (_proc.Locals.Exists(i => i.Name == identityName))
            {
                //Console.Error.WriteLine("Found as local variable: " + identityName);
                result = "ldloc " + identityName;
            }
            else
            {
                result = "ldsfld int32 plc::" + identityName;
            }
            return result;
        }

        private string StoreIdentityName(string identityName)
        {
            string result;
            if (_proc.Locals.Exists(i => i.Name == identityName))
            {
                result = "stloc " + identityName;
            }
            else
            {
                result = "stsfld int32 plc::" + identityName;
            }
            return result;
        }
        IEnumerable<string> BranchWhenTrue(Condition condition, string label)
        {
            if (condition.Type == ConditionType.True)
            {
                yield return "br " + label;
            }
            else if (condition.Type == ConditionType.False)
            {
                //yield return "nop";
            }
            else if (condition.Type == ConditionType.Odd)
            {
                var oc = (OddCondition) condition;
                foreach (string s in GenerateExpression(oc.Expression))
                {
                    yield return s;
                }
                yield return "ldc.i4.1";
                yield return "and";
                yield return "brtrue " + label;
            }
            else  // BinaryCondition
            {
                var bc = (BinaryCondition) condition;
                foreach (string s in GenerateExpression(bc.FirstExpression))
                {
                    yield return s;
                }
                foreach (string s in GenerateExpression(bc.SecondExpression))
                {
                    yield return s;
                }
                switch (bc.Type)
                {
                    case ConditionType.Equal:
                        yield return "beq " + label;
                        break;
                    case ConditionType.NotEqual:
                        yield return "bne.un " + label;
                        break;
                    case ConditionType.GreaterThan:
                        yield return "bgt " + label;
                        break;
                    case ConditionType.LessThan:
                        yield return "blt " + label;
                        break;
                    case ConditionType.GreaterThanOrEqual:
                        yield return "bge " + label;
                        break;
                    case ConditionType.LessThanOrEqual:
                        yield return "ble " + label;
                        break;
                    default:
                        throw new Exception("Unhandled BinaryCondition");
                }
            }
        }

        IEnumerable<string> BranchWhenFalse(Condition condition, string label)
        {
            if (condition.Type == ConditionType.True)
            {
                //yield return "nop";
            }
            else if (condition.Type == ConditionType.False)
            {
                yield return "br " + label;
            }
            else if (condition.Type == ConditionType.Odd)
            {
                var oc = (OddCondition) condition;
                foreach (string s in GenerateExpression(oc.Expression))
                {
                    yield return s;
                }
                yield return "ldc.i4.1";
                yield return "and";
                yield return "brfalse " + label;
            }
            else // BinaryCondition
            {
                var bc = (BinaryCondition) condition;
                foreach (string s in GenerateExpression(bc.FirstExpression))
                {
                    yield return s;
                }

                foreach (string s in GenerateExpression(bc.SecondExpression))
                {
                    yield return s;
                }

                switch (bc.Type)
                {
                    case ConditionType.Equal:
                        yield return "bne.un " + label;
                        break;
                    case ConditionType.NotEqual:
                        yield return "beq " + label;
                        break;
                    case ConditionType.GreaterThan:
                        yield return "ble " + label;
                        break;
                    case ConditionType.LessThan:
                        yield return "bge " + label;
                        break;
                    case ConditionType.GreaterThanOrEqual:
                        yield return "blt " + label;
                        break;
                    case ConditionType.LessThanOrEqual:
                        yield return "bgt " + label;
                        break;
                    default:
                        throw new Exception("Unhandled BinaryCondition");
                }
            }
        }
        public IEnumerable<string> GenerateExpression(Expression expression)
        {
            if (expression is RandExpression)
            {
                foreach (string s in GenerateRandExpression((RandExpression) expression))
                {
                    yield return s;
                }
            }
            else
            {
                if (expression.ExpressionNodes.Count == 0)
                {
                    Console.WriteLine("Trying to generate an empty expression ( no nodes )");
                }

                var enumerator = expression.ExpressionNodes.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    foreach (string s in GenerateFirstExpressionNode(enumerator.Current))
                    {
                        yield return s;
                    }
                }

                while (enumerator.MoveNext())
                {
                    foreach (string s in GenerateExpressionNode(enumerator.Current))
                    {
                        yield return s;
                    }
                }
            }
        }

        public IEnumerable<string> GenerateRandExpression(RandExpression r)
        {
            yield return "newobj instance void [mscorlib]System.Random::.ctor()";
            foreach (string s in GenerateExpression(r.LowExpression))
            {
                yield return s;
            }
            foreach (string s in GenerateExpression(r.HighExpression))
            {
                yield return s;
            }
            yield return "callvirt instance int32 [mscorlib]System.Random::Next(int32,int32)";
        }

        public IEnumerable<string> GenerateFirstExpressionNode(ExpressionNode node)
        {
            if (node.Term.IsSingleConstantFactor)
            {
                ConstantFactor factor = (ConstantFactor) node.Term.FirstFactor;
                int c = Int32.Parse(factor.Value);
                if (c == 1 && !node.IsPositive)
                {
                    yield return "ldc.i4.m1";
                }
                else
                {
                    yield return LoadConstant(factor.Value);
                    if (!node.IsPositive)
                    {
                        yield return "neg";
                    }
                }
            }
            else
            {
                foreach (string s in GenerateTerm(node.Term))
                {
                    yield return s;
                }

                if (!node.IsPositive)
                {
                    yield return "neg";
                } 
            }
        }
        public IEnumerable<string> GenerateExpressionNode(ExpressionNode node)
        {
            foreach (string s in GenerateTerm(node.Term))
            {
                yield return s;
            }
            yield return node.IsPositive ? "add" : "sub";
        }
        
        public IEnumerable<string> GenerateTerm(Term term)
        {
            var te = term.TermNodes.GetEnumerator();
            te.MoveNext();
            foreach (string s in GenerateFactor(te.Current.Factor))
            {
                yield return s;
            }
            while (te.MoveNext())
            {
                foreach (string s in GenerateFactor(te.Current.Factor))
                {
                    yield return s;
                }
                yield return te.Current.IsDivision ? "div" : "mul";
            }
        }

        string LoadConstant(string constant)
        {
            int c = Int32.Parse(constant);
            if (c >= 0 && c <= 8)
            {
                return "ldc.i4." + c;
            }
            if (c >= 0 && c < 128) // Fits in a single byte
            {
                return "ldc.i4.s " + c;
            }
            /*
            if (c == -1)
            {
                return "ldc.i4.m1";
            }
            */
            return "ldc.i4 " + c;
        }
        public IEnumerable<string> GenerateFactor(Factor factor)
        {
            if (factor is ConstantFactor)
            {
                ConstantFactor nf = (ConstantFactor) factor;
                yield return LoadConstant(nf.Value);
            }
            else if (factor is IdentityFactor)
            {
                IdentityFactor nf = (IdentityFactor) factor;
                yield return LoadIdentityName(nf.IdentityName);
            }
            else if (factor is ExpressionFactor)
            {
                ExpressionFactor ef = (ExpressionFactor) factor;
                foreach (string s in GenerateExpression(ef.Expression))
                { 
                    yield return s;
                }
            }
        }
    }
}