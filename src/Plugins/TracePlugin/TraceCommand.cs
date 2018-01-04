//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.UserInterface.Commands;
using Antmicro.Renode.UserInterface;
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Debug;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using System.Linq;
using Antmicro.Renode.Plugins.TracePlugin.Handlers;
using Dynamitey;

namespace Antmicro.Renode.Plugins.TracePlugin
{
    public class TraceCommand : Command
    {

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine("- to trace only function call with a registered handler:");
            writer.WriteLine("{0} {1} cpuName \"functionName\"".FormatWith(Name, TraceEnableCommand));
            writer.WriteLine();
            writer.WriteLine("- to trace function call and returned value with a registered handler:");
            writer.WriteLine("{0} {1} cpuName \"functionName\" true".FormatWith(Name, TraceEnableCommand));
            writer.WriteLine();
            writer.WriteLine("- to trace function call without a handler, with or without return value:");
            writer.WriteLine("{0} {1} cpuName \"functionName\" [true|false] [number of parameters]".FormatWith(Name, TraceEnableCommand));
            writer.WriteLine();
            writer.WriteLine("- to trace function call without a handler, with or without return value, with specified variable types:");
            writer.WriteLine("{0} {1} cpuName \"functionName\" [true|false] [list of parameter types]".FormatWith(Name, TraceEnableCommand));
            writer.WriteLine("(Note, that if return value is expected, the last parameter type must relate to return value.");
            writer.WriteLine();
            writer.WriteLine("- to disable tracing of a function:");
            writer.WriteLine("{0} {1} cpuName \"functionName\"".FormatWith(Name, TraceDisableCommand));
            writer.WriteLine();
            writer.WriteLine("Handlers available for functions:");
            writer.WriteLine(handlers.Keys.Select(x => "- " + x).Stringify("\r\n"));
            writer.WriteLine();
            writer.WriteLine("Possible values for parameter types:");
            writer.WriteLine(Enum.GetNames(typeof(FunctionCallParameterType)).Where(x => !x.Contains("Array")).Select(x => "- " + x).Stringify("\r\n"));
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(TraceEnableCommand, TraceDisableCommand)] LiteralToken enable, LiteralToken cpuToken, StringToken functionName)
        {
            if(enable.Value == TraceEnableCommand)
            {
                Execute(writer, cpuToken, functionName.Value, false, null);
            }
            else
            {
                var cpu = (Arm)monitor.ConvertValueOrThrowRecoverable(cpuToken.Value, typeof(Arm));
                var cpuTracer = EnsureTracer(cpu);
                cpuTracer.RemoveTracing(functionName.Value);
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(TraceEnableCommand)] LiteralToken enable, LiteralToken cpuToken, StringToken functionName, BooleanToken traceReturn)
        {
            Execute(writer, cpuToken, functionName.Value, traceReturn.Value, null);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(TraceEnableCommand)] LiteralToken enable, LiteralToken cpuToken, StringToken functionName, BooleanToken traceReturn, DecimalIntegerToken numberOfParameters)
        {
            Execute(writer, cpuToken, functionName.Value, traceReturn.Value, (int)numberOfParameters.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(TraceEnableCommand)] LiteralToken enable, LiteralToken cpuToken, StringToken functionName, BooleanToken traceReturn, params LiteralToken[] types)
        {
            var cpu = (Arm)monitor.ConvertValueOrThrowRecoverable(cpuToken.Value, typeof(Arm));
            var cpuTracer = EnsureTracer(cpu);
            var handler = new DefaultFunctionHandler(cpu);
            var paramList = new List<FunctionCallParameter>();
            foreach(var parameter in types)
            {
                FunctionCallParameterType paramType;
                if(!Enum.TryParse(parameter.Value, out paramType))
                {
                    throw new RecoverableException("{0} is not a proper parameter type.".FormatWith(parameter.Value));
                }
                paramList.Add(new FunctionCallParameter{ Type = paramType });
            }
            handler.CallParameters = paramList.Take(paramList.Count - (traceReturn.Value ? 1 : 0));
            handler.ReturnParameter = traceReturn.Value ? paramList.Last() : (FunctionCallParameter?)null;
            if(traceReturn.Value)
            {
                cpuTracer.TraceFunction(functionName.Value, handler.CallParameters, handler.CallHandler, handler.ReturnParameter, handler.ReturnHandler);
            }
            else
            {
                cpuTracer.TraceFunction(functionName.Value, handler.CallParameters, handler.CallHandler);
            }
            
        }

        public void RegisterFunctionName(string function, Type callbackType)
        {
            if(handlers.ContainsKey(function))
            {
                throw new RecoverableException("Function \"{0}\" already registered.".FormatWith(function));
            }
            handlers[function] = callbackType;
        }

        public TraceCommand(Monitor monitor) : base(monitor, "trace", "Hooks up watches for some interesting methods.")
        {
           
            handlers = new Dictionary<string, Type> 
            {
                { "printk", typeof(PrintfHandler) },
                { "printf", typeof(PrintfHandler) }
            };
        }

        private string FindTracerName(Arm cpu)
        {
            string cpuName;
            if(!cpu.Bus.Machine.TryGetAnyName(cpu, out cpuName))
            {
                throw new Exception("This should never have happened!");
            }
            return "{0}.{1}-{2}".FormatWith(EmulationManager.Instance.CurrentEmulation[cpu.Bus.Machine], cpuName, TracerName);
        }

        private void Execute(ICommandInteraction writer, LiteralToken cpuToken, String functionName, bool traceReturn, int? numberOfParameters)
        {

            var cpu = (Arm)monitor.ConvertValueOrThrowRecoverable(cpuToken.Value, typeof(Arm));

            var cpuTracer = EnsureTracer(cpu);
            Type handlerType;
            IFunctionHandler handler;
            if(!handlers.TryGetValue(functionName, out handlerType))
            {
                if(numberOfParameters.HasValue)
                {
                    var paramList = new List<FunctionCallParameter>();
                    for(var i = 0; i < numberOfParameters; ++i)
                    {
                        paramList.Add(new FunctionCallParameter{ Type = FunctionCallParameterType.UInt32 });
                    }
                    FunctionCallParameter? returnParameter = null;
                    if(traceReturn)
                    {
                        returnParameter = new FunctionCallParameter{ Type = FunctionCallParameterType.UInt32 };
                    }
                    var defHandler = new DefaultFunctionHandler(cpu);
                    defHandler.CallParameters = paramList;
                    defHandler.ReturnParameter = returnParameter;
                    handler = defHandler;
                }
                else
                {
                    throw new RecoverableException("Handler for {0} not register. You must provide numberOfParameters to use default handler.".FormatWith(functionName));
                }  
            }
            else
            {
                handler = Dynamic.InvokeConstructor(handlerType, cpu);
            }
            if(traceReturn)
            {
                cpuTracer.TraceFunction(functionName, handler.CallParameters, handler.CallHandler, handler.ReturnParameter, handler.ReturnHandler);
            }
            else
            {
                cpuTracer.TraceFunction(functionName, handler.CallParameters, handler.CallHandler);
            }
        }

        private CPUTracer EnsureTracer(Arm cpu)
        {
            CPUTracer tracer;
            var tracerName = FindTracerName(cpu);
            if(!EmulationManager.Instance.CurrentEmulation.TryGetFromBag(tracerName, out tracer))
            {
                cpu.CreateCPUTracer(tracerName);
            }
            if(!EmulationManager.Instance.CurrentEmulation.TryGetFromBag(tracerName, out tracer))
            {
                throw new RecoverableException("Could not initialize CPUTracer.");
            }
            return tracer;
        }

        private readonly Dictionary<String, Type> handlers;

        private const string TracerName = "tracealyzerTracer";
        private const string TraceEnableCommand = "enable";
        private const string TraceDisableCommand = "disable";
    }
}