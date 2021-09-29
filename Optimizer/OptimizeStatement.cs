#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;

namespace PLC
{
    public partial class Optimizer
    {
        Statement OptimizeCompoundStatement(CompoundStatement bs)
        {
            List<Statement> optimizedStatements = new();
            foreach (Statement s in bs.Statements)
            {
                optimizedStatements.Add(OptimizeStatement(s));
            }
            EliminateIfAfterConstantAssignment(optimizedStatements);
            bs.Statements = optimizedStatements;

            // Replace single statement CompoundStatement with that statement
            if (bs.Statements.Count == 1)
            {
                return bs.Statements[0];
            }

            return bs;
        }

        Statement OptimizeAssignmentStatement(AssignmentStatement s)
        {
            s.Expression = OptimizeExpression(s.Expression);
            try
            {
                Identity identity = _block.Variables.Single(x => x.Name == s.IdentityName);
                identity.AssignmentCount++;
                identity.AssignmentStatements.Add(s);
            }
            catch
            {
            }

            return s;
        }

        Statement OptimizeIfStatement(IfStatement iff)
        {
            iff.Statement = OptimizeStatement(iff.Statement);
            iff.Condition = OptimizeCondition(iff.Condition);
            if (iff.Condition.Type == ConditionType.True)
            {
                return iff.Statement;
            }

            if (iff.Condition.Type == ConditionType.False)
            {
                return new EmptyStatement();
            }

            return iff;
        }

        Statement OptimizeWhileStatement(WhileStatement ws)
        {
            ws.Statement = OptimizeStatement(ws.Statement);
            ws.Condition = OptimizeCondition(ws.Condition);
            if (ws.Condition.Type == ConditionType.False)
            {
                return new EmptyStatement();
            }

            // Perform Loop Inversion
            // Saves two jumps when exiting the loop
            DoWhileStatement dw = new();
            dw.Condition = ws.Condition;
            dw.Statement = ws.Statement;
            // If condition is true, no need even for the IF
            if (dw.Condition.Type == ConditionType.True)
            {
                return dw;
            }
            IfStatement iff = new();
            iff.Condition = ws.Condition;
            iff.Statement = dw;
            return iff;
        }

        Statement OptimizeDoWhileStatement(DoWhileStatement dw)
        {
            dw.Statement = OptimizeStatement(dw.Statement);
            dw.Condition = OptimizeCondition(dw.Condition);
            if (dw.Condition.Type == ConditionType.False)
            {
                return dw.Statement;
            }

            return dw;
        }

        Statement OptimizeCallStatement(CallStatement cs)
        {
            string name = cs.ProcedureName;
            var procs = _block.Procedures;
            try
            {
                Procedure result = procs.Single(x => x.Name == name);
                if (result.Block.Constants.Count == 0 && result.Block.Variables.Count == 0 &&
                    !result.Block.Statement.CallsProcedure)
                {
                    return result.Block.Statement;
                }

                result.CallCount++;
            }
            catch
            {
                ;
            }

            return cs;
        }

        Statement OptimizeWriteStatement(WriteStatement ws)
        {
            if (ws.Message == String.Empty)
            {
                ws.Expression = OptimizeExpression(ws.Expression);
            }

            return ws;
        }

        Statement OptimizeReadStatement(ReadStatement rs)
        {
            /*
            try
            {
                Identity identity = _block.Variables.Single(x => x.Name == rs.IdentityName);
                identity.AssignmentCount++;
            } catch { }
            */

            return rs;
        }

