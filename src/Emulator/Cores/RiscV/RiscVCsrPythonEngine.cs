//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

using Microsoft.Scripting.Hosting;

namespace Antmicro.Renode.Hooks
{
    public sealed class RiscVCsrPythonEngine : PythonEngine
    {
        public RiscVCsrPythonEngine(BaseRiscV cpu, ulong csr, bool initable, string script = null, OptionalReadFilePath path = null)
        {
            if((script == null && path == null) || (script != null && path != null))
            {
                throw new ConstructionException("Parameters 'script' and 'path' cannot be both set or both unset");
            }

            this.cpu = cpu;
            this.csr = csr;

            this.script = script;
            this.path = path;

            this.initable = initable;

            InnerInit();

            CsrWriteHook = (value) =>
            {
                TryInit();

                request.Value = value;
                request.Type = CsrRequest.RequestType.WRITE;

                Execute(code, error =>
                {
                    this.cpu.Log(LogLevel.Error, "Python runtime error: {0}", error);
                    throw new CpuAbortException($"Python runtime error: {error}");
                });
            };

            CsrReadHook = () =>
            {
                TryInit();

                request.Type = CsrRequest.RequestType.READ;
                Execute(code, error =>
                {
                    this.cpu.Log(LogLevel.Error, "Python runtime error: {0}", error);
                    throw new CpuAbortException($"Python runtime error: {error}");
                });

                return request.Value;
            };
        }

        public Action<ulong> CsrWriteHook { get; }

        public Func<ulong> CsrReadHook { get; }

        [PostDeserialization]
        private void InnerInit()
        {
            request = new CsrRequest();
            request.Csr = this.csr;

            Scope.SetVariable("cpu", cpu);
            Scope.SetVariable("machine", cpu.GetMachine());
            Scope.SetVariable("request", request);

            ScriptSource source;

            if(script != null)
            {
                source = Engine.CreateScriptSourceFromString(script);
            }
            else
            {
                if(!File.Exists(path))
                {
                    throw new RecoverableException($"Couldn't find the script file: {path}");
                }
                source = Engine.CreateScriptSourceFromFile(path);
            }

            code = Compile(source);
        }

        private void TryInit()
        {
            if(!initable || isInitialized)
            {
                return;
            }

            request.Type = CsrRequest.RequestType.INIT;
            Execute(code);
            isInitialized = true;
        }

        [Transient]
        private CompiledCode code;

        private bool isInitialized;

        private CsrRequest request;

        private readonly string script;
        private readonly string path;

        private readonly ICPU cpu;
        private readonly ulong csr;
        private readonly bool initable;

        // naming convention here is pythonic
        public class CsrRequest
        {
            public ulong Csr { get; set; }

            public ulong Value { get; set; }

            public RequestType Type { get; set; }

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

            public enum RequestType
            {
                READ,
                WRITE,
                INIT
            }
        }
    }
}