using System.Linq.Expressions;
using System.Net;
using System.Text.RegularExpressions;

#nullable enable

namespace PLC
{
    public partial class Parser
    {
        Statement ParseStatement()
        {
            switch (current.Text)
            {
                case "CALL":
                    var cs = new CallStatement();
                    ExpectAndConsume("CALL");
                    cs.ProcedureName = ParseIdentifier();
                    return cs;
                case "WRITE":
                    var ws = new WriteStatement();
                    ExpectAndConsume("WRITE");
                    if (current.Type == TokenType.StringConstant)
                    {
                        ws.Message = current.Text;
                        ExpectAndConsume(TokenType.StringConstant);
                    }
                    else
                    {
                        ws.Expression = ParseExpression();
                    }
                    return ws;
                case "READ":
                    var rs = new ReadStatement();
                    ExpectAndConsume("READ");
                    if (current.Type == TokenType.StringConstant)
                    {
                        rs.Message = current.Text;
                        ExpectAndConsume(TokenType.StringConstant);
                    }
                    rs.IdentityName = ParseIdentifier();
                    return rs;
                case "BEGIN":
                    ExpectAndConsume("BEGIN");
                    var bs = new CompoundStatement();
                    while (current.Text != "END")
                    {
                        bs.Statements.Add(ParseStatement());
                        if (current.Text != "END")
                        {
                            ExpectAndConsumeTerminator();
                        }
                    }

                    ExpectAndConsume("END");
                    return bs;
                case "IF":
                    IfStatement iss = new();
                    ExpectAndConsume("IF");
                    iss.Condition = ParseCondition();
                    ExpectAndConsume("THEN");
                    iss.Statement = ParseStatement();
                    return iss;
                case "DO":
                    DoWhileStatement dw = new();
                    ExpectAndConsume("DO");
                    dw.Statement = ParseStatement();
                    if (current.Text == "WHILE")
                    {
                        ExpectAndConsume("WHILE");
                        dw.Condition = ParseCondition();
                    }
                    else
                    {
                        dw.Condition = new TrueCondition();
                    }
                    return dw;
                case "WHILE":
                    ExpectAndConsume("WHILE");
                    if (next.Text == ":=") // If := then this is a FOR statement
                    {
                        return ParseForStaatement();
                    }
                    else  // A traditional WHILE statement
                    {
                        WhileStatement ww = new();
                        ww.Condition = ParseCondition();
                        ExpectAndConsume("DO");
                        ww.Statement = ParseStatement();
                        return ww;
                    }
                case ";":
                    ExpectAndConsumeTerminator();
                    return new EmptyStatement();
                default: // Assignment
                    AssignmentStatement ass = new();
                    ass.IdentityName = ParseIdentifier();
                    ExpectAndConsume(":=");
                    ass.Expression = ParseExpression();
                    return ass;
            }
        }

        Statement ParseForStaatement()
        { 
            CompoundStatement forStatement = new();
            string identifierName = ParseIdentifier();
            ExpectAndConsume(":=");
            var startExpression = ParseExpression();
            AssignmentStatement a = new() {IdentityName = identifierName, Expression = startExpression};
            forStatement.Statements.Add(a);
            WhileStatement whileStatement = new();
            forStatement.Statements.Add(whileStatement);
            
            ExpectAndConsume("TO");
            var endExpression = ParseExpression();
            var indexVariableExpression = new SingleIdentityExpression(identifierName);
            BinaryCondition bc = new() {
                FirstExpression = indexVariableExpression, 
                SecondExpression = endExpression, 
                Type = ConditionType.LessThanOrEqual
            };
            whileStatement.Condition = bc;
            
            Expression stepExpression;
            ExpressionNode identifierNode = new() {Term = indexVariableExpression.ExpressionNodes[0].Term};
            if (current.Text == "STEP")
            {
                ExpectAndConsume("STEP");
                stepExpression = ParseExpression();
                stepExpression.ExpressionNodes.Add(identifierNode);
            }
            else
            {
                stepExpression = new Expression();
                stepExpression.ExpressionNodes.Add(identifierNode);
                stepExpression.ExpressionNodes.Add(new ConstantExpression("1").ExpressionNodes[0]);
            }
            AssignmentStatement incrementIndex = new() {IdentityName = identifierName, Expression = stepExpression};

            ExpectAndConsume("DO");
            Statement body = ParseStatement();
            CompoundStatement compountStatement;
            if (body is CompoundStatement)
            {
                compountStatement = (CompoundStatement) body;
                compountStatement.Statements.Add(incrementIndex);
            }
            else
            {
                compountStatement = new CompoundStatement();
                compountStatement.Statements.Add(body);
                compountStatement.Statements.Add(incrementIndex);
            }
            whileStatement.Statement = compountStatement;
            return forStatement;
        }
    }
}