#nullable enable
using System.Collections.Generic;

namespace PLC
{
    public partial class Parser
    {
        Block ParseBlock()
        {
            // Parse constants
            Block block = new();
            if (current.Text == "CONST")
            {
                ExpectAndConsume("CONST");
                Identity first = new();
                first.Name = ParseIdentifier();
                symbols.Add(first.Name);
                ExpectAndConsume("=");
                first.Value = ParseConstant();
                block.Constants.Add(first);
                while (current.Text == ",")
                {
                    ExpectAndConsume(",");
                    Identity other = new();
                    other.Name = ParseIdentifier();
                    ExpectAndConsume("=");
                    other.Value = ParseConstant();
                    block.Constants.Add(other);
                }
                ExpectAndConsumeTerminator();
            }
            // Parse variables
            if (current.Text == "VAR")
            {
                ExpectAndConsume(("VAR"));
                Identity variable = new Identity() {Name = ParseIdentifier()};
                block.Variables.Add(variable);
                symbols.Add(variable.Name);
                while (current.Text == ",")
                {
                    ExpectAndConsume(",");
                    variable = new Identity() {Name = ParseIdentifier()};
                    block.Variables.Add(variable);
                    symbols.Add(variable.Name);
                }

                ExpectAndConsumeTerminator();
            }
            // Parse procedures
            List<string> parentSymbols = symbols;
            if (current.Text == "PROCEDURE")
            {
                symbols = new List<string>();
                foreach (string s in parentSymbols)
                {
                    symbols.Add(s);
                }
                while (current.Text == "PROCEDURE")
                {
                    Procedure proc = new();
                    ExpectAndConsume("PROCEDURE");
                    proc.Name = ParseIdentifier();
                    ExpectAndConsumeTerminator();
                    proc.Block = ParseBlock();
                    ExpectAndConsumeTerminator();
                    block.Procedures.Add(proc);               
                }
            }
            // Parse "statement" ( the main body of the block )
            block.Statement = ParseStatement();
            symbols = parentSymbols;
            
            return block;
        }
    }
}