#nullable enable
using System;
using System.Collections.Generic;

namespace PLC
{
    public partial class Optimizer
    {
        Term OptimizeTerm(Term term, bool countReferences = true)
        {
            List<TermNode> optimizedNodes = new();
            
            // Fold constants through multiplication
            if (term.TermNodes.Count > 1)
            {

                int multResult = 1;
                int divResult = 1;
                foreach (var n in term.TermNodes)
                {
                    var node = OptimizeTermNode(n, countReferences);
                    if (node.Factor is ConstantFactor)
                    {
                        var cf = (ConstantFactor) node.Factor;
                        if (node.IsDivision)
                        {
                            divResult *= Int32.Parse(cf.Value);
                        }
                        else
                        {
                            multResult *= Int32.Parse(cf.Value);
                        }
                    }
                    else
                    {
                        optimizedNodes.Add(OptimizeTermNode(node, countReferences));
                    }
                }

                // We do not want to multiply by 1
                if (multResult % divResult == 0)
                {
                    int constantResult = multResult / divResult;
                    if (constantResult != 1)
                    {
                        TermNode resultNode = new();
                        resultNode.Factor = new ConstantFactor() {Value = constantResult.ToString()};
                        resultNode.IsDivision = false;
                        optimizedNodes.Add(resultNode);
                    }
                }
                else
                {
                    // We do not want to multiply by 1
                    if (multResult != 1)
                    {
                        TermNode resultNode = new();
                        resultNode.Factor = new ConstantFactor() {Value = multResult.ToString()};
                        resultNode.IsDivision = false;
                        optimizedNodes.Add(resultNode);
                    }

                    // We do not want to divide by 1
                    if (divResult != 1)
                    {
                        TermNode divisorNode = new();
                        divisorNode.Factor = new ConstantFactor() {Value = divResult.ToString()};
                        divisorNode.IsDivision = true;
                        optimizedNodes.Add(divisorNode);
                    }
                }
            }
            else
            {
                optimizedNodes.Add(OptimizeTermNode(term.TermNodes[0], countReferences));
            }

            term.TermNodes = optimizedNodes;
            return term;
        }
        
        TermNode OptimizeTermNode(TermNode node, bool countReferences = true)
        {
            node.Factor = OptimizeFactor(node.Factor, countReferences);
            return node;
        }
    }
}