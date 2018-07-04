//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Debug;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Plugins.TracePlugin.Handlers
{
    public interface IFunctionHandler
    {
        void CallHandler(TranslationCPU cpu, ulong pc, string functionName, IEnumerable<object> arguments);

        void ReturnHandler(TranslationCPU cpu, ulong pc, string functionName, IEnumerable<object> argument);

        IEnumerable<FunctionCallParameter> CallParameters{ get; }

        FunctionCallParameter? ReturnParameter{ get; }
    }

    public class BaseFunctionHandler
    {
        public BaseFunctionHandler(TranslationCPU cpu)
        {
            this.CPU = cpu;
        }

        protected readonly TranslationCPU CPU;
    }
}

