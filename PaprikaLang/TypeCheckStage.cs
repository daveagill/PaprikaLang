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
			return new TypeDetail(TypePrimitive.String);
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

			// decorate the binOp node with type information
			binOp.LHSType = LHSType;
			binOp.RHSType = RHSType;

			switch (binOp.Op)
			{
				case BinaryOps.Add:
				case BinaryOps.Subtract:
				case BinaryOps.Multiply:
				case BinaryOps.Divide:
				case BinaryOps.Modulus:
					if (LHSType.TypePrimitive != TypePrimitive.Number)
					{
						throw new Exception("Expected LHS type of " + TypePrimitive.Number + " but got " + LHSType + ", for operator " + binOp.Op);
					}
					if (RHSType.TypePrimitive != TypePrimitive.Number)
					{
						throw new Exception("Expected RHS type of " + TypePrimitive.Number + " but got " + RHSType + ", for operator " + binOp.Op);
					}
					binOp.ResultType = new TypeDetail(TypePrimitive.Number);
					break;

				case BinaryOps.GreaterThan:
				case BinaryOps.LessThan:
					if (LHSType.TypePrimitive != TypePrimitive.Number)
					{
						throw new Exception("Expected LHS type of " + TypePrimitive.Number + " but got " + LHSType + ", for operator " + binOp.Op);
					}
					if (RHSType.TypePrimitive != TypePrimitive.Number)
					{
						throw new Exception("Expected RHS type of " + TypePrimitive.Number + " but got " + RHSType + ", for operator " + binOp.Op);
					}
					binOp.ResultType = new TypeDetail(TypePrimitive.Boolean);
					break;

				case BinaryOps.Equals:
				case BinaryOps.NotEquals:
					if (LHSType != RHSType)
					{
						throw new Exception("LHS type of " + LHSType + " does not match RHS type of " + RHSType + ", for operator " + binOp.Op);
					}
					binOp.ResultType = new TypeDetail(TypePrimitive.Boolean);
					break;

				case BinaryOps.StringConcat:
					if (!(LHSType.TypePrimitive == TypePrimitive.Number ||
						  LHSType.TypePrimitive == TypePrimitive.String) ||
						!(RHSType.TypePrimitive == TypePrimitive.Number ||
					      RHSType.TypePrimitive == TypePrimitive.String))
					{
						throw new Exception("String concatenation only works on " + TypePrimitive.Number + " or " + TypePrimitive.String);
					}
					binOp.ResultType = new TypeDetail(TypePrimitive.String);
					break;

				case BinaryOps.And:
				case BinaryOps.Or:
					if (LHSType.TypePrimitive != TypePrimitive.Boolean ||
						RHSType.TypePrimitive != TypePrimitive.Boolean)
					{
						throw new Exception("Logic operators only operate on booleans");
					}
					binOp.ResultType = LHSType;
					break;
					
				default:
					throw new Exception("Unhandled Operator: " + binOp.Op);
			}

			return binOp.ResultType;
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
			TypeDetail conditionType = TypeCheck(ifStatement.ConditionExpr as dynamic);
			if (conditionType.TypePrimitive != TypePrimitive.Boolean)
			{
				throw new Exception("If-condition was of type " + conditionType + " but must be " + TypePrimitive.Boolean);
			}

			TypeDetail ifBlockType = TypeCheckBlock(ifStatement.IfBody);

			if (ifStatement.ElseBody != null)
			{
				TypeDetail elseBlockType = TypeCheckBlock(ifStatement.ElseBody);

				if (ifBlockType != elseBlockType)
				{
					throw new Exception("If-block type of " + ifBlockType + " and Else block type of " + elseBlockType + " do not match");
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
