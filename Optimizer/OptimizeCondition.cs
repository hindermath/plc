#nullable enable

namespace PLC
{
    public partial class Optimizer
    {
        Condition OptimizeCondition(Condition condition)
        {
            if (condition is OddCondition)
            {
                var oc = (OddCondition) condition;
                oc.Expression = OptimizeExpression(oc.Expression);
                if (oc.Expression.IsSingleConstantFactor)
                {
                    ConstantFactor constant = (ConstantFactor) oc.Expression.ExpressionNodes[0].Term.FirstFactor;
                    int c = System.Int32.Parse(constant.Value);
                    if ((c & 1) > 0)
                    {
                        return new FalseCondition();
                    }
                    else
                    {
                        return new TrueCondition();
                    }
                }
                return oc;
            }
            else if (condition is BinaryCondition)
            {
                var bc = (BinaryCondition) condition;
                bc.FirstExpression = OptimizeExpression(bc.FirstExpression);
                bc.SecondExpression = OptimizeExpression((bc.SecondExpression));
                if (bc.FirstExpression.IsSingleConstantFactor && bc.SecondExpression.IsSingleConstantFactor)
                {
                    ConstantFactor first = (ConstantFactor) bc.FirstExpression.ExpressionNodes[0].Term.FirstFactor;
                    ConstantFactor second  = (ConstantFactor) bc.SecondExpression.ExpressionNodes[0].Term.FirstFactor;
                    int c1 = System.Int32.Parse(first.Value);
                    int c2 = System.Int32.Parse(second.Value);
                    switch (bc.Type)
                    {
                        case ConditionType.Equal:
                            return (c1 == c2) ? new TrueCondition() : new FalseCondition();
                        case ConditionType.NotEqual:
                            return (c1 != c2) ? new TrueCondition() : new FalseCondition();
                        case ConditionType.GreaterThan:
                            return (c1 > c2) ? new TrueCondition() : new FalseCondition();
                        case ConditionType.LessThan:
                            return (c1 < c2) ? new TrueCondition() : new FalseCondition();
                        case ConditionType.GreaterThanOrEqual:
                            return (c1 >= c2) ? new TrueCondition() : new FalseCondition();
                        case ConditionType.LessThanOrEqual:
                            return (c1 <= c2) ? new TrueCondition() : new FalseCondition();
                        default:
                            return bc;
                    }
                }
                return bc;
            }
            return condition;
        }
    }
}