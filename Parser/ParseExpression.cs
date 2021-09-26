#nullable enable

namespace KNR
{
    public partial class Parser
    {
        public Expression ParseExpression()
        {
            if (current.Text == "RAND")
            {
                ExpectAndConsume("RAND");
                RandExpression r = new();
                r.LowExpression = ParseExpression();
                r.HighExpression = ParseExpression();

                return r;
            }
            Expression e = new();
            
            // Handle first term differently since + / - are optional
            ExpressionNode en = new();
            if (current.Text == "+" || current.Text == "-")
            {
                if (current.Text == "-")
                {
                    en.IsPositive = false;
                    ExpectAndConsume("-");
                }
                if (current.Text == "+")
                {
                    en.IsPositive = true;
                    ExpectAndConsume("+");
                }
            }
            en.Term = ParseTerm();
            e.ExpressionNodes.Add(en);
            
            // All the other terms...
            while (current.Text == "+" || current.Text == "-")
            {
                var node = new ExpressionNode();
                if (current.Text == "+")
                {
                    node.IsPositive = true;
                    ExpectAndConsume("+");
                }
                else
                {
                    node.IsPositive = false;
                    ExpectAndConsume("-");
                }

                node.Term = ParseTerm();
                e.ExpressionNodes.Add(node);
            }
            return e;
        }
    }
}