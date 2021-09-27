#nullable enable
using System.Linq;

namespace PLC
{
    public partial class Optimizer
    {
        Factor OptimizeFactor(Factor factor)
        {
            //Console.WriteLine("Optimizing factor");
            if (factor is IdentityFactor)
            {
                var iff = (IdentityFactor) factor;
                string name = iff.IdentityName;
                
                // If it is a constant, return ConstantFactor instead
                try
                {
                    var identity = _block.Constants.Single(x => x.Name == name);
                    ConstantFactor f = new();
                    f.Value = identity.Value;
                    return f;
                }
                catch
                {
                    ;
                }

                // If it is a variable, increment the number of times it has been referenced
                try
                {
                    var identity = _block.Variables.Single(x => x.Name == name);
                    identity.ReferenceCount++;
                    identity.IdentityFactors.Add(iff);
                }
                catch
                {
                    ;
                }
            }

            if (factor is ExpressionFactor)
            {
                var ef = (ExpressionFactor) factor;
                ef.Expression = OptimizeExpression(ef.Expression);
                // Convert ExpressionFactor to ConstantFactor constant
                if (ef.Expression.IsSingleConstantFactor)
                {
                    ExpressionNode firstNode = ef.Expression.ExpressionNodes[0];
                    if (firstNode.IsPositive)
                    {
                        return firstNode.Term.FirstFactor;
                    }
                }
                return ef;
            }
            return factor;
        }
    }
}