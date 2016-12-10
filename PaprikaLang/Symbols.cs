using System;
using System.Collections.Generic;
using System.Reflection;

namespace PaprikaLang
{
	public enum TypePrimitive
	{
		Number,
		String,
		Boolean,
		Func,
		UserDefined,
		Unknown
	}


	public class TypeDetail
	{
		public string Name { get; }
		public TypePrimitive TypePrimitive { get; }
		public DataTypeSymbol UserDefinedSymbol { get; }

		public TypeDetail(TypePrimitive typePrimitive)
		{
			Name = null;
			TypePrimitive = typePrimitive;
		}

		public TypeDetail(DataTypeSymbol userDefinedTypeSymbol)
		{
			Name = userDefinedTypeSymbol.Name;
			TypePrimitive = TypePrimitive.UserDefined;
			UserDefinedSymbol = userDefinedTypeSymbol;
		}

		public override string ToString()
		{
			return string.Format("[TypeDetail: Name={0}, TypePrimitive={1}, UserDefinedSymbol={2}]", Name, TypePrimitive, UserDefinedSymbol);
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return obj is TypeDetail && this == (TypeDetail)obj;
		}

		public static bool operator ==(TypeDetail lhs, TypeDetail rhs)
		{
			return lhs.Name == rhs.Name && lhs.TypePrimitive == rhs.TypePrimitive && lhs.UserDefinedSymbol == rhs.UserDefinedSymbol;
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


	public class DataTypeSymbol : ISymbol
	{
		public string Name { get; }
		public TypeDetail Type { get; }

		public DataTypeSymbol(string name)
		{
			Name = name;
			Type = new TypeDetail(this);
		}
	}

	public class FunctionSymbol : ISymbol
	{
		public string Name { get; }
		public IList<NamedValueSymbol> Params { get; }
		public TypeDetail Type { get; }
		public SymbolTable SymbolTable { get; }
		public TypeDetail ReturnType { get; }

		public FunctionSymbol(string name, SymbolTable symbolTable, TypeDetail returnType)
		{
			Name = name;
			Params = new List<NamedValueSymbol>();
			SymbolTable = symbolTable;
			ReturnType = returnType;
			Type = new TypeDetail(TypePrimitive.Func); // TOOD do function types properly
		}
	}

	public class NamedValueSymbol : ISymbol
	{
		public string Name { get; }
		public TypeDetail Type { get; }

		public NamedValueSymbol(string name, TypeDetail typeInfo)
		{
			Name = name;
			Type = typeInfo;
		}
	}


	public class SymbolTable
	{
		private static TypeDetail numberType = new TypeDetail(TypePrimitive.Number);
		private static TypeDetail stringType = new TypeDetail(TypePrimitive.String);

		private SymbolTable parent;
		private IDictionary<string, ISymbol> symbols = new Dictionary<string, ISymbol>();

		public SymbolTable(SymbolTable parentSymbols)
		{
			this.parent = parentSymbols;
		}

		public void Add(ISymbol symbol)
		{
			symbols.Add(symbol.Name, symbol);
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
			if (typename == "number")
			{
				return numberType;
			}
			else if (typename == "string")
			{
				return stringType;
			}

			throw new Exception("Unable to resolve type name: " + typename);
		}
	}

}
