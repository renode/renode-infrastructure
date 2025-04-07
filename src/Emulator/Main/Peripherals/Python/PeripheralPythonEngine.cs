//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;

using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Peripherals.Python
{
    public class PeripheralPythonEngine : PythonEngine
    {
        private static readonly string[] Imports =
        {
            "from Antmicro.Renode.Logging import Logger as logger",
            "from Antmicro.Renode.Logging import LogLevel",
        };

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

        public void ExecuteCode()
        {
            Execute(code);
        }

        public string Code
        {
            get
            {
                return source.GetCode();
            }
        }

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

        #region Serialization

        [PreSerialization]
        protected void BeforeSerialization()
        {
            codeContent = Code;
        }

        protected override void Init()
        {
            base.Init();
            InitScope(Engine.CreateScriptSourceFromString(codeContent));
            codeContent = null;
        }

        protected override string[] ReservedVariables
        {
            get { return base.ReservedVariables.Union(PeripheralPythonEngine.InnerReservedVariables).ToArray(); }
        }

        private static readonly string[] InnerReservedVariables =
        {
            "request",
            "self",
            "size",
            "logger",
            "LogLevel"
        };

        [PostSerialization]
        private void AfterDeSerialization()
        {
            codeContent = null;
        }

        private void InitScope(ScriptSource script)
        {
            Request = new PythonRequest();

            Scope.SetVariable("request", Request);
            Scope.SetVariable("self", peripheral);
            Scope.SetVariable("size", peripheral.Size);

            source = script;
            code = Compile(source);
        }

        [Transient]
        private ScriptSource source;

        [Transient]
        private CompiledCode code;

        private string codeContent;

        #endregion

        [Transient]
        private PythonRequest request;

        private readonly PythonPeripheral peripheral;

        // naming convention here is pythonic
        public class PythonRequest
        {
            public ulong Value { get; set; }

            public byte Length { get; set; }

            public RequestType Type { get; set; }

            public long Offset { get; set; }

            public ulong Absolute { get; set; }

            public ulong Counter { get; set; }

            public bool IsInit
            {
                get
                {
                    return Type == RequestType.INIT;
                }
            }

            public bool IsRead
            {
                get
                {
                    return Type == RequestType.READ;
                }
            }

            public bool IsWrite
            {
                get
                {
                    return Type == RequestType.WRITE;
                }
            }

            public bool IsUser
            {
                get
                {
                    return Type == RequestType.USER;
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