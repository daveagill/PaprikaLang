using System;
using System.Collections.Generic;
using System.Linq;

namespace PaprikaLang
{
	public class BindSymbolsStage
	{
		private SymbolTable symTab;

		public BindSymbolsStage(SymbolTable defaultScope)
		{
			symTab = defaultScope;
		}

		public void Bind(ASTModule module)
		{
			BindScope(module.Body.Body, new SymbolTable(symTab));
		}

		private TypeDetail Bind(ASTBlock block)
		{
			return BindScope(block.Body, new SymbolTable(symTab));
		}

		private TypeDetail Bind(ASTNumeric numeric)
		{
			return TypeDetail.Number;
		}

		private TypeDetail Bind(ASTString str)
		{
			return TypeDetail.String;
		}

		private TypeDetail Bind(ASTNamedValue namedValue)
		{
			namedValue.ReferencedSymbol = symTab.ResolveSymbol(namedValue.Name);
			return namedValue.ReferencedSymbol.Type;
		}

		private TypeDetail Bind(ASTBinaryOperator binOp)
		{
			TypeDetail LHSType = Bind(binOp.LHS as dynamic);
			TypeDetail RHSType = Bind(binOp.RHS as dynamic);

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

		private TypeDetail Bind(ASTFunctionCall funcCall)
		{
			funcCall.ReferencedSymbol = symTab.ResolveSymbolAs<FunctionSymbol>(funcCall.Name);

			// validate arity
			if (funcCall.ReferencedSymbol.Params.Count != funcCall.Args.Count)
			{
				throw new Exception("Arity mismatch on function call to " + funcCall.Name +
									". Expected " + funcCall.ReferencedSymbol.Params.Count + " arguments " +
									"but actually received " + funcCall.Args.Count);
			}

			for (int i = 0; i < funcCall.Args.Count; ++i)
			{
				TypeDetail inGoingType = Bind(funcCall.Args[i] as dynamic);
				TypeDetail expectedType = funcCall.ReferencedSymbol.Params[i].Type;
				if (inGoingType != expectedType)
				{
					throw new Exception("Type mismatch on function call to " + funcCall.Name + " at " +
					                    "parameter #" + (i+1) + ". Expected type " + expectedType + " but actually " +
					                    "received " + inGoingType);
				}
			}

			return funcCall.ReferencedSymbol.ReturnType;
		}

		private TypeDetail Bind(ASTFunctionDef funcDef)
		{
			TypeDetail bodyReturnType = BindScope(funcDef.Body.Body, funcDef.Symbol.SymbolTable);

			if (bodyReturnType != funcDef.Symbol.ReturnType)
			{
				throw new Exception(
					"Function '" + funcDef.Name + "' has a defined return type of " +
					funcDef.Symbol.ReturnType + " but is attempting to return a " + bodyReturnType);
			}

			return null; // function definitions have no type
		}

		private void DeclareFunctionDef(ASTFunctionDef funcDef)
		{
			TypeDetail returnType = ResolveConcreteType(funcDef.ReturnType);

			SymbolTable functionScope = new SymbolTable(symTab);
			FunctionSymbol funcSym = new FunctionSymbol(funcDef.Name, functionScope, returnType);
			symTab.Add(funcSym);
			foreach (ASTFunctionDef.ASTParam param in funcDef.Args)
			{
				TypeDetail paramType = ResolveConcreteType(param.Type);
				ParamSymbol paramSym = new ParamSymbol(param.Name, paramType);
				functionScope.Add(paramSym);
				funcSym.Params.Add(paramSym);
			}

			funcDef.Symbol = funcSym;
		}

		private TypeDetail Bind(ASTLetDef letDef)
		{
			// the first assignment body is not allowed to reference the LHS so evaluate it first
			TypeDetail firstRHSType = Bind(letDef.AssignmentBodies.First() as dynamic);

			// now add the LHS definition to the symbol table
			TypeDetail type = ResolveConcreteType(letDef.Type);
			letDef.Symbol = new LocalSymbol(letDef.Name, type);
			symTab.Add(letDef.Symbol);

			string typeErrorMsg = "Let definition of " + letDef.Name + " is defined with a type of " +
									letDef.Symbol.Type + " but is attempting to assign a type of ";

			// verify the first assigned type is compatible
			if (letDef.Symbol.Type != firstRHSType)
			{
				throw new Exception(typeErrorMsg + firstRHSType);
			}

			// all subsequent assignment bodies can now access the LHS
			if (letDef.AssignmentBodies.Count > 1)
			{
				foreach (ASTExpression assignmentBody in letDef.AssignmentBodies.Skip(1))
				{
					TypeDetail RHSType = Bind(assignmentBody as dynamic);

					// verify the chained types are compatible
					if (letDef.Symbol.Type != RHSType)
					{
						throw new Exception(typeErrorMsg + RHSType);
					}
				}
			}

			return null; // let definitions have no type
		}

		private TypeDetail Bind(ASTIfStatement ifStatement)
		{
			TypeDetail conditionType = Bind(ifStatement.ConditionExpr as dynamic);
			if (conditionType != TypeDetail.Boolean)
			{
				throw new Exception("If-condition was of type " + conditionType + " but must be " + TypePrimitive.Boolean);
			}

			SymbolTable ifScope = new SymbolTable(symTab);
			TypeDetail ifBodyType = BindScope(ifStatement.IfBody.Body, ifScope);

			if (ifStatement.ElseBody != null)
			{
				SymbolTable elseScope = new SymbolTable(symTab);
				TypeDetail elseBodyType = BindScope(ifStatement.ElseBody.Body, elseScope);

				if (ifBodyType != elseBodyType)
				{
					throw new Exception("If-body type of " + ifBodyType + " and Else-body type of " + elseBodyType + " do not match");
				}
			}

			return ifBodyType;
		}

		private TypeDetail Bind(ASTForeachAssignment foreachAssignment)
		{
			TypeDetail rangeType = Bind(foreachAssignment.Range as dynamic);

			// add the per-element symbol
			TypeDetail elementType = ResolveConcreteType(foreachAssignment.ElementType);
			foreachAssignment.ReferencedSymbol = new LocalSymbol(foreachAssignment.ElementName, elementType);
			symTab.Add(foreachAssignment.ReferencedSymbol);

			if (rangeType.GenericType != TypeDetail.UnboundList)
			{
				throw new Exception("Range type of foreach must be a list type, not: " + rangeType);
			}
			if (elementType != rangeType.GenericParams.First())
			{
				throw new Exception("Element type of foreach " + elementType + " must match the range type of " + rangeType);
			}

			return Bind(foreachAssignment.Body);
		}

		private TypeDetail Bind(ASTList list)
		{
			TypeDetail fromType = Bind(list.From as dynamic);
			TypeDetail toType = Bind(list.To as dynamic);
			TypeDetail stepType = list.Step == null ? null : Bind(list.Step as dynamic);

			if (fromType != TypeDetail.Number ||
				toType != TypeDetail.Number ||
				stepType != null && stepType != TypeDetail.Number)
			{
				throw new Exception("List literals must be comprised of " + TypePrimitive.Number +
								   "\n[from: " + fromType + " to: " + toType + " step: " + stepType + "]");
			}

			return TypeDetail.ListOfNumbers;
		}

		private void DeclareTypeDef(ASTTypeDef typeDef)
		{
			SymbolTable typeScope = new SymbolTable(null);
			TypeDetail representingType = new TypeDetail(typeDef.Name, TypePrimitive.Structure);
			typeDef.Symbol = new TypeSymbol(representingType, typeScope);

			foreach (ASTTypeDef.ASTField field in typeDef.Fields)
			{
				TypeDetail fieldType = ResolveConcreteType(field.Type);
				FieldSymbol fieldSym = new FieldSymbol(field.Name, fieldType);
				typeDef.Symbol.Fields.Add(fieldSym);
				typeScope.Add(fieldSym);
			}

			symTab.Add(typeDef.Symbol);
			symTab.AddType(representingType);
		}

		private TypeDetail BindScope(IEnumerable<ASTNode> nodes, SymbolTable scopeSymbolTable)
		{
			// enter the new scope
			SymbolTable originalSymbolTable = symTab;
			symTab = scopeSymbolTable;

			// hoist type definitions first
			foreach (var node in nodes)
			{
				if (node is ASTTypeDef)
				{
					DeclareTypeDef((ASTTypeDef)node);
				}
			}

			// hoist function symbols second
			foreach (var node in nodes)
			{
				if (node is ASTFunctionDef)
				{
					DeclareFunctionDef((ASTFunctionDef)node);
				}
			}

			// bind all nodes in order
			TypeDetail lastReturnType = null;
			foreach (var node in nodes)
			{
				lastReturnType = Bind(node as dynamic);
			}

			// restore the original symbol table
			symTab = originalSymbolTable;

			return lastReturnType;
		}

		private TypeDetail Bind(ASTTypeDef typeDef)
		{
			return null; // type definitions have no type
		}

		private TypeDetail Bind(ASTMemberAccess memberAccess)
		{
			TypeDetail LHSType = Bind(memberAccess.LHS as dynamic);

			// resolve the RHS against the type's local symbol table
			TypeSymbol typeSym = symTab.ResolveSymbolAs<TypeSymbol>(LHSType.SimpleName);
			FieldSymbol fieldSym = typeSym.SymbolTable.ResolveSymbolAs<FieldSymbol>(memberAccess.DataMember);

			memberAccess.FieldSymbol = fieldSym;
			return fieldSym.Type;
		}

		private TypeDetail Bind(ASTTypeConstruction typeConstruction)
		{
			typeConstruction.ReferencedSymbol = symTab.ResolveSymbolAs<TypeSymbol>(typeConstruction.Name);

			// validate arity
			if (typeConstruction.ReferencedSymbol.Fields.Count != typeConstruction.Args.Count)
			{
				throw new Exception("Arity mismatch on type construction for " + typeConstruction.Name +
									". Expected " + typeConstruction.ReferencedSymbol.Fields.Count + " arguments " +
									"but actually received " + typeConstruction.Args.Count);
			}

			for (int i = 0; i < typeConstruction.Args.Count; ++i)
			{
				TypeDetail inGoingType = Bind(typeConstruction.Args[i] as dynamic);
				TypeDetail expectedType = typeConstruction.ReferencedSymbol.Fields[i].Type;
				if (inGoingType != expectedType)
				{
					throw new Exception("Type mismatch on type construction to " + typeConstruction.Name + " at " +
					                    "parameter #" + (i+1) + ". Expected type " + expectedType + " but actually " +
										"received " + inGoingType);
				}
			}

			return typeConstruction.ReferencedSymbol.Type;
		}

		private TypeDetail ResolveConcreteType(ASTTypeNameParts type)
		{
			TypeDetail mainType = symTab.ResolveType(type.Name);

			// unbound means we need to resolve generic args to make it concrete
			if (mainType.IsUnbound)
			{
				if (mainType.UnboundArgCount != type.GenericArgs.Count)
				{
					throw new Exception("Generic argument count mismatch. Type " + mainType +
					                    "has " + mainType.UnboundArgCount + " args, but " +
					                    type.GenericArgs.Count + " were provided");
				}

				IList<TypeDetail> boundArgs = new List<TypeDetail>();
				foreach (ASTTypeNameParts argType in type.GenericArgs)
				{
					boundArgs.Add(ResolveConcreteType(argType));
				}
				return new TypeDetail(mainType, boundArgs);
			}

			// otherwise it is not a generic type and useable as-is
			if (type.GenericArgs.Count > 0)
			{
				throw new Exception(mainType + " is not a generic type");
			}
			return mainType;
		}
	}
}
