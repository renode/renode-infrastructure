//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.IO;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.TAPHelper
{
    public class DynamicModuleSpawner
    {
        public static string GetTAPHelper()
        {
            var generatedFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
            var dirName = Path.GetDirectoryName(generatedFileName);
            var filName = Path.GetFileName(generatedFileName);

            // Copy Infrastructure.dll to temp directory
            var currentAssemblyPath = Assembly.GetExecutingAssembly().CodeBase.Substring(7);
            var targetPath = Path.Combine(TemporaryFilesManager.Instance.EmulatorTemporaryPath, "Infrastructure.dll");
            File.Copy(currentAssemblyPath, targetPath, true);

            // Generate binary
            GenerateTAPHelper(dirName, filName, currentAssemblyPath);

            return generatedFileName;
        }

        private DynamicModuleSpawner()
        {
        }

        private static void GenerateTAPHelper(string path, string filename, string extensionsAssemblyPath)
        {
            var assembly = new AssemblyName { Name = "TAPHelperAssembly" };
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assembly, AssemblyBuilderAccess.Save, path);

            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assembly.Name, filename);
            var typeBuilder = moduleBuilder.DefineType("TAPHelper", TypeAttributes.Public|TypeAttributes.Class);
            var mainMethodBuilder = typeBuilder.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static, typeof(int), new Type[] { typeof(string[]) });
            
            //
            // Main method
            //
            var generator = mainMethodBuilder.GetILGenerator();
            var intptrLocale = generator.DeclareLocal(typeof(IntPtr));
            var setReturnFail = generator.DefineLabel();
            var freeMemoryAndFinish = generator.DefineLabel();

            // load the proper assembly
            generator.Emit(OpCodes.Ldtoken, typeof(Func<IntPtr, bool, int>));
            generator.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            generator.Emit(OpCodes.Ldstr, extensionsAssemblyPath);
            generator.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new [] { typeof(string) }));
            generator.Emit(OpCodes.Ldstr, "Antmicro.Renode.TAPHelper.TAPTools");
            generator.Emit(OpCodes.Callvirt, typeof(Assembly).GetMethod("GetType", new [] { typeof(string) }));
            generator.Emit(OpCodes.Ldstr, "OpenTAP");
            generator.Emit(OpCodes.Call, typeof(Delegate).GetMethod("CreateDelegate", new [] { typeof(Type), typeof(Type), typeof(string) }));
            generator.Emit(OpCodes.Castclass, typeof(Func<IntPtr, bool, int>));

            // push device name on stack
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldc_I4, 0);
            generator.Emit(OpCodes.Ldelem, typeof(string));
            generator.Emit(OpCodes.Call, typeof(Marshal).GetMethod("StringToCoTaskMemAuto"));
            generator.Emit(OpCodes.Dup);
            generator.Emit(OpCodes.Stloc, intptrLocale);

            // push 'persistant' flag on stack
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldc_I4, 1);
            generator.Emit(OpCodes.Ldelem, typeof(string));
            generator.Emit(OpCodes.Call, typeof(bool).GetMethod("Parse", new [] { typeof(string) }));

            // call OpenTAP method
            generator.Emit(OpCodes.Callvirt, typeof(Func<IntPtr, bool, int>).GetMethod("Invoke", new [] { typeof(IntPtr), typeof(bool) }));

            generator.Emit(OpCodes.Ldc_I4, 0);
            generator.Emit(OpCodes.Blt, setReturnFail);
            generator.Emit(OpCodes.Ldc_I4, 0);
            generator.Emit(OpCodes.Br, freeMemoryAndFinish);

            generator.MarkLabel(setReturnFail);
            /*
            generator.Emit(OpCodes.Ldtoken, typeof(Func<int>));
            generator.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
            generator.Emit(OpCodes.Ldstr, extensionsAssemblyPath);
            generator.Emit(OpCodes.Call, typeof(Assembly).GetMethod("LoadFrom", new [] { typeof(string) }));
            generator.Emit(OpCodes.Ldstr, "Antmicro.Renode.TAPHelper.LibC");
            generator.Emit(OpCodes.Callvirt, typeof(Assembly).GetMethod("GetType", new [] { typeof(string) }));
            generator.Emit(OpCodes.Ldstr, "GetLastError");
            generator.Emit(OpCodes.Call, typeof(Delegate).GetMethod("CreateDelegate", new [] { typeof(Type), typeof(Type), typeof(string) }));
            generator.Emit(OpCodes.Castclass, typeof(Func<int>));
            generator.Emit(OpCodes.Callvirt, typeof(Func<int>).GetMethod("Invoke", Type.EmptyTypes));
            */
            generator.Emit(OpCodes.Ldc_I4, 1);

            // free memory
            generator.MarkLabel(freeMemoryAndFinish);
            generator.Emit(OpCodes.Ldloc, intptrLocale);
            generator.Emit(OpCodes.Call, typeof(Marshal).GetMethod("FreeCoTaskMem"));
            
            generator.Emit(OpCodes.Ret);

            typeBuilder.CreateType();

            // Set the entrypoint (thereby declaring it an EXE)
            assemblyBuilder.SetEntryPoint(mainMethodBuilder,PEFileKinds.ConsoleApplication);
            assemblyBuilder.Save(filename);
        }
    }
}

