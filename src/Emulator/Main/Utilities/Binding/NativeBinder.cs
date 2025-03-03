//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Logging;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using Dynamitey;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Utilities.Binding
{
    /// <summary>
    /// The <c>NativeBinder</c> class lets one bind managed delegates from given class to functions
    /// of a given native library and vice versa.
    /// </summary>
    public sealed class NativeBinder : IDisposable
    {
        static NativeBinder()
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(nameof(NativeBinder)), AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule(nameof(NativeBinder));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Renode.Utilities.Runtime.NativeBinder"/> class
        /// and performs binding between the class and given library.
        /// </summary>
        /// <param name='classToBind'>
        /// Class to bind.
        /// </param>
        /// <param name='libraryFile'>
        /// Library file to bind.
        /// </param>
        /// <remarks>
        /// Please note that:
        /// <list type="bullet">
        /// <item><description>
        /// This works now only with ELF libraries.
        /// </description></item>
        /// <item><description>
        /// You have to hold the reference to created native binder as long as the native functions
        /// can call managed one.
        /// </description></item>
        /// <item><description>
        /// You should dispose native binder after use to free memory taken by the native library.
        /// </description></item>
        /// </list>
        /// </remarks>
        public NativeBinder(IEmulationElement classToBind, string libraryFile)
        {
            delegateStore = new object[0];
#if !PLATFORM_WINDOWS && !NET
            // According to https://github.com/dotnet/runtime/issues/26381#issuecomment-394765279,
            // mono does not enforce the restrictions on pinned GCHandle objects.
            // On .NET Core trying to pin unallowed object throws exception about non-primitive or non-blittable data.
            handles = new GCHandle[0];
#endif
            this.classToBind = classToBind;
            libraryAddress = SharedLibraries.LoadLibrary(libraryFile);
            libraryFileName = libraryFile;
            var importFields = classToBind.GetType().GetAllFields().Where(x => x.IsDefined(typeof(ImportAttribute), false)).ToList();
            EnsureWrappersType(importFields);
            wrappersObj = CreateWrappersObject();
            try
            {
                ResolveCallsToNative(importFields);
                ResolveCallsToManaged();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            DisposeInner();
            GC.SuppressFinalize(this);
        }

        private void DisposeInner()
        {
#if !PLATFORM_WINDOWS && !NET
            foreach(var handle in handles)
            {
                handle.Free();
            }
#endif
            if(libraryAddress != IntPtr.Zero)
            {
                SharedLibraries.UnloadLibrary(libraryAddress);
                libraryAddress = IntPtr.Zero;
            }
        }

        ~NativeBinder()
        {
            DisposeInner();
        }

        private void EnsureWrappersType(List<FieldInfo> importFields)
        {
            // This type lives in our NativeBinder dynamic assembly (see the static constructor).
            // This means that if we find such a type, it has to have been created by us and we can
            // just use it without verifying its layout.
            // Its name is derived from the full name of the class to bind (including its namespace): for example,
            // the wrappers type for Some.Namespace.Class will be NativeBinder.Some.Namespace.ClassWrappers.
            var wrappersTypeName = $"NativeBinder.{classToBind.GetType().FullName}Wrappers";

            lock(moduleBuilder)
            {
                wrappersType = moduleBuilder.GetType(wrappersTypeName);

                if(wrappersType == null)
                {
                    var typeBuilder = moduleBuilder.DefineType(wrappersTypeName);
                    typeBuilder.DefineField(nameof(ExceptionKeeper), typeof(ExceptionKeeper), FieldAttributes.Public);
                    typeBuilder.DefineField(nameof(classToBind), classToBind.GetType(), FieldAttributes.Public);

                    foreach(var field in importFields)
                    {
                        var attribute = (ImportAttribute)field.GetCustomAttributes(false).Single(x => x is ImportAttribute);
                        if(attribute.UseExceptionWrapper)
                        {
                            typeBuilder.DefineField(field.Name, field.FieldType, FieldAttributes.Public);
                        }
                    }

                    wrappersType = typeBuilder.CreateType();
                }

                exceptionKeeperField = wrappersType.GetField(nameof(ExceptionKeeper));
                instanceField = wrappersType.GetField(nameof(classToBind));
            }
        }

        private object CreateWrappersObject()
        {
            var wrappers = Activator.CreateInstance(wrappersType);
            exceptionKeeperField.SetValue(wrappers, new ExceptionKeeper());
            instanceField.SetValue(wrappers, classToBind);
            return wrappers;
        }

        private Delegate WrapImport(FieldInfo importField, FieldInfo innerField)
        {
            var throwExceptions = typeof(ExceptionKeeper).GetMethod(nameof(ExceptionKeeper.ThrowExceptions));
            var importType = importField.FieldType;
            var invoke = importType.GetMethod("Invoke");
            Type[] paramTypes = invoke.GetParameters().Select(p => p.ParameterType).ToArray();
            Type[] paramTypesWithWrappersType = new Type[] { wrappersType }.Concat(paramTypes).ToArray();
            DynamicMethod method = new DynamicMethod(importField.Name, invoke.ReturnType, paramTypesWithWrappersType, wrappersType);
            var il = method.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // wrappersType instance
            il.Emit(OpCodes.Ldfld, innerField);
            for(int i = 0; i < paramTypes.Length; ++i)
            {
                il.Emit(OpCodes.Ldarg, 1 + i);
            }
            il.EmitCall(OpCodes.Callvirt, invoke, null); // call inner

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, exceptionKeeperField);
            il.EmitCall(OpCodes.Call, throwExceptions, null); // call ExceptionKeeper.ThrowExceptions

            il.Emit(OpCodes.Ret);

            return method.CreateDelegate(importType, wrappersObj);
        }

        private void ResolveCallsToNative(List<FieldInfo> importFields)
        {
            classToBind.NoisyLog("Binding managed -> native calls.");

            foreach (var field in importFields)
            {
                var attribute = (ImportAttribute)field.GetCustomAttributes(false).First(x => x is ImportAttribute);
                var cName = GetWrappedName(attribute.Name ?? GetCName(field.Name), attribute.UseExceptionWrapper);
                classToBind.NoisyLog(string.Format("(NativeBinder) Binding {1} as {0}.", field.Name, cName));
                Delegate result = null;
                try
                {
                    var address = SharedLibraries.GetSymbolAddress(libraryAddress, cName);
                    // Dynamically create a non-generic delegate type and make one from a function pointer
                    var invoke = field.FieldType.GetMethod("Invoke");
                    var delegateType = DelegateTypeFromParamsAndReturn(invoke.GetParameters().Select(p => p.ParameterType), invoke.ReturnType);
                    var generatedDelegate = Marshal.GetDelegateForFunctionPointer(address, delegateType);
                    // The method returned by GetDelegateForFunctionPointer is static on Mono, but not .NET
                    var delegateTarget = generatedDelegate.Method.IsStatic ? null : generatedDelegate;
                    // "Convert" the delegate from the dynamically-generated non-generic type
                    // to the field type used in the bound class (which might be generic)
                    result = Delegate.CreateDelegate(field.FieldType, delegateTarget, generatedDelegate.Method);
                }
                catch
                {
                    if(!attribute.Optional)
                    {
                        throw;
                    }
                }

                if(attribute.UseExceptionWrapper && result != null)
                {
                    var innerField = wrappersType.GetField(field.Name);
                    innerField.SetValue(wrappersObj, result);
                    field.SetValue(classToBind, WrapImport(field, innerField));
                }
                else
                {
                    field.SetValue(classToBind, result);
                }
            }
        }

        private Delegate WrapExport(Type delegateType, MethodInfo innerMethod)
        {
            var addException = typeof(ExceptionKeeper).GetMethod(nameof(ExceptionKeeper.AddException));
            var nativeUnwind = classToBind.GetType().GetMethod(nameof(INativeUnwindable.NativeUnwind));

            Type[] paramTypes = innerMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            Type[] paramTypesWithWrappersType = new Type[] { wrappersType }.Concat(paramTypes).ToArray();
            // We need skipVisibility to be able to access superclass private methods.
            var attacheeWrapper = new DynamicMethod(innerMethod.Name + "Wrapper", innerMethod.ReturnType, paramTypesWithWrappersType, wrappersType, skipVisibility: true);
            var il = attacheeWrapper.GetILGenerator();
            LocalBuilder retval = null;
            if(innerMethod.ReturnType != typeof(void))
            {
                retval = il.DeclareLocal(innerMethod.ReturnType);
            }
            var exception = il.DeclareLocal(typeof(Exception));

            Label @try = il.BeginExceptionBlock();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, instanceField);
            for(int i = 0; i < paramTypes.Length; ++i)
            {
                il.Emit(OpCodes.Ldarg, 1 + i);
            }

            il.EmitCall(OpCodes.Call, innerMethod, null);
            if(retval != null)
            {
                il.Emit(OpCodes.Stloc, retval);
            }
            il.Emit(OpCodes.Leave, @try);
            il.BeginCatchBlock(typeof(Exception));
            il.Emit(OpCodes.Stloc, exception); // We need this local because MSIL doesn't have an instruction
            il.Emit(OpCodes.Ldarg_0);          // that swaps the top two values on the stack.
            il.Emit(OpCodes.Ldfld, exceptionKeeperField);
            il.Emit(OpCodes.Ldloc, exception);
            il.EmitCall(OpCodes.Call, addException, null);
            if(nativeUnwind != null)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, instanceField);
                il.EmitCall(OpCodes.Call, nativeUnwind, null);
            }
            else
            {
                var typeName = classToBind.GetType().FullName;
                il.EmitWriteLine($"Export to type '{typeName}' which is not unwindable threw an exception.");

                // Print exceptions.
                var printExceptions = typeof(ExceptionKeeper).GetMethod(nameof(ExceptionKeeper.PrintExceptions));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, exceptionKeeperField);
                il.EmitCall(OpCodes.Call, printExceptions, null); // call ExceptionKeeper.PrintExceptions

                il.Emit(OpCodes.Ldc_I4_1);
                il.EmitCall(OpCodes.Call, typeof(Environment).GetMethod(nameof(Environment.Exit)), null);
            }
            il.EndExceptionBlock();
            if(retval != null)
            {
                il.Emit(OpCodes.Ldloc, retval);
            }
            il.Emit(OpCodes.Ret);

            return attacheeWrapper.CreateDelegate(delegateType, wrappersObj);
        }

        private void ResolveCallsToManaged()
        {
            classToBind.NoisyLog("Binding native -> managed calls.");
            var symbols = SharedLibraries.GetAllSymbols(libraryFileName);
            var classMethods = classToBind.GetType().GetAllMethods().ToArray();
            var exportedMethods = new List<MethodInfo>();
            foreach(var originalCandidate in symbols.Where(x => x.Contains("renode_external_attach")))
            {
                var candidate  = FilterCppName(originalCandidate);
                var parts = candidate.Split(new [] { "__" }, StringSplitOptions.RemoveEmptyEntries);
                var cName = parts[2];
                var expectedTypeName = parts[1];
                var csName = cName.StartsWith('$') ? GetCSharpName(cName.Substring(1)) : cName;
                classToBind.NoisyLog("(NativeBinder) Binding {0} as {2} of type {1}.", cName, expectedTypeName, csName);
                // let's find the desired method
                var desiredMethodInfo = classMethods.FirstOrDefault(x => x.Name == csName);
                if(desiredMethodInfo == null)
                {
                    throw new InvalidOperationException(string.Format("Could not find method {0} in a class {1}.",
                                                                      csName, classToBind.GetType().Name));
                }
                if(!desiredMethodInfo.IsDefined(typeof(ExportAttribute), true))
                {
                    throw new InvalidOperationException(
                        string.Format("Method {0} is exported as {1} but it is not marked with the Export attribute.",
                                  desiredMethodInfo.Name, cName));
                }
                var parameterTypes = desiredMethodInfo.GetParameters().Select(p => p.ParameterType).ToList();
                var actualTypeName = ShortTypeNameFromParamsAndReturn(parameterTypes, desiredMethodInfo.ReturnType);
                if(expectedTypeName != actualTypeName)
                {
                    throw new InvalidOperationException($"Method {cName} has type {actualTypeName} but the native library expects {expectedTypeName}");
                }
                exportedMethods.Add(desiredMethodInfo);
                // let's make the delegate instance
                var delegateType = DelegateTypeFromParamsAndReturn(parameterTypes, desiredMethodInfo.ReturnType);
                Delegate attachee;
                try
                {
                    attachee = WrapExport(delegateType, desiredMethodInfo);
                }
                catch(ArgumentException e)
                {
                    throw new InvalidOperationException($"Could not resolve call to managed: {e.Message}. Candidate is '{candidate}', desired method is '{desiredMethodInfo.ToString()}'");
                }

#if !PLATFORM_WINDOWS && !NET
                // according to https://blogs.msdn.microsoft.com/cbrumme/2003/05/06/asynchronous-operations-pinning/,
                // pinning is wrong (and it does not work on windows too)...
                // but both on linux & osx it seems to be essential to avoid delegates from being relocated
                handles = handles.Union(new [] { GCHandle.Alloc(attachee, GCHandleType.Pinned) }).ToArray();
#endif
                delegateStore = delegateStore.Union(new [] { attachee }).ToArray();
                // let's make the attaching function delegate
                var attacherType = DelegateTypeFromParamsAndReturn(new [] { delegateType }, typeof(void), $"Attach{delegateType.Name}");
                var address = SharedLibraries.GetSymbolAddress(libraryAddress, originalCandidate);
                var attacher = Marshal.GetDelegateForFunctionPointer(address, attacherType);
                // and invoke it
                attacher.FastDynamicInvoke(attachee);
            }
            // check that all exported methods were really exported and issue a warning if not
            var notExportedMethods = classMethods.Where(x => x.IsDefined(typeof(ExportAttribute), true)).Except(exportedMethods);
            foreach(var method in notExportedMethods)
            {
                classToBind.Log(LogLevel.Warning, "Method {0} is marked with Export attribute, but was not exported.", method.Name);
            }
        }

        private static string ShortTypeNameFromParamsAndReturn(IEnumerable<Type> parameterTypes, Type returnType)
        {
            var baseName = returnType == typeof(void) ? "Action" : "Func" + returnType.Name;
            var paramNames = parameterTypes.Select(p => p.Name);
            return string.Join("", paramNames.Prepend(baseName)).Replace("[]", "Array");
        }

        private static string GetCName(string name)
        {
            var lastCapitalChar = 0;
            var cName = name.GroupBy(x =>
            {
                if (char.IsUpper(x))
                {
                    lastCapitalChar++;
                }
                return lastCapitalChar;
            }).Select(x => x.Aggregate(string.Empty, (y, z) => y + char.ToLower(z))).
                Aggregate((x, y) => x + "_" + y);

            return cName;
        }

        private static string GetWrappedName(string name, bool useExceptionWrapper)
        {
            if(useExceptionWrapper)
            {
                return name + "_ex"; // Bind to exception wrapper instead of inner function
            }

            return name;
        }

        private static string GetCSharpName(string name)
        {
            var words = name.Split('_');
            return words.Select(x => FirstLetterUpper(x)).Aggregate((x, y) => x + y);
        }

        private static string FirstLetterUpper(string str)
        {
            return str.Substring(0, 1).ToUpper() + str.Substring(1);
        }

        private static string FilterCppName(string name)
        {
            var result = Symbol.DemangleSymbol(name).Split('(')[0].ToString();
            if(result.StartsWith("_"))
            {
                return result.Skip(4).ToString();
            }
            return result;
        }

        private static Type GetUnderlyingType(Type type)
        {
            return type.IsEnum ? type.GetEnumUnderlyingType() : type;
        }

        // This method and the constants used in it are inspired by MakeNewCustomDelegate from .NET itself.
        // See https://github.com/dotnet/runtime/blob/8ca896c3f5ef8eb1317439178bf041b5f270f351/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/Compiler/DelegateHelpers.cs#L110
        private static Type DelegateTypeFromParamsAndReturn(IEnumerable<Type> parameterTypes, Type returnType, string name = null)
        {
            // Convert enums to their underlying types to avoid creating needless additional delegate types
            returnType = GetUnderlyingType(returnType);
            parameterTypes = parameterTypes.Select(GetUnderlyingType);

            if(name == null)
            {
                // The default naming mirrors the generic delegate types, but for functions, the return type comes first
                // For example, Func<Int32, UInt64> becomes FuncUInt64Int32.
                name = ShortTypeNameFromParamsAndReturn(parameterTypes, returnType);
            }

            var delegateTypeName = $"NativeBinder.Delegates.{name}";

            lock(moduleBuilder)
            {
                var delegateType = moduleBuilder.GetType(delegateTypeName);

                if(delegateType == null)
                {
                    var delegateTypeAttributes = TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed |
                        TypeAttributes.AnsiClass | TypeAttributes.AutoClass;
                    var delegateConstructorAttributes = MethodAttributes.RTSpecialName | MethodAttributes.HideBySig |
                        MethodAttributes.Public;
                    var delegateInvokeAttributes = MethodAttributes.Public | MethodAttributes.HideBySig |
                        MethodAttributes.NewSlot | MethodAttributes.Virtual;
                    var delegateMethodImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;

                    var typeBuilder = moduleBuilder.DefineType(delegateTypeName, delegateTypeAttributes, typeof(MulticastDelegate));

                    var constructor = typeBuilder.DefineConstructor(delegateConstructorAttributes,
                        CallingConventions.Standard, new [] { typeof(object), typeof(IntPtr) });
                    constructor.SetImplementationFlags(delegateMethodImplAttributes);

                    var invokeMethod = typeBuilder.DefineMethod("Invoke", delegateInvokeAttributes, returnType, parameterTypes.ToArray());
                    invokeMethod.SetImplementationFlags(delegateMethodImplAttributes);

                    // Add the [UnmanagedFunctionPointer] attribute with Cdecl specified
                    var unmanagedFnAttrCtor = typeof(UnmanagedFunctionPointerAttribute).GetConstructor(new [] { typeof(CallingConvention) });
                    var unmanagedFnAttrBuilder = new CustomAttributeBuilder(unmanagedFnAttrCtor, new object[] { CallingConvention.Cdecl });
                    typeBuilder.SetCustomAttribute(unmanagedFnAttrBuilder);

                    delegateType = typeBuilder.CreateType();
                }

                return delegateType;
            }
        }

        private static readonly ModuleBuilder moduleBuilder;

        private IntPtr libraryAddress;
        private string libraryFileName;
        private IEmulationElement classToBind;
        private Type wrappersType;
        private FieldInfo instanceField;
        private FieldInfo exceptionKeeperField;
        private object wrappersObj;

        // the point of delegate store is to hold references to delegates
        // which would otherwise be garbage collected while native calls
        // can still use them
        private object[] delegateStore;
#if !PLATFORM_WINDOWS && !NET
        private GCHandle[] handles;
#endif
    }
}

