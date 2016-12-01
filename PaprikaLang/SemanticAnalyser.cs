using System;
namespace PaprikaLang
{
	public class SemanticAnalyser
	{
		public static void Analyse(ASTModule module)
		{
			new BindSymbolsStage().Bind(module);
			new TypeCheckStage().TypeCheck(module);
		}
	}
}
