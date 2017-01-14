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
		public ASTBlock Body { get; }

		public ASTModule(ASTBlock body)
		{
			this.Body = body;
		}
	}

	public class ASTBlock : ASTExpression
	{
		public IList<ASTNode> Body { get; }

		public ASTBlock(IList<ASTNode> body)
		{
			this.Body = body;
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
		public ASTBlock Body { get; }

		public FunctionSymbol Symbol { get; set; }

		public ASTFunctionDef(string name, IList<ASTParam> args, ASTBlock body, ASTTypeNameParts returnType)
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
		public IList<ASTExpression> AssignmentBodies { get; }

		public LocalSymbol Symbol { get; set; }

		public ASTLetDef(string name, ASTTypeNameParts type, IList<ASTExpression> assignmentBodies)
		{
			this.Name = name;
			this.Type = type;
			this.AssignmentBodies = assignmentBodies;
		}
	}

	public class ASTFunctionCall : ASTExpression
	{
		public string Name { get; }
		public IList<ASTExpression> Args { get; }

		public FunctionSymbol ReferencedSymbol { get; set; }

		public ASTFunctionCall(string name, IList<ASTExpression> args)
		{
			this.Name = name;
			this.Args = args;
		}
	}

	public class ASTIfStatement : ASTExpression
	{
		public ASTExpression ConditionExpr;
		public ASTBlock IfBody { get; }
		public ASTBlock ElseBody { get; }

		public ASTIfStatement(ASTExpression conditionExpr, ASTBlock ifBody, ASTBlock elseBody)
		{
			this.ConditionExpr = conditionExpr;
			this.IfBody = ifBody;
			this.ElseBody = elseBody;
		}
	}

	public class ASTForeachAssignment : ASTExpression
	{
		public string ElementName { get; }
		public ASTTypeNameParts ElementType { get; }
		public ASTExpression Range { get; }
		public ASTBlock Body { get; }

		public LocalSymbol ReferencedSymbol { get; set; }

		public ASTForeachAssignment(string elementName, ASTTypeNameParts elementType, ASTExpression range, ASTBlock body)
		{
			this.ElementName = elementName;
			this.ElementType = elementType;
			this.Range = range;
			this.Body = body;
		}
	}

	public class ASTTypeDef : ASTDeclaration
	{
		public class ASTField : ASTDeclaration
		{
			public string Name { get; }
			public ASTTypeNameParts Type { get; }

			public ASTField(string name, ASTTypeNameParts type)
			{
				this.Name = name;
				this.Type = type;
			}
		}

		public string Name { get; }
		public IList<ASTField> Fields { get; }

		public TypeSymbol Symbol { get; set; }

		public ASTTypeDef(string name, IList<ASTField> fields)
		{
			this.Name = name;
			this.Fields = fields;
		}
	}

	public class ASTMemberAccess : ASTExpression
	{
		public ASTExpression LHS { get; }
		public string DataMember { get; }

		public FieldSymbol FieldSymbol { get; set; }

		public ASTMemberAccess(ASTExpression LHS, string dataMember)
		{
			this.LHS = LHS;
			this.DataMember = dataMember;
		}
	}

	public class ASTTypeConstruction : ASTExpression
	{
		public string Name { get; }
		public IList<ASTExpression> Args { get; }

		public TypeSymbol ReferencedSymbol { get; set; }

		public ASTTypeConstruction(string name, IList<ASTExpression> args)
		{
			this.Name = name;
			this.Args = args;
		}
	}
}
