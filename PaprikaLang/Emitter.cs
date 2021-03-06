﻿using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using PaprikaLib;

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

			if (typeDetail.TypePrimitive == TypePrimitive.Boolean)
			{
				return typeof(bool);
			}

			if (typeDetail.IsBound)
			{
				if (typeDetail.GenericType == TypeDetail.UnboundList)
				{
					Type elemType = MapTypeInfo(typeDetail.GenericParams.First());
					return typeof(IList<>).MakeGenericType(elemType);
				}
			}

			throw new Exception("TypeInfo '" + typeDetail + "' does not map to a .Net type");
		}
	}

	public class LoweredSymbolTable
	{
		private IDictionary<TypeDetail, Type> typeDetailsToType = new Dictionary<TypeDetail, Type>();

		private IDictionary<FunctionSymbol, MethodBuilder> funcsToMethods = new Dictionary<FunctionSymbol, MethodBuilder>();
		private IDictionary<ParamSymbol, short> argsToIndex = new Dictionary<ParamSymbol, short>();
		private IDictionary<LocalSymbol, LocalBuilder> localSymbolsToBuilders = new Dictionary<LocalSymbol, LocalBuilder>();
		private IDictionary<FieldSymbol, FieldInfo> fieldSymbolToFields = new Dictionary<FieldSymbol, FieldInfo>();

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

		public void AddType(TypeSymbol typeSym, TypeBuilder typeBuilder)
		{
			typeDetailsToType.Add(typeSym.Type, typeBuilder);
		}

		public void AddFieldMember(FieldSymbol fieldSym, FieldInfo field)
		{
			fieldSymbolToFields.Add(fieldSym, field);
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

		public FieldInfo GetField(FieldSymbol fieldSym)
		{
			return fieldSymbolToFields[fieldSym];
		}

		public Type GetType(TypeDetail typeDetail)
		{
			Type mappedType = null;
			if (typeDetailsToType.TryGetValue(typeDetail, out mappedType))
			{
				return mappedType;
			}

			return DotNetTypeMapper.MapTypeInfo(typeDetail);
		}

		public TypeBuilder GetTypeBuilder(TypeSymbol typeSym)
		{
			return GetType(typeSym.Type) as TypeBuilder;
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
			TypeBuilder typeBuilder = moduleBuilder.DefineType(mainClassName, TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.NotPublic);

			LoweredSymbolTable loweredSymbols =
				new StructuralSymbolLoweringStage(assemblyBuilder, moduleBuilder, typeBuilder).LowerSymbols(module);

			new ILGenStage(loweredSymbols).EmitIL(module);

			typeBuilder.CreateType();
			assemblyBuilder.Save(filename);
		}
	}

	public class StructuralSymbolLoweringStage
	{
		private AssemblyBuilder assemblyBuilder;
		private ModuleBuilder moduleBuilder;
		private TypeBuilder typeBuilderForMethods;

		private LoweredSymbolTable loweredSymbols = new LoweredSymbolTable();

		public StructuralSymbolLoweringStage(AssemblyBuilder assemblyBuilder, ModuleBuilder moduleBuilder, TypeBuilder typeBuilderForMethods)
		{
			this.assemblyBuilder = assemblyBuilder;
			this.moduleBuilder = moduleBuilder;
			this.typeBuilderForMethods = typeBuilderForMethods;
		}

		public LoweredSymbolTable LowerSymbols(ASTModule module)
		{
			MethodInfo mainMethod = null;

			// declare all the type builders
			foreach (var node in module.Body.Body)
			{
				if (node is ASTTypeDef)
				{
					CreateTypeBuilder(((ASTTypeDef)node).Symbol);
				}
			}

			// declare all the method builders
			foreach (var node in module.Body.Body)
			{
				if (node is ASTFunctionDef)
				{
					MethodInfo method = DeclareMethodBuilders((ASTFunctionDef)node);

					// set the main method if we encounter it
					if (method.Name == "Main")
					{
						//assemblyBuilder.SetEntryPoint(method);
						mainMethod = method;
					}
				}
			}

			var entryMethod = typeBuilderForMethods.DefineMethod("Execute", MethodAttributes.Public | MethodAttributes.Static);
			assemblyBuilder.SetEntryPoint(entryMethod);
			var ilGen = entryMethod.GetILGenerator();
			ilGen.Emit(OpCodes.Call, mainMethod);
			ilGen.Emit(OpCodes.Box, mainMethod.ReturnType);
			ilGen.Emit(OpCodes.Call, typeof(ListOps).GetMethod("ToStringRepresentation", new System.Type[] { typeof(object) }));
			ilGen.Emit(OpCodes.Call, typeof(System.Console).GetMethod("WriteLine", new System.Type[] { typeof(object) }));
			ilGen.Emit(OpCodes.Ret);

			return loweredSymbols;
		}

		private MethodInfo DeclareMethodBuilders(ASTFunctionDef funcDef)
		{
			// declare all nested methods
			foreach (var node in funcDef.Body.Body)
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

			MethodBuilder method = typeBuilderForMethods.DefineMethod(
				funcSym.Name,
				MethodAttributes.Public | MethodAttributes.Static,
				returnType,
				paramTypes);

			loweredSymbols.AddMethod(funcSym, method);
			return method;
		}

		public void CreateTypeBuilder(TypeSymbol typeSym)
		{
			TypeBuilder typeBuilder = moduleBuilder.DefineType(typeSym.Name, TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Public);
			loweredSymbols.AddType(typeSym, typeBuilder);
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
			foreach (var node in module.Body.Body)
			{
				Gen(node as dynamic, null);
			}
		}

		private void Gen(ASTBlock block, ILGenerator ilGen)
		{
			// emit n-1 body nodes but discard them from the stack
			for (int i = 0; i < block.Body.Count - 1; ++i)
			{
				ASTNode node = block.Body[i];
				Gen(node as dynamic, ilGen);

				// expressions leave values on the stack which we need to discard
				if (node is ASTExpression)
				{
					ilGen.Emit(OpCodes.Pop);
				}
			}

			// emit the last body node to keep on the stack
			Gen(block.Body.Last() as dynamic, ilGen);
		}

		private void Gen(ASTTypeDef typeDef, ILGenerator ilGen)
		{
			TypeBuilder typeBuilder = loweredSymbols.GetTypeBuilder(typeDef.Symbol);

			Type[] fieldTypes = typeDef.Symbol.Fields.Select(
				f => loweredSymbols.GetType(f.Type)).ToArray();

			// define a constructor which will initialise each field
			ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(
				MethodAttributes.Public,
				CallingConventions.Standard,
				fieldTypes);

			ILGenerator typeIlGen = constructorBuilder.GetILGenerator();

			for (int i = 0; i < typeDef.Symbol.Fields.Count; ++i)
			{
				FieldSymbol fieldSym = typeDef.Symbol.Fields[i];
				Type fieldType = fieldTypes[i];

				FieldBuilder fieldBuilder = typeBuilder.DefineField(fieldSym.Name, fieldType, FieldAttributes.Public);
				loweredSymbols.AddFieldMember(fieldSym, fieldBuilder);

				// init field in constructor
				typeIlGen.Emit(OpCodes.Ldarg_0); // load 'this'
				typeIlGen.Emit(OpCodes.Ldarg, i + 1);
				typeIlGen.Emit(OpCodes.Stfld, fieldBuilder);
			}

			typeIlGen.Emit(OpCodes.Ret);
			typeBuilder.CreateType();
		}

		private void Gen(ASTFunctionDef funcDef, ILGenerator ilGen)
		{
			// overwrite ilGen with the appropriate one for this method
			ilGen = loweredSymbols.GetMethodILGenerator(funcDef.Symbol);
			Gen(funcDef.Body, ilGen);
			ilGen.Emit(OpCodes.Ret);
		}

		private void Gen(ASTLetDef letDef, ILGenerator ilGen)
		{
			LocalBuilder local = ilGen.DeclareLocal(
				loweredSymbols.GetType(letDef.Symbol.Type));

			loweredSymbols.AddLocal(letDef.Symbol, local);

			foreach (ASTExpression assignmentBody in letDef.AssignmentBodies)
			{
				GenAssignmentToLocal(assignmentBody as dynamic, local, ilGen);
			}
		}

		private void Gen(ASTNumeric numeric, ILGenerator ilGen)
		{
			ilGen.Emit(OpCodes.Ldc_R8, numeric.Value);
		}

		private void Gen(ASTString str, ILGenerator ilGen)
		{
			ilGen.Emit(OpCodes.Ldstr, str.Value);
		}

		private void Gen(ASTBoolean booleanLiteral, ILGenerator ilGen)
		{
			ilGen.Emit(booleanLiteral.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
		}

		private void Gen(ASTList list, ILGenerator ilGen)
		{
			Gen(list.From as dynamic, ilGen);
			Gen(list.To as dynamic, ilGen);
			if (list.Step != null)
			{
				Gen(list.Step as dynamic, ilGen);
			}
			else
			{
				ilGen.Emit(OpCodes.Ldc_R8, 1d);
			}
			ilGen.Emit(OpCodes.Call, typeof(ListOps).GetMethod("GenerateList"));
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
				Gen(ifStatement.IfBody as dynamic, ilGen);
				ilGen.MarkLabel(doneLabel);
			}
			else // an if-else statement
			{
				Label doneLabel = ilGen.DefineLabel();
				Label elseLabel = ilGen.DefineLabel();
				ilGen.Emit(OpCodes.Brfalse, elseLabel);
				Gen(ifStatement.IfBody as dynamic, ilGen);
				ilGen.Emit(OpCodes.Br, doneLabel);
				ilGen.MarkLabel(elseLabel);
				Gen(ifStatement.ElseBody as dynamic, ilGen);
				ilGen.MarkLabel(doneLabel);
			}
		}

		private void Gen(ASTMemberAccess memberAccess, ILGenerator ilGen)
		{
			Gen(memberAccess.LHS as dynamic, ilGen);
			FieldInfo field = loweredSymbols.GetField(memberAccess.FieldSymbol);
			ilGen.Emit(OpCodes.Ldfld, field);
		}

		private void Gen(ASTTypeConstruction typeConstruction, ILGenerator ilGen)
		{
			foreach (var param in typeConstruction.Args)
			{
				Gen(param as dynamic, ilGen);
			}

			Type type = loweredSymbols.GetType(typeConstruction.ReferencedSymbol.Type);
			ilGen.Emit(OpCodes.Newobj, type.GetConstructors()[0]);
		}

		private void GenAssignmentToLocal(ASTIfStatement conditionalAssignment, LocalBuilder local, ILGenerator ilGen)
		{
			// if it has an else block then it's not actually a conditional assignment
			// so just treat it as a regular expression
			if (conditionalAssignment.ElseBody != null)
			{
				ASTExpression expr = conditionalAssignment;
				GenAssignmentToLocal(expr, local, ilGen);
				return;
			}

			Gen(conditionalAssignment.ConditionExpr as dynamic, ilGen);

			Label doneLabel = ilGen.DefineLabel();
			ilGen.Emit(OpCodes.Brfalse, doneLabel);
			Gen(conditionalAssignment.IfBody as dynamic, ilGen);
			ilGen.Emit(OpCodes.Stloc, local); // conditionally assign to the local
			ilGen.MarkLabel(doneLabel);
		}

		private void GenAssignmentToLocal(ASTForeachAssignment foreachAssignment, LocalBuilder local, ILGenerator ilGen)
		{
			LocalBuilder elementLocal = ilGen.DeclareLocal(
				loweredSymbols.GetType(foreachAssignment.ReferencedSymbol.Type));

			loweredSymbols.AddLocal(foreachAssignment.ReferencedSymbol, elementLocal);

			// gen an enumerator for the range
			Gen(foreachAssignment.Range as dynamic, ilGen);
			ilGen.Emit(OpCodes.Callvirt, typeof(IEnumerable).GetMethod("GetEnumerator"));
			LocalBuilder enumeratorLocal = ilGen.DeclareLocal(typeof(IEnumerator<>));
			ilGen.Emit(OpCodes.Stloc, enumeratorLocal);

			Label endOfTry = ilGen.BeginExceptionBlock();

				// mark the start of the loop
				Label topOfLoop = ilGen.DefineLabel();
				ilGen.MarkLabel(topOfLoop);

				// advance the enumerator
				ilGen.Emit(OpCodes.Ldloc, enumeratorLocal);
				ilGen.Emit(OpCodes.Callvirt, typeof(IEnumerator).GetMethod("MoveNext"));

				// if no more then jump to the end
				ilGen.Emit(OpCodes.Brfalse, endOfTry);

				// extract the element and assign to the local
				ilGen.Emit(OpCodes.Ldloc, enumeratorLocal);
				ilGen.Emit(OpCodes.Callvirt, typeof(IEnumerator<>).MakeGenericType(elementLocal.LocalType).GetProperty("Current").GetGetMethod());
				ilGen.Emit(OpCodes.Stloc, elementLocal);

				// emit the body of the loop
				Gen(foreachAssignment.Body as dynamic, ilGen);

				// assign the accumulating local
				ilGen.Emit(OpCodes.Stloc, local);

				// repeat loop
				ilGen.Emit(OpCodes.Br, topOfLoop);

			ilGen.BeginFinallyBlock();

				// finally, dispose of the enumerator
				ilGen.Emit(OpCodes.Ldloc, enumeratorLocal);
				ilGen.Emit(OpCodes.Callvirt, typeof(IDisposable).GetMethod("Dispose"));

			ilGen.EndExceptionBlock();
		}

		private void GenAssignmentToLocal(ASTExpression simpleAssignment, LocalBuilder local, ILGenerator ilGen)
		{
			Gen(simpleAssignment as dynamic, ilGen);
			ilGen.Emit(OpCodes.Stloc, local);
		}
	}
}
