#nullable enable

namespace PLC
{
    public partial class Optimizer
    {
        Block OptimzeBlock(Block block)
        {
            block.Statement = OptimizeStatement(block.Statement);
            return block;
        }
    }
}