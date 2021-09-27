#nullable enable
using System.Collections.Generic;

namespace PLC
{
    public partial class Parser
    {
        // "="|"#"|"<"|"<="|">"|">="
        private readonly Dictionary<string, ConditionType> _conditionDict = new()
        {
            {"=", ConditionType.Equal},
            {"#", ConditionType.NotEqual},
            {"<", ConditionType.LessThan},
            {"<=", ConditionType.LessThanOrEqual},
            {">", ConditionType.GreaterThan},
            {">=", ConditionType.GreaterThanOrEqual}
        };
        private Condition ParseCondition()
        {
            if (current.Text == "ODD")
            {
                ExpectAndConsume("ODD");
                OddCondition oc = new();
                oc.Expression = ParseExpression();
                return oc;
            }

            BinaryCondition bc = new();
            bc.FirstExpression = ParseExpression();
            bc.Type = _conditionDict[current.Text];
            MoveNext();
            bc.SecondExpression = ParseExpression();
            return bc;
        }
    }
}