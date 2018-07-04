//
// Copyright (c) 2010-2018 Antmicro
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
using System.Collections.Generic;
using Dynamitey;

namespace Antmicro.Renode.Utilities.Binding
{
    /// <summary>
    /// The <c>NativeBinder</c> class lets one bind managed delegates from given class to functions
    /// of a given native library and vice versa.
    /// </summary>
    public sealed class NativeBinder : IDisposable
    {
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
#if !PLATFORM_WINDOWS
            handles = new GCHandle[0];
#endif
            this.classToBind = classToBind;
            libraryAddress = SharedLibraries.LoadLibrary(libraryFile);
            libraryFileName = libraryFile;
            try
            {
                ResolveCallsToNative();
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
#if !PLATFORM_WINDOWS
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

        private void ResolveCallsToNative()
        {
            classToBind.NoisyLog("Binding managed -> native calls.");
            var fields = classToBind.GetType().GetAllFields().Where(x => x.IsDefined(typeof(ImportAttribute), false)).ToList(); // TODO: const it
            var staticContext = InvokeContext.CreateStatic;
            foreach (var field in fields)
            {
                var attribute = (ImportAttribute)field.GetCustomAttributes(false).First(x => x is ImportAttribute);
                var cName = attribute.Name ?? GetCName(field.Name);
                classToBind.NoisyLog(string.Format("(NativeBinder) Binding {1} as {0}.", field.Name, cName));
                var address = SharedLibraries.GetSymbolAddress(libraryAddress, cName);
                var result = Dynamic.InvokeMember(staticContext(typeof(Marshal)), "GetDelegateForFunctionPointer", address, field.FieldType);
                field.SetValue(classToBind, result);
            }
        }

        private void ResolveCallsToManaged()
        {
            classToBind.NoisyLog("Binding native -> managed calls.");
            var symbols = SharedLibraries.GetAllSymbols(libraryFileName).ToArray();
            var classMethods = classToBind.GetType().GetAllMethods().ToArray();
            var exportedMethods = new List<MethodInfo>();
            foreach(var candidate in symbols.Where(x => x.StartsWith("renode_external_attach")))
            {
                var parts = candidate.Split(new [] { "__" }, StringSplitOptions.RemoveEmptyEntries);
                var cName = parts[2];
                var shortName = parts[1];
                var csName = cName.StartsWith('$') ? GetCSharpName(cName.Substring(1)) : cName;
                classToBind.NoisyLog("(NativeBinder) Binding {0} as {2} of type {1}.", cName, shortName, csName);
                var delegateType = TypeFromShortTypeName(shortName);
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
                exportedMethods.Add(desiredMethodInfo);
                // let's make the delegate instance
                Delegate attachee;
                try
                {
                    attachee = Delegate.CreateDelegate(delegateType, classToBind, desiredMethodInfo);
                }
                catch(ArgumentException e)
                {
                    throw new InvalidOperationException($"Could not resolve call to managed: {e.Message}. Candidate is '{candidate}', desired method is '{desiredMethodInfo.ToString()}'");
                }

#if !PLATFORM_WINDOWS
                // according to https://blogs.msdn.microsoft.com/cbrumme/2003/05/06/asynchronous-operations-pinning/,
                // pinning is wrong (and it does not work on windows too)...
                // but both on linux & osx it seems to be essential to avoid delegates from being relocated
                handles = handles.Union(new [] { GCHandle.Alloc(attachee, GCHandleType.Pinned) }).ToArray();
#endif
                delegateStore = delegateStore.Union(new [] { attachee }).ToArray();
                // let's make the attaching function delegate
                var attacherType = TypeFromShortTypeName(string.Format("Attach{0}", shortName));
                var address = SharedLibraries.GetSymbolAddress(libraryAddress, candidate);
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

        private static Type TypeFromShortTypeName(string shortName)
        {
            if(shortName == "Action")
            {
                return typeof(Action);
            }
            var fullName = string.Format("{0}.{1}", typeof(NativeBinder).Namespace.ToString(), shortName);
            var result = Type.GetType(fullName);
            if(result == null)
            {
                throw new InvalidOperationException(string.Format("Could not find type {0}.", shortName));
            }
            return result;
        }

        private static string GetCName(string name)
        {
            var lastCapitalChar = 0;
            return name.GroupBy(x =>
            {
                if (char.IsUpper(x))
                {
                    lastCapitalChar++;
                }
                return lastCapitalChar;
            }).Select(x => x.Aggregate(string.Empty, (y, z) => y + char.ToLower(z))).
                Aggregate((x, y) => x + "_" + y);
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

        private IntPtr libraryAddress;
        private string libraryFileName;
        private IEmulationElement classToBind;

        // the point of delegate store is to hold references to delegates
        // which would otherwise be garbage collected while native calls
        // can still use them
        private object[] delegateStore;
#if !PLATFORM_WINDOWS
        private GCHandle[] handles;
#endif
    }
}

