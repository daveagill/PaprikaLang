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
			BindScope(module.FunctionDefs, new SymbolTable(symTab));
		}

		private void Bind(ASTNamedValue namedValue)
		{
			namedValue.ReferencedSymbol = symTab.ResolveSymbol(namedValue.Name);
		}

		private void Bind(ASTBinaryOperator binOp)
		{
			Bind(binOp.LHS as dynamic);
			Bind(binOp.RHS as dynamic);
		}

		private void Bind(ASTFunctionCall funcCall)
		{
			funcCall.ReferencedSymbol = symTab.ResolveSymbolAs<FunctionSymbol>(funcCall.Name);
			foreach (var argNode in funcCall.Args)
			{
				Bind(argNode as dynamic);
			}
		}

		private void Bind(ASTFunctionDef funcDef)
		{
			BindScope(funcDef.Body.Body, funcDef.Symbol.SymbolTable);
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

		private void Bind(ASTLetDef letDef)
		{
			// the first assignment body is not allowed to reference the LHS so evaluate it first
			BindScope(letDef.AssignmentBodies.First().Body, new SymbolTable(symTab));

			// now add the LHS definition to the symbol table
			TypeDetail type = ResolveConcreteType(letDef.Type);
			letDef.ReferencedSymbol = new LocalSymbol(letDef.Name, type);
			symTab.Add(letDef.ReferencedSymbol);

			// all subsequent assignment bodies can now access the LHS
			if (letDef.AssignmentBodies.Count > 1)
			{
				foreach (ASTBlock block in letDef.AssignmentBodies.Skip(1))
				{
					BindScope(block.Body, new SymbolTable(symTab));
				}
			}
		}

		private void Bind(ASTIfStatement ifStatement)
		{
			Bind(ifStatement.ConditionExpr as dynamic);

			SymbolTable ifScope = new SymbolTable(symTab);
			BindScope(ifStatement.IfBody.Body, ifScope);

			if (ifStatement.ElseBody != null)
			{
				SymbolTable elseScope = new SymbolTable(symTab);
				BindScope(ifStatement.ElseBody.Body, elseScope);
			}
		}

		private void Bind(ASTList list)
		{
			Bind(list.From as dynamic);
			Bind(list.To as dynamic);

			if (list.Step != null)
			{
				Bind(list.Step as dynamic);
			}
		}

		private void BindScope(IEnumerable<ASTNode> nodes, SymbolTable scopeSymbolTable)
		{
			// enter the new scope
			SymbolTable originalSymbolTable = symTab;
			symTab = scopeSymbolTable;

			// hoist function symbols first
			foreach (var node in nodes)
			{
				if (node is ASTFunctionDef)
				{
					DeclareFunctionDef((ASTFunctionDef)node);
				}
			}

			// bind all nodes
			foreach (var node in nodes)
			{
				Bind(node as dynamic);
			}

			// restore the original symbol table
			symTab = originalSymbolTable;
		}

		// nothing to do here
		private void Bind(ASTNumeric numeric) { }
		private void Bind(ASTString str) { }

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
