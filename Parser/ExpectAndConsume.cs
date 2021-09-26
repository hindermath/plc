#nullable enable
using System;

namespace KNR
{
    public partial class Parser
    {

        bool MoveNext()
        {
            current = next;
            if (enumerator != null)
            {
                next = (enumerator.MoveNext())
                    ? enumerator.Current
                    : new Token {Text = String.Empty, Type = TokenType.EndProgram};
            }

            if (next.Type == TokenType.Comment)
            {
                return MoveNext();
            }
            return (current.Type != TokenType.EndProgram);
        }

        bool ExpectAndConsume(string s)
        {
            if (current.Text == s)
            {
                //Console.WriteLine("Just matched to " + s + " ( next is " + next.Text + " )");
                MoveNext();
                return true;
            }

            throw new Exception("Expected: " + s + " but got " + current.Text + " at line " + current.LineNumber);
        }

        string ParseIdentifier()
        {
            if (current.Type == TokenType.Identifier)
            {
                string s = current.Text;
                MoveNext();
                return NameWithoutCollisions(s);
            }
            throw new Exception("Expected an identifier but got [" + current.Type + ":" + current.Text + "] at line " + current.LineNumber);
        }
        string ParseConstant()
        {
            if (current.Type == TokenType.IntegerConstant)
            {
                
                string s = current.Text;
                try
                {
                    long num = Int32.Parse(s);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message + " at line " + current.LineNumber);
                }
                
                MoveNext();
                return s;
            }

            throw new Exception("Expected a constant but got " + current.Type + " at line " + current.LineNumber);
        }

        bool ExpectAndConsumeTerminator()
        {
            if (current.Text == ";")
            {
                MoveNext();
                return true;
            }

            throw new Exception("Expected terminator symbol but got " + current.Text+ " at line " + current.LineNumber);
        }

        bool ExpectAndConsume(TokenType t)
        {
            if (current.Type == t)
            {
                MoveNext();
                return true;
            }

            throw new Exception("Expected: TokenType" + current.Type + " at line " + current.LineNumber);
        }
    }
}