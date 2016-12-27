using System;
using System.Collections.Generic;

namespace PaprikaLang
{
	public interface ASTNode
	{
	}

	public interface ASTDeclaration : ASTNode
	{
	}

	public interface ASTExpression : ASTNode
	{
	}

	public class ASTModule : ASTDeclaration
	{
		public IList<ASTFunctionDef> FunctionDefs;

		public ASTModule(IList<ASTFunctionDef> functionDefs)
		{
			this.FunctionDefs = functionDefs;
		}
	}

	public class ASTNumeric : ASTExpression
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

	public class ASTString : ASTExpression
	{
		public string Value { get; }

		public ASTString(string value)
		{
			this.Value = value;
		}
	}

	public class ASTList : ASTExpression
	{
		public ASTExpression From { get; }
		public ASTExpression To { get; }
		public ASTExpression Step { get; }

		public ASTList(ASTExpression from, ASTExpression to, ASTExpression step)
		{
			this.From = from;
			this.To = to;
			this.Step = step;
		}
	}

	public class ASTNamedValue : ASTExpression
	{
		public string Name { get; }

		public ISymbol ReferencedSymbol { get; set; }

		public ASTNamedValue(string name)
		{
			this.Name = name;
		}
	}

	public class ASTBinaryOperator : ASTExpression
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

	public class ASTTypeNameParts
	{
		public string Name { get; }
		public IList<ASTTypeNameParts> GenericArgs { get; }

		public ASTTypeNameParts(string name, IList<ASTTypeNameParts> genericArgs)
		{
			this.Name = name;
			this.GenericArgs = genericArgs;
		}
	}

	public class ASTFunctionDef : ASTDeclaration
	{
		public class ASTParam
		{
			public string Name { get; }
			public ASTTypeNameParts Type { get; }

			public ASTParam(string name, ASTTypeNameParts type)
			{
				this.Name = name;
				this.Type = type;
			}
		}

		public string Name { get; }
		public IList<ASTParam> Args { get; }
		public ASTTypeNameParts ReturnType { get; }
		public IList<ASTNode> Body { get; }

		public FunctionSymbol Symbol { get; set; }

		public ASTFunctionDef(string name, IList<ASTParam> args, IList<ASTNode> body, ASTTypeNameParts returnType)
		{
			this.Name = name;
			this.Args = args;
			this.Body = body;
			this.ReturnType = returnType;
		}
	}

	public class ASTLetDef : ASTDeclaration
	{
		public string Name { get; }
		public ASTTypeNameParts Type { get; }
		public IList<ASTNode> AssignmentBody { get; }

		public LocalSymbol ReferencedSymbol { get; set; }

		public ASTLetDef(string name, ASTTypeNameParts type, IList<ASTNode> assignmentBody)
		{
			this.Name = name;
			this.Type = type;
			this.AssignmentBody = assignmentBody;
		}
	}

	public class ASTFunctionCall : ASTExpression
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

	public class ASTIfStatement : ASTExpression
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
