using System;
using System.Reflection;
using System.Reflection.Emit;

namespace PaprikaLang
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			//Compile("helloworld");

			ASTModule module = Parser.Parse(
				"func test(one, two) {\n" +
					"one + two * 3" +
				"}" +
				"func Main() { test(2, 4) / 2.4 }").RootNode;


			SemanticAnalyser.Analyse(module);
			Emitter.Emit("MyAwesomeAssembly", "MyAwesomeAssembly.exe", module);

			string asString = new ASTStringifier().Stringify(module);
			Console.WriteLine(asString);
			Console.WriteLine("\n\nDone!");
		}


		public static void Compile(string assemblyName)
		{
			string filename = assemblyName + ".exe";

			AssemblyBuilder asm = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName),
														AssemblyBuilderAccess.Save);

			ModuleBuilder mod = asm.DefineDynamicModule(assemblyName, filename);

			var mainClassTypeName = assemblyName + ".Program";
			var type = mod.DefineType(mainClassTypeName, TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Public);

			var mainMethod = type.DefineMethod("Execute", MethodAttributes.Public | MethodAttributes.Static);
			var ilGen = mainMethod.GetILGenerator();
			ilGen.EmitWriteLine("Hello There!");
			ilGen.Emit(OpCodes.Ret);

			type.CreateType();
			asm.SetEntryPoint(mainMethod);
			asm.Save(filename);
		}


	}
}
