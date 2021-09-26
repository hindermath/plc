#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;

namespace KNR
{
    public partial class Optimizer
    {
        private Block _block;

        public Optimizer()
        {
            _block = new Block();
        }

        public ParsedProgram Optimize(ParsedProgram program)
        {
            _block = program.Block;
            program.Block = OptimzeBlock(_block);

            List<Procedure> procsToRemove = new();
            foreach (var proc in _block.Procedures)
            {
                //Console.WriteLine("Optimizing procedure " + proc.Name);
                proc.Block = OptimzeBlock(proc.Block);
                OptimzeProcedureTailCall(proc);
            }

            foreach (var proc in _block.Procedures)
            {
                if (proc.CallCount == 0)
                {
                    procsToRemove.Add(proc);
                }
            }

            foreach (var procToRemove in procsToRemove)
            {
                _block.Procedures.Remove(procToRemove);
            }

            List<Identity> variablesToRemove = new();
            foreach (var variable in _block.Variables)
            {
                /*Console.WriteLine("{0} is assigned {1} times and called {2} times", variable.Name,
                    variable.AssignmentCount, variable.ReferenceCount);
                */
                if (variable.ReferenceCount == 00)
                {
                    variablesToRemove.Add(variable);
                }

                if (variable.AssignmentCount == 1)
                {
                    var ass = variable.AssignmentStatements.First();
                    //Console.WriteLine(ass.IdentityName);
                    if (ass.Expression.IsSingleConstantFactor)
                    {
                        ConstantFactor cf = (ConstantFactor) ass.Expression.ExpressionNodes[0].Term.TermNodes[0].Factor;
                        variable.Value = cf.Value;
                        //Console.WriteLine("Making constant");
                        _block.Constants.Add(variable);
                        variablesToRemove.Add(variable);
                        ass.SkipGeneration = true;
                    }
                }
            }

            foreach (var variable in variablesToRemove)
            {
                _block.Variables.Remove(variable);
            }

            program.Block = OptimzeBlock(_block);
            return program;
        }

        // Convert procedure call into a loop for a very specific case
        void OptimzeProcedureTailCall(Procedure proc)
        {
            if (proc.Block.Statement is IfStatement)
            {
                var iff = (IfStatement) proc.Block.Statement;
                if (iff.Statement is CompoundStatement)
                {
                    var bs = (CompoundStatement) iff.Statement;
                    if (bs.Statements.Last() is CallStatement)
                    {
                        CallStatement ls = (CallStatement) bs.Statements.Last();
                        if (ls.ProcedureName == proc.Name)
                        {
                            // Procedure calls itself
                            bs.Statements.Remove(ls);
                            DoWhileStatement dw = new();
                            dw.Condition = iff.Condition;
                            dw.Statement = iff.Statement;
                            iff.Statement = dw;
                            proc.CallCount--;
                        }
                    }
                }
            }
        }
    }
}