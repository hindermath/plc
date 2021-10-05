#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;

namespace PLC
{
    public partial class Parser
    {
        Token current, next;
        IEnumerator<Token>? enumerator;
        ParsedProgram Program;
        List<string> symbols;

        string[] keywords = new string[]
            {"CONST", "VAR", "PROCEDURE", "BEGIN", "END", "IF", "THEN", "DO", "WHILE", "CALL", "ODD", "READ", "WRITE", "FOR", "TO", "STEP", "RAND"};

        public Parser()
        {
            Program = new ParsedProgram();
            symbols = new List<string>();
        }

        public ParsedProgram Parse(IEnumerable<Token> tokens)
        {
            enumerator = MarkKeywords(tokens).GetEnumerator();
            MoveNext(); MoveNext(); // Prime the pump
            
            Program.Block = ParseBlock();

            return Program;
        }
        
        IEnumerable<Token> MarkKeywords(IEnumerable<Token> tokens)
        {
            foreach (var token in tokens)
            {
                var newtoken = token;
                if (token.Type == TokenType.Identifier)
                {
                    string upperCaseTokenText = token.Text.ToUpperInvariant();
                    if (keywords.Contains(upperCaseTokenText))
                    {
                        newtoken.Type = TokenType.Keyword;
                        newtoken.Text = upperCaseTokenText;
                    }
                }

                yield return newtoken;
            }
        }
    }
}