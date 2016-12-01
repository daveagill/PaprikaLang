using System;
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
			TypeDetail blockReturnType = null;
			foreach (var node in funcDef.Body)
			{
				blockReturnType = TypeCheck(node as dynamic);
			}

			if (blockReturnType == null)
			{
				throw new Exception("Unable to determine return type of function definition");
			}

			if (blockReturnType.TypePrimitive != TypePrimitive.Number)
			{
				throw new Exception("Function return types must be numbers (for now)");
			}

			return new TypeDetail(TypePrimitive.Func); // TODO do function types properly
		}
	}
}
