//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using IronPython.Hosting;
using IronPython.Modules;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Exceptions;
using System.Collections.Generic;
using System.Linq;
using IronPython.Runtime;
using System;
using Antmicro.Migrant;

namespace Antmicro.Renode.Core
{
    public abstract class PythonEngine
    {
        #region Python Engine

        private static readonly ScriptEngine _Engine = Python.CreateEngine();
        protected ScriptEngine Engine { get { return PythonEngine._Engine; } }

        #endregion

        [Transient]
        protected ScriptScope Scope;

        private readonly string[] Imports =
        {
            "import clr",
            "clr.AddReference('Emulator')",
            "clr.AddReference('Renode')",
            "clr.AddReference('IronPython.StdLib')",
            "import Antmicro.Renode",
            "import System",
            "import time",
            "import sys",
            "import Antmicro.Renode.Logging.Logger",
            "clr.ImportExtensions(Antmicro.Renode.Logging.Logger)",
            "import Antmicro.Renode.Peripherals.CPU.ControllableCPUExtension",
            "clr.ImportExtensions(Antmicro.Renode.Peripherals.CPU.ControllableCPUExtension)",
            "import Antmicro.Renode.Logging.LogLevel as LogLevel"
        };

        protected virtual string[] ReservedVariables
        {
            get
            {
                return new []
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

        protected PythonEngine()
        {
            InnerInit();
        }

        private void InnerInit()
        {
            Scope = Engine.CreateScope();
            Scope.SetVariable("emulationManager", EmulationManager.Instance);
            Scope.SetVariable("pythonEngine", Engine);
            PythonTime.localtime();

            var imports = Engine.CreateScriptSourceFromString(Aggregate(Imports));
            imports.Execute(Scope);
        }

        protected virtual void Init()
        {
            InnerInit();
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

        #endregion

        #region Helper methods

        protected static string Aggregate(string[] array)
        {
            return array.Aggregate((prev, curr) => string.Format("{0}{1}{2}", prev, Environment.NewLine, curr));
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

