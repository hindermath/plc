#nullable enable

namespace PLC
{
    public partial class Parser
    {
        private Block ParseBlock()
        {
            // Parse constants
            Block block = new();
            if (current.Text == "CONST")
            {
                ExpectAndConsume("CONST");
                Identity first = new();
                first.Name = ParseIdentifier();
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
                block.Variables.Add(new Identity() {Name = ParseIdentifier()});
                while (current.Text == ",")
                {
                    ExpectAndConsume(",");
                    block.Variables.Add(new Identity() {Name = ParseIdentifier()});
                }

                ExpectAndConsumeTerminator();
            }

            // Parse procedures
            if (current.Text == "PROCEDURE")
            {
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

            return block;
        }
    }
}