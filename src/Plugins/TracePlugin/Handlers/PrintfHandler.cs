//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Debug;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Plugins.TracePlugin.Handlers
{
    public class PrintfHandler : BaseFunctionHandler, IFunctionHandler
    {
        public PrintfHandler(TranslationCPU cpu) : base(cpu)
        {
        }

        public void CallHandler(TranslationCPU cpu, ulong pc, string functionName, IEnumerable<object> arguments)
        {
            Logger.LogAs(this, LogLevel.Warning, arguments.First().ToString());
        }

        public void ReturnHandler(TranslationCPU cpu, ulong pc, string functionName, IEnumerable<object> argument)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<FunctionCallParameter> CallParameters
        {
            get
            {
                return callParameters;
            }
        }

        public FunctionCallParameter? ReturnParameter
        {
            get
            {
                return null;
            }
        }

        private static readonly IEnumerable<FunctionCallParameter> callParameters = new []{ new FunctionCallParameter{ Type = FunctionCallParameterType.String } };
    }
}

