using System.Collections.Generic;

namespace PLC
{
    public interface IGenerator
    {
        public ParsedProgram Program { get; }
        public IEnumerable<string> Generate();
        public int Compile(string filename);
    }
}