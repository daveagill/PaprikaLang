using System;
using System.Collections.Generic;

namespace PaprikaLang
{
	public class BindSymbolsStage
	{
		private SymbolTable symTab;

		public void Bind(ASTModule module)
		{
			BindScope(module.FunctionDefs, new SymbolTable(null));
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
			BindScope(funcDef.Body, funcDef.Symbol.SymbolTable);
		}

		private void DeclareFunctionDef(ASTFunctionDef funcDef)
		{
			TypeDetail returnType = symTab.ResolveType(funcDef.ReturnType);

			SymbolTable functionScope = new SymbolTable(symTab);
			FunctionSymbol funcSym = new FunctionSymbol(funcDef.Name, functionScope, returnType);
			symTab.Add(funcSym);
			foreach (ASTFunctionDef.ASTParam param in funcDef.Args)
			{
				TypeDetail paramType = symTab.ResolveType(param.Type);
				NamedValueSymbol paramSym = new NamedValueSymbol(param.Name, paramType);
				functionScope.Add(paramSym);
				funcSym.Params.Add(paramSym);
			}

			funcDef.Symbol = funcSym;
		}

		private void Bind(ASTIfStatement ifStatement)
		{
			Bind(ifStatement.ConditionExpr as dynamic);

			SymbolTable ifScope = new SymbolTable(symTab);
			BindScope(ifStatement.IfBody, ifScope);

			if (ifStatement.ElseBody != null)
			{
				SymbolTable elseScope = new SymbolTable(symTab);
				BindScope(ifStatement.ElseBody, elseScope);
			}
		}

		// nothing to do here
		private void Bind(ASTNumeric numeric) { }
		private void Bind(ASTString str) { }

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
	}
}
