using System;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;

namespace PaprikaLang
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			string sourceFilePath = "program.paprika";
			if (args.Length > 0)
			{
				sourceFilePath = args[0];
			}

			string programName = Path.GetFileNameWithoutExtension(sourceFilePath);

			string sourceCode = File.ReadAllText(sourceFilePath);
			ASTModule module = Parser.Parse(sourceCode).RootNode;

			SemanticAnalyser.Analyse(module);
			Emitter.Emit(programName, programName + ".exe", module);

			string asString = new ASTStringifier().Stringify(module);
			Console.WriteLine(asString);
			Console.WriteLine("\n\nDone!");
		}
	}
}
