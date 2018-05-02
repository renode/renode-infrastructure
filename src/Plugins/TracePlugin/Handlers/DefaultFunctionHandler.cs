//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Debug;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Plugins.TracePlugin.Handlers
{
    public class DefaultFunctionHandler : BaseFunctionHandler, IFunctionHandler
    {
        public DefaultFunctionHandler(TranslationCPU cpu) : base(cpu)
        {
        }

        public void CallHandler(TranslationCPU cpu, ulong pc, string functionName, IEnumerable<object> arguments)
        {
            Logger.Log(LogLevel.Debug, "Call {0} @ 0x{1:X} ({2})",functionName, pc, arguments.Stringify(", "));
        }

        public void ReturnHandler(TranslationCPU cpu, ulong pc, string functionName, IEnumerable<object> argument)
        {
            Logger.Log(LogLevel.Debug, "Return from {0} @ 0x{1:X} ({2})",functionName, pc, argument.First());
        }

        public IEnumerable<FunctionCallParameter> CallParameters
        {
            get;
            set;
        }

        public FunctionCallParameter? ReturnParameter
        {
            get;
            set;
        }

    }
}