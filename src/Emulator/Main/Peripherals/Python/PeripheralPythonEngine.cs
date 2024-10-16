//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Microsoft.Scripting.Hosting;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Migrant.Hooks;
using Antmicro.Migrant;
using System.Linq;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Python
{
    public class PeripheralPythonEngine : PythonEngine
    {
        private readonly static string[] Imports =
        {
            "from Antmicro.Renode.Logging import Logger as logger",
            "from Antmicro.Renode.Logging import LogLevel",
        };

        protected override string[] ReservedVariables
        {
            get { return base.ReservedVariables.Union(PeripheralPythonEngine.InnerReservedVariables).ToArray(); }
        }

        private readonly static string[] InnerReservedVariables =
        {
            "request",
            "self",
            "size",
            "logger",
            "LogLevel"
        };

        private void InitScope(ScriptSource script)
        {
            Request = new PythonRequest();

            Scope.SetVariable("request", Request);
            Scope.SetVariable("self", peripheral);
            Scope.SetVariable("size", peripheral.Size);

            source = script;
            code = Compile(source);
        }

        public PeripheralPythonEngine(PythonPeripheral peripheral)
        {
            this.peripheral = peripheral;
            InitScope(Engine.CreateScriptSourceFromString(Aggregate(Imports)));
        }

        public PeripheralPythonEngine(PythonPeripheral peripheral, Func<ScriptEngine, ScriptSource> sourceGenerator)
        {
            this.peripheral = peripheral;
            InitScope(sourceGenerator(Engine));
        }

        public string Code
        {
            get
            {
                return source.GetCode();
            }
        }

        public void ExecuteCode()
        {
            Execute(code);
        }

        [Transient]
        private ScriptSource source;

        [Transient]
        private CompiledCode code;

        protected override void Init()
        {
            base.Init();
            InitScope(Engine.CreateScriptSourceFromString(codeContent));
            codeContent = null;
        }

        #region Serialization

        [PreSerialization]
        protected void BeforeSerialization()
        {
            codeContent = Code;
        }

        [PostSerialization]
        private void AfterDeSerialization()
        {
            codeContent = null;
        }

        private string codeContent;

        #endregion

        [Transient]
        private PythonRequest request;

        public PythonRequest Request
        {
            get
            {
                return request;
            }
            private set
            {
                request = value;
            }
        }

        private readonly PythonPeripheral peripheral;

        // naming convention here is pythonic
        public class PythonRequest
        {
            public ulong value { get; set; }
            public byte length { get; set; }
            public RequestType type { get; set; }
            public long offset { get; set; }
            public ulong absolute { get; set; }
            public ulong counter { get; set; }

            public bool isInit
            {
                get
                {
                    return type == RequestType.INIT;
                }
            }

            public bool isRead
            {
                get
                {
                    return type == RequestType.READ;
                }
            }

            public bool isWrite
            {
                get
                {
                    return type == RequestType.WRITE;
                }
            }

            public bool isUser
            {
                get
                {
                    return type == RequestType.USER;
                }
            }

            public enum RequestType
            {
                READ,
                WRITE,
                INIT,
                USER
            }
        }
    }
}
