#nullable enable
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Authentication.ExtendedProtection;

namespace PLC
{
    public class PL0Generator : IGenerator
    {
        readonly Dictionary<ConditionType, string> _conditionDict = new()
        {
            {ConditionType.Equal, "="},
            {ConditionType.NotEqual, "#"},
            {ConditionType.LessThan, "<"},
            {ConditionType.LessThanOrEqual, "<="},
            {ConditionType.GreaterThan, ">"},
            {ConditionType.GreaterThanOrEqual, ">="}
        };
        public PL0Generator(ParsedProgram program)
        {
            Program = program;
        }
        
        public ParsedProgram Program { get; set; }

        public int Compile(string filename)
        {
            return 1;
        }
        public IEnumerable<string> Generate()
        {
            // Generate constant declarations
            var constants = Program.Block.Constants;
            int c = constants.Count;
            if (c != 0)
            {
                StringBuilder sb = new();
                sb.Append("CONST ");
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
                yield return sb.ToString();
            }
            
            // Variable declarations
            var variables = Program.Block.Variables;
            c = variables.Count;
            if (c != 0)
            {
                StringBuilder sb = new();
                sb.Append("VAR ");
                for (int i = 0; i < c; i++)
                {
                    sb.Append(variables.ElementAt(i).Name);
                    if (i < (c - 1))
                    {
                        sb.Append(", ");
                    }
                }

                sb.Append(";");
                yield return sb.ToString();
                yield return String.Empty; // Just makes it look prettier
            }
            
            foreach (Procedure method in Program.Block.Procedures)
            {
                yield return "PROCEDURE " + method.Name + ";";
                var enumerator = GenerateBlock(method.Block).GetEnumerator();
                bool keepGoing = enumerator.MoveNext();
                while (keepGoing)
                {
                    string currentLine = enumerator.Current;
                    if (enumerator.MoveNext())
                    {
                        yield return currentLine;
                    } else
                    {
                        keepGoing = false;
                        yield return currentLine + ";";
                    }
                }
                yield return String.Empty;    // Just makes it prettier
            }
            
            // Generate the Main body of the program
            var line_enumerator = GenerateStatement(Program.Block.Statement).GetEnumerator();
            bool moreLines = line_enumerator.MoveNext();
            while (moreLines)
            {
                string currentLine = line_enumerator.Current;
                if (line_enumerator.MoveNext())
                {
                    yield return currentLine;
                } else
                {
                    moreLines = false;
                    yield return currentLine + ".";
                }
            }
        }
        IEnumerable<string> GenerateBlock(Block block)
        {
            string constants = GenerateConstantDeclarations(block.Constants);
            if (constants != String.Empty)
            {
                yield return constants;
            }

            string variables = GenerateVariableDeclarations(block.Variables);
            if (variables != String.Empty)
            {
                yield return variables;
            }

            foreach (string s in GenerateStatement(block.Statement))
            {
                yield return s;
            }
        }