        Statement OptimizeStatement(Statement statement)
        {
            if (statement is CompoundStatement)
                return OptimizeCompoundStatement((CompoundStatement) statement);
            if (statement is AssignmentStatement)
                return OptimizeAssignmentStatement((AssignmentStatement) statement);
            if (statement is IfStatement)
                return OptimizeIfStatement((IfStatement) statement);
            if (statement is WhileStatement)
                return OptimizeWhileStatement((WhileStatement) statement);
            if (statement is DoWhileStatement)
                return OptimizeDoWhileStatement((DoWhileStatement) statement);
            if (statement is CallStatement)
                return OptimizeCallStatement((CallStatement) statement);
            if (statement is WriteStatement)
                return OptimizeWriteStatement((WriteStatement) statement);
            if (statement is ReadStatement)
                return OptimizeReadStatement((ReadStatement) statement);
            return statement;
        }
        
        
        // This looks for the pattern x := 1; IF x < 10 THEN ...
        // If we know x fails this test, skip the IF
        void EliminateIfAfterConstantAssignment(List<Statement> statements)
        {
            List<AssignmentStatement> constantAssignments = new();
            for (int i = 0; i < statements.Count; i++)
            {
                Statement currentStatement = statements[i];
                if (currentStatement is AssignmentStatement &&
                    ((AssignmentStatement) currentStatement).Expression.IsSingleConstantFactor)
                {
                    constantAssignments.Add((AssignmentStatement) currentStatement);
                }
                else if (currentStatement is IfStatement)
                {
                    IfStatement ifStatement = (IfStatement) currentStatement;
                    if (ifStatement.Condition is BinaryCondition)
                    {
                        BinaryCondition bc1 = (BinaryCondition) ifStatement.Condition;
                        BinaryCondition bc2 = new()
                        {
                            FirstExpression = bc1.FirstExpression,
                            SecondExpression = bc1.SecondExpression,
                            Type = bc1.Type
                        };
                        for (int c = constantAssignments.Count - 1; c >= 0; c--)
                        {
                            AssignmentStatement currentAssignment = constantAssignments[c];
                            if (bc2.FirstExpression.IsSingleIdentity)
                            {
                                IdentityFactor identityFactor =
                                    (IdentityFactor) bc2.FirstExpression.ExpressionNodes[0].Term.FirstFactor;
                                if (currentAssignment.IdentityName == identityFactor.IdentityName)
                                {
                                    bc2.FirstExpression = currentAssignment.Expression;
                                }
                            }
                            if (bc2.SecondExpression.IsSingleIdentity)
                            {
                                IdentityFactor identityFactor =
                                    (IdentityFactor) bc2.SecondExpression.ExpressionNodes[0].Term.FirstFactor;
                                if (currentAssignment.IdentityName == identityFactor.IdentityName)
                                {
                                    bc2.SecondExpression = currentAssignment.Expression;
                                }
                            }

                            Condition cond = OptimizeCondition(bc2);
                            if (cond.Type == ConditionType.True)
                            {
                                statements[i] = ifStatement.Statement;
                                return;
                            }

                            if (cond.Type == ConditionType.False)
                            {
                                statements[i] = new EmptyStatement();
                                return;
                            }
                        }
                    }
                    else if (ifStatement.Condition.Type == ConditionType.Odd)
                    {
                        var oc1 = (OddCondition) ifStatement.Condition;
                        OddCondition oc2 = new() {Expression = oc1.Expression};
                        if (oc2.Expression.IsSingleIdentity)
                        {
                            IdentityFactor identityFactor =
                                (IdentityFactor) oc2.Expression.ExpressionNodes[0].Term.FirstFactor;

                            for (int c = constantAssignments.Count - 1; c >= 0; c--)
                            {
                                AssignmentStatement currentAssignment = constantAssignments[c];
                                if (currentAssignment.IdentityName == identityFactor.IdentityName)
                                {
                                    oc2.Expression = currentAssignment.Expression;
                                    Condition cond = OptimizeCondition(oc2);
                                    if (cond.Type == ConditionType.True)
                                    {
                                        statements[i] = ifStatement.Statement;
                                        return;
                                    }

                                    if (cond.Type == ConditionType.False)
                                    {
                                        statements[i] = new EmptyStatement();
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    constantAssignments.Clear();
                }
                else
                {
                    constantAssignments.Clear();
                }
            }
        }
    }
}