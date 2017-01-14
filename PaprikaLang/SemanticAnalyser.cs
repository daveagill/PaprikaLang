using System;
namespace PaprikaLang
{
	public class SemanticAnalyser
	{
		public static void Analyse(ASTModule module)
		{
			new BindSymbolsStage(MakeDefaultScope()).Bind(module);
		}

		private static SymbolTable MakeDefaultScope()
		{
			SymbolTable defaultScope = new SymbolTable(null);

			defaultScope.AddType(TypeDetail.Number);
			defaultScope.AddType(TypeDetail.Boolean);
			defaultScope.AddType(TypeDetail.String);
			defaultScope.AddType(TypeDetail.UnboundList);

			return defaultScope;
		}
	}
}
