﻿using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Collections.Generic;

namespace PaprikaLang
{
	public class DotNetTypeMapper
	{
		public static Type MapTypeInfo(TypeDetail typeDetail)
		{
			if (typeDetail.TypePrimitive == TypePrimitive.Number)
			{
				return typeof(double);
			}

			if (typeDetail.TypePrimitive == TypePrimitive.String)
			{
				return typeof(string);
			}

			throw new Exception("TypeInfo '" + typeDetail.Name + "' is currently not supported for bytecode emission");
		}
	}

	public class Emitter
	{
		public static void Emit(string assemblyName, string filename, ASTModule module)
		{
			AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
				new AssemblyName(assemblyName),
				AssemblyBuilderAccess.Save);

			ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName, filename);

			string mainClassName = assemblyName + "_PaprikaMainClass";
			TypeBuilder typeBuilder = moduleBuilder.DefineType(mainClassName, TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Public);

			LoweredSymbolTable loweredSymbols =
				new SymbolLoweringStage(assemblyBuilder, typeBuilder).LowerSymbols(module);

			new ILGenStage(loweredSymbols).EmitIL(module);

			typeBuilder.CreateType();
			assemblyBuilder.Save(filename);
		}
	}

	public class LoweredSymbolTable
	{
		private IDictionary<FunctionSymbol, MethodBuilder> funcsToMethods = new Dictionary<FunctionSymbol, MethodBuilder>();
		private IDictionary<NamedValueSymbol, short> argsToIndex = new Dictionary<NamedValueSymbol, short>();

		public void AddMethod(FunctionSymbol funcSym, MethodBuilder methodBuilder)
		{
			funcsToMethods.Add(funcSym, methodBuilder);

			short argIndex = 0;
			foreach (var arg in funcSym.Params)
			{
				argsToIndex.Add(arg, argIndex);
				++argIndex;
			}
		}

		public MethodInfo GetMethod(FunctionSymbol funcSym)
		{
			return funcsToMethods[funcSym];
		}

		public ILGenerator GetMethodILGenerator(FunctionSymbol funcSym)
		{
			return funcsToMethods[funcSym].GetILGenerator();
		}

		public short GetArgIndex(NamedValueSymbol arg)
		{
			return argsToIndex[arg];
		}

		public Type GetType(TypeDetail typeDetail)
		{
			return DotNetTypeMapper.MapTypeInfo(typeDetail);
		}
	}

	public class SymbolLoweringStage
	{
		private AssemblyBuilder assemblyBuilder;
		private TypeBuilder typeBuilder;

		private LoweredSymbolTable loweredSymbols = new LoweredSymbolTable();

		public SymbolLoweringStage(AssemblyBuilder assemblyBuilder, TypeBuilder typeBuilder)
		{
			this.assemblyBuilder = assemblyBuilder;
			this.typeBuilder = typeBuilder;
		}

		public LoweredSymbolTable LowerSymbols(ASTModule module)
		{
			MethodInfo mainMethod = null;

			// declare all the method builders
			foreach (ASTFunctionDef funcDef in module.FunctionDefs)
			{
				MethodInfo method = DeclareMethodBuilders(funcDef);

				// set the main method if we encounter it
				if (method.Name == "Main")
				{
					//assemblyBuilder.SetEntryPoint(method);
					mainMethod = method;
				}
			}

			var entryMethod = typeBuilder.DefineMethod("Execute", MethodAttributes.Public | MethodAttributes.Static);
			assemblyBuilder.SetEntryPoint(entryMethod);
			var ilGen = entryMethod.GetILGenerator();
			ilGen.Emit(OpCodes.Call, mainMethod);
			ilGen.Emit(OpCodes.Call, typeof(System.Console).GetMethod("WriteLine", new System.Type[] { typeof(double) }));
			ilGen.Emit(OpCodes.Ret);

			return loweredSymbols;
		}

		private MethodInfo DeclareMethodBuilders(ASTFunctionDef funcDef)
		{
			// declare all nested methods
			foreach (var node in funcDef.Body)
			{
				if (node is ASTFunctionDef)
				{
					DeclareMethodBuilders((ASTFunctionDef)node);
				}
			}

			return CreateMethodBuilder(funcDef.Symbol);
		}

		public MethodBuilder CreateMethodBuilder(FunctionSymbol funcSym)
		{
			Type[] paramTypes = funcSym.Params.Select(
				p => DotNetTypeMapper.MapTypeInfo(p.Type)).ToArray();

			Type returnType = DotNetTypeMapper.MapTypeInfo(funcSym.ReturnType);

			MethodBuilder method = typeBuilder.DefineMethod(
				funcSym.Name,
				MethodAttributes.Public | MethodAttributes.Static,
				returnType,
				paramTypes);

			loweredSymbols.AddMethod(funcSym, method);
			return method;
		}
	}

	public class ILGenStage
	{
		private LoweredSymbolTable loweredSymbols;

		public ILGenStage(LoweredSymbolTable loweredSymbols)
		{
			this.loweredSymbols = loweredSymbols;
		}

		public void EmitIL(ASTModule module)
		{
			foreach (ASTFunctionDef funcDef in module.FunctionDefs)
			{
				Gen(funcDef, null);
			}
		}

		private void Gen(ASTFunctionDef funcDef, ILGenerator ilGen)
		{
			// overwrite ilGen with the appropriate one for this method
			ilGen = loweredSymbols.GetMethodILGenerator(funcDef.Symbol);

			// emit all body nodes except the last one
			for (int i = 0; i < funcDef.Body.Count - 1; ++i)
			{
				Gen(funcDef.Body[i] as dynamic, ilGen);
				ilGen.Emit(OpCodes.Pop);
			}

			// emit the last body node to use as the return value
			Gen(funcDef.Body.Last() as dynamic, ilGen);
			ilGen.Emit(OpCodes.Ret);
		}

		private void Gen(ASTNumeric numeric, ILGenerator ilGen)
		{
			ilGen.Emit(OpCodes.Ldc_R8, numeric.Value);
		}

		private void Gen(ASTString str, ILGenerator ilGen)
		{
			ilGen.Emit(OpCodes.Ldstr, str.Value);
		}

		private void Gen(ASTNamedValue namedValue, ILGenerator ilGen)
		{
			if (namedValue.ReferencedSymbol is NamedValueSymbol)
			{
				short argIndex = loweredSymbols.GetArgIndex((NamedValueSymbol)namedValue.ReferencedSymbol);
				ilGen.Emit(OpCodes.Ldarg, argIndex);
			}
			else
			{
				throw new Exception("Unsupported bytecode emission for ASTNamedValue referencing a " + namedValue.ReferencedSymbol.GetType());
			}
		}

		private void Gen(ASTBinaryOperator binOp, ILGenerator ilGen)
		{
			Gen(binOp.LHS as dynamic, ilGen);
			Gen(binOp.RHS as dynamic, ilGen);

			switch (binOp.Op)
			{
				case BinaryOps.Add:
					ilGen.Emit(OpCodes.Add);
					return;
				case BinaryOps.Subtract:
					ilGen.Emit(OpCodes.Sub);
					return;
				case BinaryOps.Multiply:
					ilGen.Emit(OpCodes.Mul);
					return;
				case BinaryOps.Divide:
					ilGen.Emit(OpCodes.Div);
					return;
			}
		}

		private void Gen(ASTFunctionCall funcCall, ILGenerator ilGen)
		{
			foreach (var param in funcCall.Args)
			{
				Gen(param as dynamic, ilGen);
			}

			MethodInfo method = loweredSymbols.GetMethod(funcCall.ReferencedSymbol);
			ilGen.Emit(OpCodes.Call, method);
		}
	}
}
