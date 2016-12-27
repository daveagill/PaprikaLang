using System;
using System.Collections.Generic;

namespace PaprikaLang
{
	public class ASTStringifier
	{
		public static string Stringify(ASTModule module)
		{
			string s = "";
			foreach (ASTFunctionDef funcDef in module.FunctionDefs)
			{
				s += Stringify(funcDef) + '\n';
			}
			return s;
		}

		private static string Stringify(ASTBlock block)
		{
			string s = "{\n";
			foreach (ASTNode node in block.Body)
			{
				s += Stringify(node as dynamic) + '\n';
			}
			return s + "}";
		}

		private static string Stringify(ASTString str)
		{
			return "'" + str.Value + "'";
		}

		private static string Stringify(ASTNumeric numeric)
		{
			return numeric.Value.ToString();
		}

		private static string Stringify(ASTNamedValue namedValue)
		{
			return namedValue.Name;
		}

		private static string Stringify(ASTBinaryOperator binOp)
		{
			return "(" + Stringify(binOp.LHS as dynamic) + ") " +
						binOp.Op +
							 " (" + Stringify(binOp.RHS as dynamic) + ")";
		}

		private static string Stringify(ASTFunctionCall funcCall)
		{
			return "call " + funcCall.Name + "(" + funcCall.Args.Count + ")";
		}

		private static string Stringify(ASTIfStatement ifStatement)
		{
			string s = "if " + Stringify(ifStatement.ConditionExpr as dynamic) +
				" " + Stringify(ifStatement.IfBody);
			
			if (ifStatement.ElseBody != null)
			{
				s += " else " + Stringify(ifStatement.ElseBody);
			}

			return s;
		}

		private static string Stringify(ASTFunctionDef funcDef)
		{
			string s = "func " + funcDef.Name + "(" + funcDef.Args.Count + ") -> " + Stringify(funcDef.ReturnType);
			return s + " " + Stringify(funcDef.Body);
		}

		private static string Stringify(ASTLetDef letDef)
		{
			return "let " + letDef.Name + " " + Stringify(letDef.Type) + " = " + Stringify(letDef.AssignmentBody);
		}

		private static string Stringify(ASTList list)
		{
			string s = Stringify(list.From as dynamic) + " to " + Stringify(list.To as dynamic);
			if (list.Step != null)
			{
				s += " step " + Stringify(list.Step as dynamic);
			}
			return "[" + s + "]";
		}

		private static string Stringify(ASTTypeNameParts type)
		{
			string s = type.Name;
			if (type.GenericArgs.Count > 0)
			{
				s += "<";
				foreach (ASTTypeNameParts genericArg in type.GenericArgs)
				{
					s += Stringify(genericArg);
				}
				s += ">";
			}
			return s;
		}

		private static string Stringify(ASTNode untyped)
		{
			throw new Exception("Unhandled ASTNode: " + untyped.GetType());
		}
	}
}
