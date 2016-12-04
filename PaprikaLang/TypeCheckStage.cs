using System;
using System.Collections.Generic;

namespace PaprikaLang
{
	public class TypeCheckStage
	{
		public void TypeCheck(ASTModule module)
		{
			foreach (var funcDef in module.FunctionDefs)
			{
				TypeCheck(funcDef);
			}
		}

		private TypeDetail TypeCheck(ASTString str)
		{
			return new TypeDetail(TypePrimitive.Number);
		}

		private TypeDetail TypeCheck(ASTNumeric numeric)
		{
			return new TypeDetail(TypePrimitive.Number);
		}

		private TypeDetail TypeCheck(ASTNamedValue namedValue)
		{
			return namedValue.ReferencedSymbol.Type;
		}

		private TypeDetail TypeCheck(ASTBinaryOperator binOp)
		{
			TypeDetail LHSType = TypeCheck(binOp.LHS as dynamic);
			TypeDetail RHSType = TypeCheck(binOp.RHS as dynamic);

			if (!LHSType.Equals(RHSType))
			{
				throw new Exception("LHS type of " + LHSType + " does match RHS type of " + RHSType);
			}

			return LHSType;
		}

		private TypeDetail TypeCheck(ASTFunctionCall funcCall)
		{
			foreach (var argNode in funcCall.Args)
			{
				TypeCheck(argNode as dynamic);
			}
			return funcCall.ReferencedSymbol.ReturnType;
		}

		private TypeDetail TypeCheck(ASTFunctionDef funcDef)
		{
			TypeDetail blockReturnType = TypeCheckBlock(funcDef.Body);

			if (blockReturnType != funcDef.Symbol.ReturnType)
			{
				throw new Exception(
					"Function '" + funcDef.Name + "' has a defined return type of " +
					funcDef.Symbol.ReturnType + " but is attempted to return a " + blockReturnType);
			}

			return new TypeDetail(TypePrimitive.Func); // TODO do function types properly
		}

		private TypeDetail TypeCheck(ASTIfStatement ifStatement)
		{
			TypeDetail ifBlockType = TypeCheckBlock(ifStatement.IfBody);

			if (ifStatement.ElseBody != null)
			{
				TypeDetail elseBlockType = TypeCheckBlock(ifStatement.ElseBody);

				if (ifBlockType != elseBlockType)
				{
					throw new Exception("If block type of " + ifBlockType + " and Else block type of " + elseBlockType + " do not match");
				}
			}

			return ifBlockType;
		}

		private TypeDetail TypeCheckBlock(IList<ASTNode> block)
		{
			TypeDetail blockReturnType = null;
			foreach (var node in block)
			{
				blockReturnType = TypeCheck(node as dynamic);
			}
			return blockReturnType;
		}
	}
}
