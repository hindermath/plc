using System;

namespace PLC {
    public enum TokenType { Comment, Keyword, Identifier, Parens, Seperator, Operator, IntegerConstant, StringConstant, Terminator, EndProgram };
    public struct Token {
        public string Text;
        public TokenType Type;
        public int LineNumber;
    }
}