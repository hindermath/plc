//#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace PLC
{
    public class CLRGenerator
    {
        private Procedure _proc;

        private readonly AssemblyBuilder _asmb;
        private ILGenerator _il;
        private readonly ModuleBuilder _modb;
        private readonly string _moduleName;
        private readonly Dictionary<string, FieldBuilder> globalTable;
        private static Dictionary<string, MethodBuilder> methodTable;
        private readonly Dictionary<string, Dictionary<string, LocalBuilder>> methodLocalsTable;
        private readonly TypeBuilder _typeBuilder;
        private MethodBuilder _methodBuilder;

        public CLRGenerator(ParsedProgram program, string moduleName = "program")
        {
            Program = program;
            foreach (Identity constant in program.Block.Constants) {
                program.Globals.Add(constant);
            }
            foreach (Identity variable in program.Block.Variables) {
                program.Globals.Add(variable);
            }
            _moduleName = moduleName;
            if (Path.GetFileName(moduleName) != moduleName)
            {
                throw new Exception("can only output into current directory!");
            }

            string filename = Path.GetFileNameWithoutExtension(moduleName);
            AssemblyName asmName = new(filename);
            _asmb = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Save);
            _modb = _asmb.DefineDynamicModule(moduleName);
            _typeBuilder = _modb.DefineType("plc");
            globalTable = new Dictionary<string, FieldBuilder>();
            methodTable = new Dictionary<string, MethodBuilder>();
            methodLocalsTable = new Dictionary<string, Dictionary<string, LocalBuilder>>();
        }
        
        public ParsedProgram Program { get; }

        public IEnumerable<string> Generate()
        {
            yield return "Text output is not available for this back-end -- call Compile() to generate a binary";
        }

        public int Compile()
        {
            // Generate constant declarations
            foreach (Identity constant in Program.Block.Constants) {
                var fieldBuilder = _typeBuilder.DefineField(constant.Name, typeof(int), FieldAttributes.Static | FieldAttributes.Private | FieldAttributes.Literal);
                fieldBuilder.SetConstant(Int32.Parse(constant.Value));
            }
            
            // Variable declarations
            foreach (Identity variable in Program.Block.Variables) {
                var fieldBuilder = _typeBuilder.DefineField(variable.Name, typeof(int), FieldAttributes.Static | FieldAttributes.Private);
                globalTable.Add(variable.Name, fieldBuilder);
            }
            
            // Procedure definitions
            foreach (Procedure proc in Program.Block.Procedures)
            {
                _proc = proc;
                GenerateProcedure(_proc);
            }
            
            // Main program
            _proc = new Procedure() {Name = "Main", Block = new Block() {Statement = Program.Block.Statement}};
            GenerateProcedure(_proc, true);
            
            // Actually write out the assembly
            _typeBuilder.CreateType();
            _modb.CreateGlobalFunctions();
            _asmb.Save(_moduleName);

            return 0; // Success!!
        }

        void GenerateProcedure(Procedure proc, bool procIsEntryPoint = false)
        {
            _proc = proc;
            _methodBuilder = _typeBuilder.DefineMethod(proc.Name, MethodAttributes.Static, typeof(void), Type.EmptyTypes);
            if (procIsEntryPoint) { _asmb.SetEntryPoint(_methodBuilder); }
            methodTable[proc.Name] = _methodBuilder;
            _il = _methodBuilder.GetILGenerator();
            GenerateBlock(proc.Block);
            _il.Emit(OpCodes.Ret);
        }
        public void GenerateBlock(Block block)
        {
            Dictionary<string, LocalBuilder> localTable = new();
            methodLocalsTable[_proc.Name] = localTable;
            foreach (Identity variable in block.Variables)
            {
                localTable[variable.Name] = _il.DeclareLocal(typeof(int));
                _proc.Locals.Add(variable);
            }
            GenerateStatement(block.Statement);
        }

        public void GenerateStatement(Statement statement)
        {
            if (statement is WriteStatement)
            {
                var writeStatement = (WriteStatement) statement;
                if (String.IsNullOrEmpty(writeStatement.Message))
                {
                    GenerateExpression(writeStatement.Expression);
                    _il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new[] { typeof(int) }));
                }
                else
                {
                    _il.Emit(OpCodes.Ldstr, writeStatement.Message);
                    _il.Emit(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new[] { typeof(string) }));
                }
            }
            else if (statement is ReadStatement)
            {
                var readStatement = (ReadStatement) statement;
                if (!String.IsNullOrEmpty(readStatement.Message))
                {
                    _il.Emit(OpCodes.Ldstr, readStatement.Message);
                    _il.Emit(OpCodes.Call, typeof(Console).GetMethod("Write", new[] { typeof(string) }));
                }
                _il.Emit(OpCodes.Call,
                    typeof(Console).GetMethod("ReadLine", BindingFlags.Public | BindingFlags.Static, null,  new Type[] { }, null));

                var localsTable = methodLocalsTable[_proc.Name];
                if (localsTable.ContainsKey(readStatement.IdentityName))
                {
                    _il.Emit(OpCodes.Ldloca, localsTable[readStatement.IdentityName]);
                }
                else
                {
                    _il.Emit(OpCodes.Ldsflda, globalTable[readStatement.IdentityName]);
                }
                // This typeof(int).MakeRefType() thing took me at least an hour to figure out :-)
                _il.Emit(OpCodes.Call, typeof(int).GetMethod("TryParse", new[] { typeof(string), typeof(int).MakeByRefType()}));
                _il.Emit(OpCodes.Pop);
            }
            else if (statement is AssignmentStatement)
            {
                var assignmentStatement = (AssignmentStatement) statement;
                GenerateExpression(assignmentStatement.Expression);
                StoreIdentityName(assignmentStatement.IdentityName);
            }
            else if (statement is CallStatement)
            {
                var cs = (CallStatement) statement;
                _il.Emit(OpCodes.Call, methodTable[cs.ProcedureName]);
            }
            else if (statement is CompoundStatement)
            {
                var bs = (CompoundStatement) statement;
                foreach (Statement st in bs.Statements.Where(x => !x.SkipGeneration))
                {
                    GenerateStatement(st);
                }
            }
            else if (statement is IfStatement)
            {
                var iff = (IfStatement) statement;
                var label = _il.DefineLabel();
                BranchWhenFalse(iff.Condition, label);
                GenerateStatement(iff.Statement);
                _il.MarkLabel(label);
            }
            else if (statement is DoWhileStatement)
            {
                var dw = (DoWhileStatement) statement;
                var label = _il.DefineLabel();
                _il.MarkLabel(label);
                GenerateStatement(dw.Statement);
                BranchWhenTrue(dw.Condition, label);
            }
            else if (statement is WhileStatement)
            {
                var startLabel = _il.DefineLabel();
                var endLabel = _il.DefineLabel();
                _il.MarkLabel(startLabel);
                var ws = (WhileStatement) statement;
                BranchWhenFalse(ws.Condition, endLabel);
                GenerateStatement(ws.Statement);
                _il.Emit(OpCodes.Br, startLabel);
                _il.MarkLabel(endLabel);
            }
            else    // Must be empty statement
            {
                // "nop";
            }
        }
        private void LoadIdentityName(string identityName)
        {   // CIL requires that "literals" ( constants ) be propagated
            // This is not an optimization -- it is a requirement
            if (Program.Block.Constants.Exists(i => i.Name == identityName))
            {
                Identity constant = Program.Block.Constants.Single(i => i.Name == identityName);
                LoadConstant(constant.Value);
            }
            else
            {
                var localTable = methodLocalsTable[_proc.Name];
                if (localTable.ContainsKey(identityName))
                {
                    _il.Emit(OpCodes.Ldloc, localTable[identityName]);
                }
                else
                {
                    _il.Emit(OpCodes.Ldsfld, globalTable[identityName]);
                }
            }
        }

        private void StoreIdentityName(string identityName)
        {
            var localTable = methodLocalsTable[_proc.Name];
            if (localTable.ContainsKey(identityName))
            {
                _il.Emit(OpCodes.Stloc, localTable[identityName]);
            }
            else
            {
                _il.Emit(OpCodes.Stsfld, globalTable[identityName]);
            }
        }
        void BranchWhenTrue(Condition condition, Label label)
        {
            if (condition.Type == ConditionType.True)
            {
                _il.Emit(OpCodes.Br, label);
            }
            else if (condition.Type == ConditionType.False)
            {
                //yield return "nop";
            }
            else if (condition.Type == ConditionType.Odd)
            {
                var oc = (OddCondition) condition;
                GenerateExpression(oc.Expression);
                _il.Emit(OpCodes.Ldc_I4_1);
                _il.Emit(OpCodes.And);
                _il.Emit(OpCodes.Brtrue, label);
            }
            else  // BinaryCondition
            {
                var bc = (BinaryCondition) condition;
                GenerateExpression(bc.FirstExpression);
                GenerateExpression(bc.SecondExpression);
                switch (bc.Type)
                {
                    case ConditionType.Equal:
                        _il.Emit(OpCodes.Beq, label);
                        break;
                    case ConditionType.NotEqual:
                        _il.Emit(OpCodes.Bne_Un, label);
                        break;
                    case ConditionType.GreaterThan:
                        _il.Emit(OpCodes.Bgt, label);
                        break;
                    case ConditionType.LessThan:
                        _il.Emit(OpCodes.Blt, label);
                        break;
                    case ConditionType.GreaterThanOrEqual:
                        _il.Emit(OpCodes.Bge, label);
                        break;
                    case ConditionType.LessThanOrEqual:
                        _il.Emit(OpCodes.Ble, label);
                        break;
                    default:
                        throw new Exception("Unhandled BinaryCondition");
                }
            }
        }

        void BranchWhenFalse(Condition condition, Label label)
        {
            if (condition.Type == ConditionType.True)
            {
                //yield return "nop";
            }
            else if (condition.Type == ConditionType.False)
            {
                _il.Emit(OpCodes.Br, label);
            }
            else if (condition.Type == ConditionType.Odd)
            {
                var oc = (OddCondition) condition;
                GenerateExpression(oc.Expression);
                _il.Emit(OpCodes.Ldc_I4_1);
                _il.Emit(OpCodes.And);
                _il.Emit(OpCodes.Brfalse, label);
            }
            else // BinaryCondition
            {
                var bc = (BinaryCondition) condition;
                GenerateExpression(bc.FirstExpression);
                GenerateExpression(bc.SecondExpression);

                switch (bc.Type)
                {
                    case ConditionType.Equal:
                        _il.Emit(OpCodes.Bne_Un, label);
                        break;
                    case ConditionType.NotEqual:
                        _il.Emit(OpCodes.Beq, label);
                        break;
                    case ConditionType.GreaterThan:
                        _il.Emit(OpCodes.Ble, label);
                        break;
                    case ConditionType.LessThan:
                        _il.Emit(OpCodes.Bge, label);
                        break;
                    case ConditionType.GreaterThanOrEqual:
                        _il.Emit(OpCodes.Blt, label);
                        break;
                    case ConditionType.LessThanOrEqual:
                        _il.Emit(OpCodes.Bgt, label);
                        break;
                    default:
                        throw new Exception("Unhandled BinaryCondition");
                }
            }
        }
        public void GenerateExpression(Expression expression)
        {
            if (expression is RandExpression)
            {
                GenerateRandExpression((RandExpression) expression);
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
                    GenerateFirstExpressionNode(enumerator.Current);
                }

                while (enumerator.MoveNext())
                {
                    GenerateExpressionNode(enumerator.Current);
                }
            }
        }

        public void GenerateRandExpression(RandExpression r)
        {
            // This took me forever - I was calling typeof(Random).GetConstructor() and that does not work
            _il.Emit(OpCodes.Newobj, typeof(Random).GetConstructors()[0]);
            GenerateExpression(r.LowExpression);
            GenerateExpression(r.HighExpression);
            _il.Emit(OpCodes.Callvirt, typeof(Random).GetMethod("Next", new[] {typeof(int), typeof(int)}));
        }

        public void GenerateFirstExpressionNode(ExpressionNode node)
        {
            if (node.Term.IsSingleConstantFactor)
            {
                ConstantFactor factor = (ConstantFactor) node.Term.FirstFactor;
                int c = Int32.Parse(factor.Value);
                if (c == 1 && !node.IsPositive)
                {
                    _il.Emit(OpCodes.Ldc_I4_M1);
                }
                else
                {
                    LoadConstant(factor.Value);
                    if (!node.IsPositive)
                    {
                        _il.Emit(OpCodes.Neg);
                    }
                }
            }
            else
            {
                GenerateTerm(node.Term);
                if (!node.IsPositive)
                {
                    _il.Emit(OpCodes.Neg);
                } 
            }
        }
        public void GenerateExpressionNode(ExpressionNode node)
        {
            GenerateTerm(node.Term);
            _il.Emit(node.IsPositive ? OpCodes.Add : OpCodes.Sub );
        }
        
        public void GenerateTerm(Term term)
        {
            var te = term.TermNodes.GetEnumerator();
            te.MoveNext();
            GenerateFactor(te.Current.Factor);
            while (te.MoveNext())
            {
                GenerateFactor(te.Current.Factor);
                _il.Emit((te.Current.IsDivision) ? OpCodes.Div : OpCodes.Mul);
            }
        }

        void LoadConstant(string constant)
        {
            int c = Int32.Parse(constant);
            switch (c)
            {
                case 0: _il.Emit(OpCodes.Ldc_I4_0); break;
                case 1: _il.Emit(OpCodes.Ldc_I4_1); break;
                case 2: _il.Emit(OpCodes.Ldc_I4_2); break;
                case 3: _il.Emit(OpCodes.Ldc_I4_3); break;
                case 4: _il.Emit(OpCodes.Ldc_I4_4); break;
                case 5: _il.Emit(OpCodes.Ldc_I4_5); break;
                case 6: _il.Emit(OpCodes.Ldc_I4_6); break;
                case 7: _il.Emit(OpCodes.Ldc_I4_7); break;
                case 8: _il.Emit(OpCodes.Ldc_I4_8); break;
            default:
                if (c >= 0 && c < 128) // Fits in a single byte
                {
                    _il.Emit(OpCodes.Ldc_I4_S, c);
                }
                else
                {
                    _il.Emit(OpCodes.Ldc_I4, c);
                }
                break;
            }
        }
        public void GenerateFactor(Factor factor)
        {
            if (factor is ConstantFactor)
            {
                ConstantFactor nf = (ConstantFactor) factor;
                LoadConstant(nf.Value);
            }
            else if (factor is IdentityFactor)
            {
                IdentityFactor nf = (IdentityFactor) factor;
                LoadIdentityName(nf.IdentityName);
            }
            else if (factor is ExpressionFactor)
            {
                ExpressionFactor ef = (ExpressionFactor) factor;
                GenerateExpression(ef.Expression);
            }
        }
    }
}