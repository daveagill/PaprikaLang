using System;
using System.Collections.Generic;
using System.Linq;

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

		private TypeDetail TypeCheck(ASTBlock block)
		{
			TypeDetail blockReturnType = null;
			foreach (var node in block.Body)
			{
				blockReturnType = TypeCheck(node as dynamic);
			}
			return blockReturnType;
		}

		private TypeDetail TypeCheck(ASTString str)
		{
			return TypeDetail.String;
		}

		private TypeDetail TypeCheck(ASTNumeric numeric)
		{
			return TypeDetail.Number;
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
					binOp.ResultType = TypeDetail.Number;
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
					binOp.ResultType = TypeDetail.Boolean;
					break;

				case BinaryOps.Equals:
				case BinaryOps.NotEquals:
					if (LHSType != RHSType)
					{
						throw new Exception("LHS type of " + LHSType + " does not match RHS type of " + RHSType + ", for operator " + binOp.Op);
					}
					binOp.ResultType = TypeDetail.Boolean;
					break;

				case BinaryOps.StringConcat:
					if (!(LHSType.TypePrimitive == TypePrimitive.Number ||
						  LHSType.TypePrimitive == TypePrimitive.String) ||
						!(RHSType.TypePrimitive == TypePrimitive.Number ||
					      RHSType.TypePrimitive == TypePrimitive.String))
					{
						throw new Exception("String concatenation only works on " + TypePrimitive.Number + " or " + TypePrimitive.String);
					}
					binOp.ResultType = TypeDetail.String;
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
			TypeDetail blockReturnType = TypeCheck(funcDef.Body);

			if (blockReturnType != funcDef.Symbol.ReturnType)
			{
				throw new Exception(
					"Function '" + funcDef.Name + "' has a defined return type of " +
					funcDef.Symbol.ReturnType + " but is attempting to return a " + blockReturnType);
			}

			return null; // function definitions have no type
		}

		private TypeDetail TypeCheck(ASTLetDef letDef)
		{
			foreach (ASTExpression assignmentBody in letDef.AssignmentBodies)
			{
				TypeDetail RHSType = TypeCheck(assignmentBody as dynamic);

				if (letDef.ReferencedSymbol.Type != RHSType)
				{
					throw new Exception("Let definition of " + letDef.Name + " is defined with a type of " +
										letDef.ReferencedSymbol.Type + " but is attempting to assign a " +
										"type of " + RHSType);
				}
			}

			return null; // let definitions have no type
		}

		private TypeDetail TypeCheck(ASTIfStatement ifStatement)
		{
			TypeDetail conditionType = TypeCheck(ifStatement.ConditionExpr as dynamic);
			if (conditionType.TypePrimitive != TypePrimitive.Boolean)
			{
				throw new Exception("If-condition was of type " + conditionType + " but must be " + TypePrimitive.Boolean);
			}

			TypeDetail ifBlockType = TypeCheck(ifStatement.IfBody);

			if (ifStatement.ElseBody != null)
			{
				TypeDetail elseBlockType = TypeCheck(ifStatement.ElseBody);

				if (ifBlockType != elseBlockType)
				{
					throw new Exception("If-block type of " + ifBlockType + " and Else block type of " + elseBlockType + " do not match");
				}
			}

			return ifBlockType;
		}

		private TypeDetail TypeCheck(ASTForeachAssignment foreachAssignment)
		{
			TypeDetail rangeType = TypeCheck(foreachAssignment.Range as dynamic);

			if (rangeType.GenericType != TypeDetail.UnboundList)
			{
				throw new Exception("Range type of foreach must be a list type, not: " + rangeType);
			}
			if (foreachAssignment.ReferencedSymbol.Type != rangeType.GenericParams.First())
			{
				throw new Exception("Element type of foreach " + foreachAssignment.ReferencedSymbol.Type +
				                   " must match the range type of " + rangeType);
			}

			return TypeCheck(foreachAssignment.Body);
		}

		private TypeDetail TypeCheck(ASTList list)
		{
			TypeDetail fromType = TypeCheck(list.From as dynamic);
			TypeDetail toType = TypeCheck(list.To as dynamic);
			TypeDetail stepType = list.Step == null ? null : TypeCheck(list.Step as dynamic);

			if (fromType.TypePrimitive != TypePrimitive.Number ||
			    toType.TypePrimitive != TypePrimitive.Number ||
			    stepType != null && stepType.TypePrimitive != TypePrimitive.Number)
			{
				throw new Exception("List literals must be comprised of " + TypePrimitive.Number +
				                   "\n[from: " + fromType + " to: " + toType + " step: " + stepType + "]");
			}

			return TypeDetail.ListOfNumbers;
		}
	}
}
