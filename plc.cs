using System;
using System.IO;
using System.Collections.Generic;

namespace PLC
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: plc inputfile outputfile");
                return;
            }
            IEnumerable<Token> tokens = new List<Token>();
            string inFilename = args[0];

            string outFilename;
            if (args.Length > 1)
            {
                outFilename = args[1];
                if (Path.GetFileName(outFilename) != outFilename)
                {
                    throw new Exception("can only output into current directory!");
                }
            }
            else
            {
                outFilename = Path.GetFileNameWithoutExtension(inFilename) + ".exe";
            }
            string extension = Path.GetExtension(outFilename);
            using (var fileStream = new FileStream(inFilename, FileMode.Open)) {
                ParsedProgram program = new Parser().Parse(new Scanner().Scan(fileStream));
                program = new Optimizer().Optimize(program);
                IGenerator generator;
                switch (extension)
                {
                    case ".p":
                        generator = new PL0Generator(program);
                        break;
                    case ".bas":
                        generator = new BasicGenerator(program);
                        break;
                    case ".il":
                    case ".cil":
                    case ".msil":
                        generator = new CIL_Generator(program);
                        break;
                    case ".c":
                        generator = new CGenerator(program);
                        break;
                    case ".cs":
                        generator = new CSharpGenerator(program);
                        break;
                    default:
                        generator = new CLRGenerator(program);
                        break;
                }
                foreach (string s in generator.Generate()) Console.WriteLine(s);
                generator.Compile(outFilename);
                if (!(generator is CLRGenerator))
                {
                    outFilename = Path.GetFileNameWithoutExtension(inFilename) + ".exe";
                    generator = new CLRGenerator(program);
                    generator.Compile(outFilename);
                }
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