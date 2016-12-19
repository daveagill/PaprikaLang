using System;
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
				new StructuralSymbolLoweringStage(assemblyBuilder, typeBuilder).LowerSymbols(module);

			new ILGenStage(loweredSymbols).EmitIL(module);

			typeBuilder.CreateType();
			assemblyBuilder.Save(filename);
		}
	}

	public class LoweredSymbolTable
	{
		private IDictionary<FunctionSymbol, MethodBuilder> funcsToMethods = new Dictionary<FunctionSymbol, MethodBuilder>();
		private IDictionary<ParamSymbol, short> argsToIndex = new Dictionary<ParamSymbol, short>();
		private IDictionary<LocalSymbol, LocalBuilder> localSymbolsToBuilders = new Dictionary<LocalSymbol, LocalBuilder>();

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

		public void AddLocal(LocalSymbol locaSym, LocalBuilder localBuilder)
		{
			localSymbolsToBuilders.Add(locaSym, localBuilder);
		}

		public MethodInfo GetMethod(FunctionSymbol funcSym)
		{
			return funcsToMethods[funcSym];
		}

		public ILGenerator GetMethodILGenerator(FunctionSymbol funcSym)
		{
			return funcsToMethods[funcSym].GetILGenerator();
		}

		public short GetArgIndex(ParamSymbol arg)
		{
			return argsToIndex[arg];
		}

		public LocalBuilder GetLocal(LocalSymbol localSym)
		{
			return localSymbolsToBuilders[localSym];
		}

		public Type GetType(TypeDetail typeDetail)
		{
			return DotNetTypeMapper.MapTypeInfo(typeDetail);
		}
	}

	public class StructuralSymbolLoweringStage
	{
		private AssemblyBuilder assemblyBuilder;
		private TypeBuilder typeBuilder;

		private LoweredSymbolTable loweredSymbols = new LoweredSymbolTable();

		public StructuralSymbolLoweringStage(AssemblyBuilder assemblyBuilder, TypeBuilder typeBuilder)
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
			ilGen.Emit(OpCodes.Call, typeof(System.Console).GetMethod("WriteLine", new System.Type[] { mainMethod.ReturnType }));
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
				p => loweredSymbols.GetType(p.Type)).ToArray();

			Type returnType = loweredSymbols.GetType(funcSym.ReturnType);

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
			GenBlock(funcDef.Body, ilGen);
			ilGen.Emit(OpCodes.Ret);
		}

		private void Gen(ASTLetDef letDef, ILGenerator ilGen)
		{
			LocalBuilder local = ilGen.DeclareLocal(
				loweredSymbols.GetType(letDef.ReferencedSymbol.Type));

			loweredSymbols.AddLocal(letDef.ReferencedSymbol, local);

			GenBlock(letDef.AssignmentBody, ilGen);
			ilGen.Emit(OpCodes.Stloc, local);
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
			if (namedValue.ReferencedSymbol is ParamSymbol)
			{
				short argIndex = loweredSymbols.GetArgIndex((ParamSymbol)namedValue.ReferencedSymbol);
				ilGen.Emit(OpCodes.Ldarg, argIndex);
			}
			else if (namedValue.ReferencedSymbol is LocalSymbol)
			{
				LocalBuilder local = loweredSymbols.GetLocal((LocalSymbol)namedValue.ReferencedSymbol);
				ilGen.Emit(OpCodes.Ldloc, local);
			}
			else
			{
				throw new Exception("Unsupported bytecode emission for ASTNamedValue referencing a " + namedValue.ReferencedSymbol.GetType());
			}
		}

		private void Gen(ASTBinaryOperator binOp, ILGenerator ilGen)
		{
			bool isStringOp = binOp.ResultType.TypePrimitive == TypePrimitive.String;
			if (isStringOp)
			{
				Gen(binOp.LHS as dynamic, ilGen);
				Type LHSLoweredType = loweredSymbols.GetType(binOp.LHSType);
				ilGen.Emit(OpCodes.Box, LHSLoweredType);

				Gen(binOp.RHS as dynamic, ilGen);
				Type RHSLoweredType = loweredSymbols.GetType(binOp.RHSType);
				ilGen.Emit(OpCodes.Box, RHSLoweredType);

				MethodInfo equalsMethod = typeof(string).GetMethod("Equals", new System.Type[] { typeof(string), typeof(string) });
				MethodInfo concatMethod = typeof(string).GetMethod("Concat", new System.Type[] { LHSLoweredType, RHSLoweredType });

				switch (binOp.Op)
				{
					case BinaryOps.Equals:
						ilGen.Emit(OpCodes.Call, equalsMethod);
						return;
					case BinaryOps.NotEquals:
						ilGen.Emit(OpCodes.Call, equalsMethod);
						ilGen.Emit(OpCodes.Not);
						return;
					case BinaryOps.StringConcat:
						ilGen.Emit(OpCodes.Call, concatMethod);
						return;
					default:
						throw new Exception("Unexpected binary operator for string based operands: " + binOp.Op);
				}
			}
			else
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
					case BinaryOps.Equals:
						ilGen.Emit(OpCodes.Ceq);
						return;
					case BinaryOps.NotEquals:
						ilGen.Emit(OpCodes.Ceq);
						ilGen.Emit(OpCodes.Not);
						return;
					case BinaryOps.GreaterThan:
						ilGen.Emit(OpCodes.Cgt);
						return;
					case BinaryOps.LessThan:
						ilGen.Emit(OpCodes.Clt);
						return;
					case BinaryOps.Modulus:
						ilGen.Emit(OpCodes.Rem);
						return;
					case BinaryOps.And:
						ilGen.Emit(OpCodes.And);
						return;
					case BinaryOps.Or:
						ilGen.Emit(OpCodes.Or);
						return;
					default:
						throw new Exception("Unexpected binary operator for number based operands: " + binOp.Op);
				}
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

		private void Gen(ASTIfStatement ifStatement, ILGenerator ilGen)
		{
			Gen(ifStatement.ConditionExpr as dynamic, ilGen);

			if (ifStatement.ElseBody == null) // if without else
			{
				Label doneLabel = ilGen.DefineLabel();
				ilGen.Emit(OpCodes.Brfalse, doneLabel);
				GenBlock(ifStatement.IfBody, ilGen);
				ilGen.MarkLabel(doneLabel);
			}
			else // an if-else statement
			{
				Label doneLabel = ilGen.DefineLabel();
				Label elseLabel = ilGen.DefineLabel();
				ilGen.Emit(OpCodes.Brfalse, elseLabel);
				GenBlock(ifStatement.IfBody, ilGen);
				ilGen.Emit(OpCodes.Br, doneLabel);
				ilGen.MarkLabel(elseLabel);
				GenBlock(ifStatement.ElseBody, ilGen);
				ilGen.MarkLabel(doneLabel);
			}
		}

		private void GenBlock(IList<ASTNode> block, ILGenerator ilGen)
		{
			// emit n-1 body nodes but discard them from the stack
			for (int i = 0; i < block.Count - 1; ++i)
			{
				ASTNode node = block[i];
				Gen(node as dynamic, ilGen);

				// expressions leave values on the stack which we need to discard
				if (node is ASTExpression)
				{
					ilGen.Emit(OpCodes.Pop);
				}
			}

			// emit the last body node to keep on the stack
			Gen(block.Last() as dynamic, ilGen);
		}
	}
}
