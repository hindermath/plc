using System;
using System.Linq;

namespace PLC
{
    public partial class Parser
    {
        Factor ParseFactor()
        {
            switch (current.Type)
            {
                case TokenType.IntegerConstant:
                    ConstantFactor cf = new();
                    cf.Value = ParseConstant();
                    return cf;
                case TokenType.Identifier:
                    IdentityFactor ff = new();
                    ff.IdentityName = current.Text;
                    ExpectAndConsume(TokenType.Identifier);
                    if (!symbols.Contains(ff.IdentityName))
                    {
                        throw new Exception("Use of undeclared identifier " + ff.IdentityName + " at line " + current.LineNumber);
                    }
                    return ff;
                case TokenType.Parens:
                    ExpressionFactor ef = new();
                    ExpectAndConsume("(");
                    ef.Expression = ParseExpression();
                    ExpectAndConsume(")");
                    return ef;
                default:
                    throw new Exception("Could not parse factor [" + current.Text + "] at line " + current.LineNumber);
            }
        }
    }
}