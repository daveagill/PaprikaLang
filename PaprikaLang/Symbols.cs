using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PaprikaLang
{
	public enum TypePrimitive
	{
		Number,
		String,
		Boolean,
		List,
		Func,
		Structure,
		ConcreteGeneric,
		Unknown
	}


	public class TypeDetail
	{
		public static readonly TypeDetail Number = new TypeDetail("number", TypePrimitive.Number);
		public static readonly TypeDetail String = new TypeDetail("string", TypePrimitive.String);
		public static readonly TypeDetail Boolean = new TypeDetail("boolean", TypePrimitive.Boolean);
		public static readonly TypeDetail UnboundList = new TypeDetail("seq", TypePrimitive.List, 1);
		public static readonly TypeDetail ListOfNumbers = new TypeDetail(UnboundList, new TypeDetail[] { Number });
		public static readonly TypeDetail Func = new TypeDetail("func", TypePrimitive.Func);
		public static readonly TypeDetail Unknown = new TypeDetail("unknown", TypePrimitive.Unknown);

		public string SimpleName { get; }
		public TypePrimitive TypePrimitive { get; }

		public int UnboundArgCount { get; }
		public bool IsUnbound
		{
			get
			{
				return UnboundArgCount > 0;
			}
		}

		public TypeDetail GenericType { get; }
		public IList<TypeDetail> GenericParams { get; }
		public bool IsBound
		{
			get
			{
				return GenericType != null;
			}
		}

		public TypeDetail(string simpleName, TypePrimitive typePrimitive, int unboundArgCount = 0)
		{
			SimpleName = simpleName;
			TypePrimitive = typePrimitive;
			UnboundArgCount = unboundArgCount;
			GenericType = null;
			GenericParams = new List<TypeDetail>();
		}

		public TypeDetail(TypeDetail unboundGenericType, IList<TypeDetail> genericParams)
		{
			if (unboundGenericType.UnboundArgCount != genericParams.Count)
			{
				throw new InvalidOperationException();
			}

			SimpleName = unboundGenericType.SimpleName;
			TypePrimitive = TypePrimitive.ConcreteGeneric;
			GenericType = unboundGenericType;
			GenericParams = genericParams;
		}

		public override string ToString()
		{
			return string.Format("[TypeDetail: SimpleName={0}, TypePrimitive={1}, GenericType={2}]", SimpleName, TypePrimitive, GenericType);
		}

		public override int GetHashCode()
		{
			return SimpleName.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return obj is TypeDetail && this == (TypeDetail)obj;
		}

		public static bool operator ==(TypeDetail lhs, TypeDetail rhs)
		{
			return object.ReferenceEquals(lhs, rhs) ||
				         !object.ReferenceEquals(lhs, null) && !object.ReferenceEquals(rhs, null) &&
				         lhs.TypePrimitive == rhs.TypePrimitive &&
				         lhs.GenericParams.SequenceEqual(rhs.GenericParams);
		}

		public static bool operator !=(TypeDetail lhs, TypeDetail rhs)
		{
			return !(lhs == rhs);
		}
	}



	public interface ISymbol
	{
		string Name { get; }
		TypeDetail Type { get; }
	}


	public class TypeSymbol : ISymbol
	{
		public string Name { get; }
		public TypeDetail Type { get; }
		public IList<FieldSymbol> Fields { get; }

		public TypeSymbol(TypeDetail type)
		{
			Name = type.SimpleName;
			Type = type;
			Fields = new List<FieldSymbol>();
		}
	}

	public class FunctionSymbol : ISymbol
	{
		public string Name { get; }
		public IList<ParamSymbol> Params { get; }
		public TypeDetail Type { get; }
		public SymbolTable SymbolTable { get; }
		public TypeDetail ReturnType { get; }

		public FunctionSymbol(string name, SymbolTable symbolTable, TypeDetail returnType)
		{
			Name = name;
			Params = new List<ParamSymbol>();
			SymbolTable = symbolTable;
			ReturnType = returnType;
			Type = TypeDetail.Func; // TODO do function types properly
		}
	}

	public class ParamSymbol : ISymbol
	{
		public string Name { get; }
		public TypeDetail Type { get; }

		public ParamSymbol(string name, TypeDetail typeInfo)
		{
			Name = name;
			Type = typeInfo;
		}
	}

	public class LocalSymbol : ISymbol
	{
		public string Name { get; }
		public TypeDetail Type { get; }

		public LocalSymbol(string name, TypeDetail typeInfo)
		{
			Name = name;
			Type = typeInfo;
		}
	}

	public class FieldSymbol : ISymbol
	{
		public string Name { get; }
		public TypeDetail Type { get; }

		public FieldSymbol(string name, TypeDetail typeInfo)
		{
			Name = name;
			Type = typeInfo;
		}
	}


	public class SymbolTable
	{
		private SymbolTable parent;
		private IDictionary<string, ISymbol> symbols = new Dictionary<string, ISymbol>();
		private IDictionary<string, TypeDetail> types = new Dictionary<string, TypeDetail>();

		public SymbolTable(SymbolTable parentSymbols)
		{
			this.parent = parentSymbols;
		}

		public void Add(ISymbol symbol)
		{
			symbols.Add(symbol.Name, symbol);
		}

		public void AddType(TypeDetail type)
		{
			types.Add(type.SimpleName, type);
		}

		public ISymbol ResolveSymbol(string name)
		{
			ISymbol sym = null;
			symbols.TryGetValue(name, out sym);

			if (sym == null && parent != null)
			{
				sym = parent.ResolveSymbol(name);
			}

			if (sym == null)
			{
				throw new Exception("Unable to resolve symbol: " + name);
			}

			return sym;
		}

		public T ResolveSymbolAs<T>(string name) where T : class, ISymbol
		{
			T sym = ResolveSymbol(name) as T;
			if (sym == null)
			{
				throw new Exception("Unable to resolve symbol " + name + " as a " + typeof(T).Name);
			}
			return sym;
		}

		public TypeDetail ResolveType(string typename)
		{
			TypeDetail type = null;
			types.TryGetValue(typename, out type);

			if (type == null && parent != null)
			{
				type = parent.ResolveType(typename);
			}

			if (type == null)
			{
				throw new Exception("Unable to resolve type name: " + typename);
			}

			return type;
		}
	}

}
