using System;
namespace PaprikaLang
{
	public class ASTStringifier
	{
		public string Stringify(ASTModule module)
		{
			string s = "";
			foreach (ASTFunctionDef funcDef in module.FunctionDefs)
			{
				s += Stringify(funcDef) + '\n';
			}
			return s;
		}

		private string Stringify(ASTString str)
		{
			return "'" + str.Value + "'";
		}

		private string Stringify(ASTNumeric numeric)
		{
			return numeric.Value.ToString();
		}

		private string Stringify(ASTNamedValue namedValue)
		{
			return namedValue.Name;
		}

		private string Stringify(ASTBinaryOperator binOp)
		{
			return "(" + Stringify(binOp.LHS as dynamic) + ") " +
						binOp.Op +
							 " (" + Stringify(binOp.RHS as dynamic) + ")";
		}

		private string Stringify(ASTFunctionCall funcCall)
		{
			return "call " + funcCall.Name + "(" + funcCall.Args.Count + ")";
		}

		private string Stringify(ASTFunctionDef funcDef)
		{
			string s = "func " + funcDef.Name + "(" + funcDef.Args.Count + ") {\n";
			foreach (ASTNode node in funcDef.Body)
			{
				s += Stringify(node as dynamic) + '\n';
			}
			return s + "}";
		}
	}
}
