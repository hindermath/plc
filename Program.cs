using System;
using System.IO;
using System.Collections.Generic;

namespace KNR
{
    class Program
    {
        static void Main(string[] args)
        {
            IEnumerable<Token> tokens = new List<Token>();
            string filename;
            if (args.Length > 0)
            {
                filename = args[0];
            }
            else
            {
                filename = @"test.pl";
            }
            using (var fileStream = new FileStream(filename, FileMode.Open)) {
                ParsedProgram program = new Parser().Parse(new Scanner().Scan(fileStream));
                program = new Optimizer().Optimize(program);

                CIL_Generator generator = new(program);
                foreach (string s in generator.Generate()) Console.WriteLine(s);
            }
        }
        
        static IEnumerable<string> LinesFromFile(string filename) {
            var fileStream = new FileStream(filename, FileMode.Open);
            var reader = new StreamReader(fileStream);
            string line;
            while ((line = reader.ReadLine()) != null) {
                yield return line;
            }
        }

        static IEnumerable<char> CharactersFromFile(string filename) {
            var fileStream = new FileStream(filename, FileMode.Open);
            var reader = new StreamReader(fileStream);
            int c;
            while ((c = reader.Read()) != -1) {
                yield return (char) c;
            }
        }
    }
}