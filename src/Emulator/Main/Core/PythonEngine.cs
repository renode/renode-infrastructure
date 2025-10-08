//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Exceptions;

using IronPython.Hosting;
using IronPython.Modules;
using IronPython.Runtime;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Core
{
    public abstract class PythonEngine
    {
        #region Python Engine

        private static readonly ScriptEngine _Engine = Python.CreateEngine();

        #endregion

        #region Helper methods

        protected static string Aggregate(string[] array)
        {
            return array.Aggregate((prev, curr) => string.Format("{0}{1}{2}", prev, Environment.NewLine, curr));
        }

        protected PythonEngine()
        {
            InnerInit();
        }

        protected virtual void Init()
        {
            InnerInit();
        }

        protected CompiledCode Compile(ScriptSource source)
        {
            return Compile(source, error => throw new RecoverableException(error));
        }

        protected CompiledCode Compile(ScriptSource source, Action<string> errorCallback)
        {
            return source.Compile(new ErrorHandler(errorCallback));
        }

        protected void Execute(CompiledCode code)
        {
            Execute(code, error => throw new RecoverableException($"Python runtime error: {error}"));
        }

        protected void Execute(CompiledCode code, Action<string> errorCallback)
        {
            try
            {
                // code can be null when compiled SourceScript had syntax errors
                code?.Execute(Scope);
            }
            catch(Exception e)
            {
                if(e is UnboundNameException || e is MissingMemberException || e is ArithmeticException)
                {
                    errorCallback?.Invoke(e.Message);
                }
                else
                {
                    throw;
                }
            }
        }

        protected virtual string[] ReservedVariables
        {
            get
            {
                return new[]
                {
                    "__doc__",
                    "__builtins__",
                    "Antmicro",
                    "System",
                    "cpu",
                    "clr",
                    "sysbus",
                    "time",
                    "__file__",
                    "__name__",
                    "sys",
                    "LogLevel",
                    "emulationManager",
                    "pythonEngine",
                };
            }
        }

        protected ScriptEngine Engine { get { return PythonEngine._Engine; } }

        #endregion

        [Transient]
        protected ScriptScope Scope;

        private void InnerInit()
        {
            Scope = Engine.CreateScope();
            Scope.SetVariable("emulationManager", EmulationManager.Instance);
            Scope.SetVariable("pythonEngine", Engine);
            // `Monitor` is located in a different project, so we cannot reference the class directly
            var monitor = ObjectCreator.Instance.GetSurrogate(MonitorTypeName);
            if(monitor != null)
            {
                Scope.SetVariable("monitor", monitor);
            }
            PythonTime.localtime();

            var imports = Engine.CreateScriptSourceFromString(Aggregate(Imports));
            imports.Execute(Scope);
        }

        #region Serialization

        [PreSerialization]
        private void BeforeSerialization()
        {
            var variablesToSerialize = Scope.GetVariableNames().Except(ReservedVariables);
            variables = new Dictionary<string, object>();
            foreach(var variable in variablesToSerialize)
            {
                var value = Scope.GetVariable<object>(variable);
                if(value.GetType() == typeof(PythonModule))
                {
                    continue;
                }
                if(value.GetType().FullName == MonitorTypeName)
                {
                    continue;
                }
                variables[variable] = value;
            }
        }

        [PostSerialization]
        private void AfterSerialization()
        {
            variables = null;
        }

        [PostDeserialization]
        private void AfterDeserialization()
        {
            Init();
            foreach(var variable in variables)
            {
                Scope.SetVariable(variable.Key, variable.Value);
            }
            variables = null;
        }

        private Dictionary<string, object> variables;

        private readonly string[] Imports =
        {
            "import clr",
            "clr.AddReference('Infrastructure')",
            "clr.AddReference('Renode')",
        #if NET
            "clr.AddReference('System.Console')", // It was moved to separate assembly on .NET Core.
        #else
            "clr.AddReference('IronPython.StdLib')", // It is referenced by default on NET Core, but not on mono.
        #endif
            "import Antmicro.Renode",
            "import System",
            "import time",
            "import sys",
            "import Antmicro.Renode.Logging.Logger",
            "clr.ImportExtensions(Antmicro.Renode.Logging.Logger)",
            "clr.ImportExtensions(Antmicro.Renode.Peripherals.IPeripheralExtensions)",
            "import Antmicro.Renode.Peripherals.CPU.ICPUWithRegistersExtensions",
            "clr.ImportExtensions(Antmicro.Renode.Peripherals.CPU.ICPUWithRegistersExtensions)",
            "import Antmicro.Renode.Core.Extensions.FileLoaderExtensions",
            "clr.ImportExtensions(Antmicro.Renode.Core.Extensions.FileLoaderExtensions)",
            "import Antmicro.Renode.Logging.LogLevel as LogLevel",
            "clr.ImportExtensions(Antmicro.Renode.Peripherals.Bus.BusControllerExtensions)",
        };

        private const string MonitorTypeName = "Antmicro.Renode.UserInterface.Monitor";

        #endregion

        public class ErrorHandler : ErrorListener
        {
            public ErrorHandler(Action<string> errorCallback)
            {
                this.errorCallback = errorCallback;
            }

            public override void ErrorReported(ScriptSource source, string message, SourceSpan span, int errorCode, Severity severity)
            {
                errorCallback?.Invoke($"[{severity}] {message} (Line {span.Start.Line}, Column {span.Start.Column})");
            }

            private readonly Action<string> errorCallback;
        }
    }
}