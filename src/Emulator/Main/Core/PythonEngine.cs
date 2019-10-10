//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Microsoft.Scripting.Hosting;
using IronPython.Hosting;
using IronPython.Modules;
using Antmicro.Migrant.Hooks;
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
            "import Antmicro.Renode",
            "import System",
            "import time",
            "import sys",
            "import Antmicro.Renode.Logging.Logger",
            "clr.ImportExtensions(Antmicro.Renode.Logging.Logger)",
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

        #endregion
    }
}

