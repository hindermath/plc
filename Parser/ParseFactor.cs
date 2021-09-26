using System;
using System.Linq;

namespace KNR
{
    public partial class Parser
    {
        public Factor ParseFactor()
        {
            //Console.WriteLine("About to parse a factor. Looking at " + current.Text + " and " + next.Text );
            switch (current.Type)
            {
                case TokenType.IntegerConstant:
                    ConstantFactor cf = new();
                    cf.Value = ParseConstant();
                    //Console.WriteLine("Added Constant Factor: " + cf.Value);
                    return cf;
                case TokenType.Identifier:
                    IdentityFactor ff = new();
                    ff.IdentityName = NameWithoutCollisions(current.Text);
                    ExpectAndConsume(TokenType.Identifier);
                    if (!Program.Block.Constants.Exists(x => x.Name == ff.IdentityName))
                    {
                        if (Program.Block.Variables.Exists(x => x.Name == ff.IdentityName))
                        {
                            Identity identity = Program.Block.Variables.Single(x => x.Name == ff.IdentityName);
                            
                        }
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