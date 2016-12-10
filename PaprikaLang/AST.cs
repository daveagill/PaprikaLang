using System;
using System.Collections.Generic;

namespace PaprikaLang
{
	public interface ASTNode
	{
	}

	public class ASTModule : ASTNode
	{
		public IList<ASTFunctionDef> FunctionDefs;

		public ASTModule(IList<ASTFunctionDef> functionDefs)
		{
			this.FunctionDefs = functionDefs;
		}
	}

	public class ASTNumeric : ASTNode
	{
		public double Value { get; }

		public ASTNumeric(double value)
		{
			this.Value = value;
		}

		public override string ToString()
		{
			return Value.ToString();
		}
	}

	public class ASTString : ASTNode
	{
		public string Value { get; }

		public ASTString(string value)
		{
			this.Value = value;
		}
	}

	public class ASTNamedValue : ASTNode
	{
		public string Name { get; }

		public ISymbol ReferencedSymbol { get; set; }

		public ASTNamedValue(string name)
		{
			this.Name = name;
		}
	}

	public class ASTBinaryOperator : ASTNode
	{
		public BinaryOps Op { get; }
		public ASTNode LHS { get; }
		public ASTNode RHS { get; }

		public TypeDetail LHSType { get; set; }
		public TypeDetail RHSType { get; set; }
		public TypeDetail ResultType { get; set; }

		public ASTBinaryOperator(BinaryOps op, ASTNode LHS, ASTNode RHS)
		{
			this.Op = op;
			this.LHS = LHS;
			this.RHS = RHS;
		}
	}

	public class ASTFunctionDef : ASTNode
	{
		public class ASTParam
		{
			public string Name { get; }
			public string Type { get; }

			public ASTParam(string name, string type)
			{
				this.Name = name;
				this.Type = type;
			}
		}

		public string Name { get; }
		public IList<ASTParam> Args { get; }
		public string ReturnType { get; }
		public IList<ASTNode> Body { get; }

		public FunctionSymbol Symbol { get; set; }

		public ASTFunctionDef(string name, IList<ASTParam> args, IList<ASTNode> body, string returnType)
		{
			this.Name = name;
			this.Args = args;
			this.Body = body;
			this.ReturnType = returnType;
		}
	}

	public class ASTFunctionCall : ASTNode
	{
		public string Name { get; }
		public IList<ASTNode> Args { get; }

		public FunctionSymbol ReferencedSymbol { get; set; }

		public ASTFunctionCall(string name, IList<ASTNode> args)
		{
			this.Name = name;
			this.Args = args;
		}
	}

	public class ASTIfStatement : ASTNode
	{
		public ASTNode ConditionExpr;
		public IList<ASTNode> IfBody { get; }
		public IList<ASTNode> ElseBody { get; }

		public ASTIfStatement(ASTNode conditionExpr, IList<ASTNode> ifBody, IList<ASTNode> elseBody)
		{
			this.ConditionExpr = conditionExpr;
			this.IfBody = ifBody;
			this.ElseBody = elseBody;
		}
	}
}