        string GenerateConstantDeclarations(List<Identity> constants)
        {
            int c = constants.Count;
            if (c == 0)
            {
                return String.Empty;
            }

            StringBuilder sb = new();
            sb.Append("CONST ");
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

        string GenerateVariableDeclarations(List<Identity> variables)
        {
            int c = variables.Count;
            if (c == 0)
            {
                return String.Empty;
            }

            StringBuilder sb = new();
            sb.Append("VAR ");
            var enumerator = variables.GetEnumerator();
            bool keepGoing = enumerator.MoveNext();
            while (keepGoing)
            {
                sb.Append(enumerator.Current.Name);
                if (enumerator.MoveNext())
                {
                    sb.Append(", ");
                }
                else
                {
                    keepGoing = false;
                }
            }
            sb.Append(";");
            return sb.ToString();
        }

        IEnumerable<string> GenerateStatement(Statement statement)
        {
            if (statement is WriteStatement)
            {
                var writeStatement = (WriteStatement) statement;
                if (writeStatement.Message == String.Empty)
                {
                    yield return "WRITE " + GenerateExpression(writeStatement.Expression);
                }
                else
                {
                    yield return "WRITE \"" + writeStatement.Message + "\"";
                }
            }
            else if (statement is ReadStatement)
            {
                var readStatement = (ReadStatement) statement;
                if (readStatement.Message != String.Empty)
                {
                    yield return "READ \"" + readStatement.Message + "\" " + readStatement.IdentityName;
                }
                else
                {
                    yield return "READ " + readStatement.IdentityName;
                }
            }
            else if (statement is AssignmentStatement)
            {
                var assignmentStatement = (AssignmentStatement) statement;
                yield return assignmentStatement.IdentityName + " := " +
                             GenerateExpression(assignmentStatement.Expression);
            }
            else if (statement is CallStatement)
            {
                var cs = (CallStatement) statement;
                yield return "CALL " + cs.ProcedureName;
            }
            else if (statement is CompoundStatement)
            {
                var bs = (CompoundStatement) statement;
                if (bs.Statements.Count == 1)
                {
                    foreach (string line in GenerateStatement(bs.Statements.First()))
                    {
                        yield return line;
                    }
                }
                else
                {
                    yield return "BEGIN";
                    var statement_enumerator = bs.Statements.GetEnumerator();
                    bool moreStatements = statement_enumerator.MoveNext();
                    while (moreStatements)
                    {
                        var currentStatement = statement_enumerator.Current;
                        if (currentStatement.SkipGeneration)
                        {
                            moreStatements = statement_enumerator.MoveNext();
                        }
                        else
                        {
                            var line_enumerator = GenerateStatement(currentStatement).GetEnumerator();
                            bool moreLines = line_enumerator.MoveNext();
                            while (moreLines)
                            {
                                string line = line_enumerator.Current;
                                if (line_enumerator.MoveNext())
                                {
                                    yield return "    " + line;
                                }
                                else
                                {
                                    moreLines = false;
                                    moreStatements = statement_enumerator.MoveNext();
                                    if (moreStatements)
                                    {
                                        yield return "    " + line + ";";
                                    }
                                    else
                                    {
                                        yield return "    " + line;
                                    }
                            
                                }
                            }
                        }
                    }
                    yield return "END";
                }
            }
            else if (statement is IfStatement)
            {
                var iff = (IfStatement) statement;
                var lines = GenerateStatement(iff.Statement);
                if (lines.Count() == 1)
                {
                    yield return "IF " + GenerateCondition(iff.Condition) + " THEN " + lines.First();
                }
                else
                {
                    yield return "IF " + GenerateCondition(iff.Condition) + " THEN";
                    foreach (string s in GenerateStatement(iff.Statement))
                    {
                        yield return s;
                    }
                }
            }
            else if (statement is DoWhileStatement)
            {
                var dw = (DoWhileStatement) statement;
                var lines = GenerateStatement(dw.Statement);
                if (lines.Count() == 1)
                {
                    yield return  "DO " + lines.First();
                }
                else
                {
                    yield return "DO";
                    foreach (string s in GenerateStatement(dw.Statement))
                    {
                        yield return s;
                    }
                }
                if (dw.Condition.Type != ConditionType.True)
                {
                    yield return "WHILE " + GenerateCondition(dw.Condition);
                }
            }
            else if (statement is WhileStatement)
            {
                var ws = (WhileStatement) statement;
                var lines = GenerateStatement(ws.Statement);
                if (lines.Count() == 1)
                {
                    yield return "WHILE " + GenerateCondition(ws.Condition) + " DO " + lines.First();
                }
                else
                {
                    yield return "WHILE " + GenerateCondition(ws.Condition) + " DO";
                    foreach (string s in GenerateStatement(ws.Statement))
                    {
                        yield return s;
                    }
                }
            }
            else // Must be empty statement
            {
                //yield return "";
            }
        }

        string GenerateExpression(Expression expression)
        {
            if (expression is RandExpression)
            {
                return GenerateRandExpression((RandExpression) expression);
            }
            StringBuilder sb = new();
            if (expression.ExpressionNodes.Count == 0)
            {
                Console.WriteLine("Trying to generate an empty expression ( no nodes )");
            }

            var enumerator = expression.ExpressionNodes.GetEnumerator();
            if (enumerator.MoveNext())
            {
                sb.Append(GenerateFirstExpressionNode(enumerator.Current));
            }

            while (enumerator.MoveNext())
            {
                sb.Append(GenerateExpressionNode(enumerator.Current));
            }
            return sb.ToString();
        }

        string GenerateRandExpression(RandExpression r)
        {
            string low = GenerateExpression(r.LowExpression);
            string high = GenerateExpression(r.HighExpression);
            return "RAND " + low + " " + high;
        }

        string GenerateFirstExpressionNode(ExpressionNode node)
        {
            return (node.IsPositive ? String.Empty : "-") + GenerateTerm(node.Term);
        }
        string GenerateExpressionNode(ExpressionNode node)
        {
            return  (node.IsPositive ? "+" : "-") + GenerateTerm(node.Term);
        }
        
        string GenerateTerm(Term term)
        {
            StringBuilder sb = new();
            var te = term.TermNodes.GetEnumerator();
            te.MoveNext();
            sb.Append(GenerateFactor(te.Current.Factor));
            while (te.MoveNext())
            {
                sb.Append(te.Current.IsDivision ? "/" : "*");
                sb.Append(GenerateFactor(te.Current.Factor)); 
            }
            return sb.ToString();
        }

        string GenerateFactor(Factor factor)
        {
            if (factor == null)
            {
                Console.WriteLine("Trying to generate null factor");
            }
            if (factor is ConstantFactor)
            {
                ConstantFactor nf = (ConstantFactor) factor;
                return nf.Value;
            }
            else if (factor is IdentityFactor)
            {
                IdentityFactor nf = (IdentityFactor) factor;
                return nf.IdentityName;
            }
            else if (factor is ExpressionFactor)
            {
                ExpressionFactor ef = (ExpressionFactor) factor;
                string ex = GenerateExpression(ef.Expression);
                if (ef.Expression.IsSingleTerm)
                {
                    return ex;
                }
                return "(" + ex + ")";
            }
            throw new Exception("Could not generate factor");
        }

        string GenerateCondition(Condition condition)
        {
            if (condition is BinaryCondition)
            {
                var binaryCondition = (BinaryCondition) condition;
                return GenerateExpression(binaryCondition.FirstExpression) +
                       " " + _conditionDict[binaryCondition.Type] + " " +
                       GenerateExpression(binaryCondition.SecondExpression);
            }
            else  // Odd condition
            {
                var oddCondition = (OddCondition) condition;
                return "ODD " + GenerateExpression(oddCondition.Expression);
            }
        }
    }
}