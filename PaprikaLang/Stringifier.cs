﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace PaprikaLang
{
	public class ASTStringifier
	{
		public static string Stringify(ASTModule module)
		{
			string s = "";
			foreach (ASTNode node in module.Body.Body)
			{
				if (s.Length > 0)
				{
					s += "\n\n";
				}
				s += Stringify(node as dynamic);
			}
			return s;
		}

		private static string Stringify(ASTBlock block)
		{
			string s = "";
			foreach (ASTNode node in block.Body)
			{
				if (s.Length > 0)
				{
					s += '\n';
				}
				s += Stringify(node as dynamic);
			}

			return block.Body.Count <= 1 ? "{ " + s + " }" : "{\n" + s + "\n}";
		}

		private static string Stringify(ASTString str)
		{
			return "\"" + str.Value + "\"";
		}

		private static string Stringify(ASTNumeric numeric)
		{
			return numeric.Value.ToString();
		}

		private static string Stringify(ASTBoolean booleanLiteral)
		{
			return booleanLiteral.Value.ToString();
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
			string s = "";
			foreach (var arg in funcCall.Args)
			{
				if (s.Length > 0)
				{
					s += ", ";
				}
				s += Stringify(arg as dynamic);
			}
			return "call " + funcCall.Name + "(" + s + ")";
		}

		private static string Stringify(ASTIfStatement ifStatement)
		{
			string s = "if " + Stringify(ifStatement.ConditionExpr as dynamic) +
				" then " + Stringify(ifStatement.IfBody as dynamic);
			
			if (ifStatement.ElseBody != null)
			{
				s += " else " + Stringify(ifStatement.ElseBody as dynamic);
			}

			return s;
		}

		private static string Stringify(ASTFunctionDef funcDef)
		{
			string s = "func " + funcDef.Name + "(" + funcDef.Args.Count + " args) -> " + Stringify(funcDef.ReturnType);
			return s + " " + Stringify(funcDef.Body);
		}

		private static string Stringify(ASTLetDef letDef)
		{
			string s = "let " + letDef.Name + " " + Stringify(letDef.Type) + " = " + Stringify(letDef.AssignmentBodies.First() as dynamic);
			foreach (ASTExpression assignmentBody in letDef.AssignmentBodies.Skip(1))
			{
				s += " where " + Stringify(assignmentBody as dynamic);
			}
			return s;
		}

		private static string Stringify(ASTForeachAssignment foreachAssignment)
		{
			return "foreach " +
				foreachAssignment.ElementName + " " + Stringify(foreachAssignment.ElementType) +
				" in " + Stringify(foreachAssignment.Range as dynamic) +
				" do " + Stringify(foreachAssignment.Body as dynamic);
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

		private static string Stringify(ASTTypeDef typeDef)
		{
			string s = "";
			foreach (ASTTypeDef.ASTField field in typeDef.Fields)
			{
				if (s.Length > 0)
				{
					s += '\n';
				}
				s += field.Name + " " + Stringify(field.Type);
			}

			return "type " + typeDef.Name + (typeDef.Fields.Count <= 1 ? " { " + s + " }" : " {\n" + s + "\n}");
		}

		private static string Stringify(ASTMemberAccess memberAccess)
		{
			return Stringify(memberAccess.LHS as dynamic) + "." + memberAccess.DataMember;
		}

		private static string Stringify(ASTTypeConstruction typeConstruction)
		{
			string s = "";
			foreach (var arg in typeConstruction.Args)
			{
				if (s.Length > 0)
				{
					s += ", ";
				}
				s += Stringify(arg as dynamic);
			}
			return "construct " + typeConstruction.Name + '{' + s + '}';
		}

		private static string Stringify(ASTNode untyped)
		{
			throw new Exception("Unhandled ASTNode: " + untyped.GetType());
		}
	}
}
