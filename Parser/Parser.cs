#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;

namespace KNR
{
    public partial class Parser
    {
        private Token current, next;
        private IEnumerator<Token>? enumerator;
        private ParsedProgram Program;

        private string[] keywords = new string[]
            {"CONST", "VAR", "PROCEDURE", "BEGIN", "END", "IF", "THEN", "DO", "WHILE", "CALL", "ODD", "READ", "WRITE", "FOR", "TO", "STEP", "RAND"};

        public Parser()
        {
            Program = new ParsedProgram();
        }

        public ParsedProgram Parse(IEnumerable<Token> tokens)
        {
            enumerator = MarkKeywords(tokens).GetEnumerator();
            MoveNext(); MoveNext(); // Prime the pump
            
            Program.Block = ParseBlock();

            return Program;
        }
        
        private IEnumerable<Token> MarkKeywords(IEnumerable<Token> tokens)
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
        
        private string NameWithoutCollisions(string identifierName)
        {
            switch (identifierName)
            {
                case "ret":
                    return "__ret";
                case "add":
                    return "__add";
                case "sub":
                    return "__sub";
                case "mul":
                    return "__mul";
                case "div":
                    return "__div";
                case "rem":
                    return "__rem";
                default:
                    return identifierName;
            }
        }
    }
}