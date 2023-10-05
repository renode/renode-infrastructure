//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using System.Text;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Debug
{
    public static class CPUTracerExtensions
    {
        public static void CreateCPUTracer(this Arm cpu, string name)
        {
            EmulationManager.Instance.CurrentEmulation.AddOrUpdateInBag(name, new CPUTracer(cpu));
        }
    }

    public class CPUTracer : IExternal
    {
        public CPUTracer(Arm cpu)
        {
            this.cpu = cpu;
            this.bus = cpu.Bus;
        }

        public void TraceFunction(string name, IEnumerable<FunctionCallParameter> parameters, Action<TranslationCPU, ulong, string, IEnumerable<object>> callback,
            FunctionCallParameter? returnParameter = null, Action<TranslationCPU, ulong, string, IEnumerable<object>> returnCallback = null)
        {
            if(registeredCallbacks.ContainsKey(name))
            {
                throw new RecoverableException("Function {0} is already being traced.".FormatWith(name));
            }
            cpu.Log(LogLevel.Info, "Going to trace function '{0}'.", name);

            Symbol symbol;
            try
            {
                var address = cpu.Bus.GetSymbolAddress(name);
                symbol = cpu.Bus.Lookup.GetSymbolByAddress(address);
            }
            catch(RecoverableException)
            {
                cpu.Log(LogLevel.Warning, "Symbol {0} not found, exiting.", name);
                throw;
            }

            var traceInfo = new TraceInfo();
            traceInfo.Begin = symbol.Start.RawValue;
            traceInfo.BeginCallback = (cpu, pc) => EvaluateTraceCallback(pc, name, parameters, callback);

            cpu.AddHook(traceInfo.Begin, traceInfo.BeginCallback);
            if(returnCallback != null && returnParameter.HasValue)
            {
                traceInfo.HasEnd = true;
                traceInfo.End = symbol.End.RawValue - (symbol.IsThumbSymbol ? 2 : 4UL);
                traceInfo.EndCallback = (cpu, pc) => EvaluateTraceCallback(pc, name, new[] { returnParameter.Value }, returnCallback);
                cpu.Log(LogLevel.Debug, "Address is @ 0x{0:X}, end is @ 0x{1:X}.", traceInfo.Begin, traceInfo.End);
                cpu.AddHook(traceInfo.End, traceInfo.EndCallback);
            }
            else
            {
                cpu.Log(LogLevel.Debug, "Address is @ 0x{0:X}, end is not traced.", traceInfo.Begin);
            }

            registeredCallbacks[name] = traceInfo;
        }

        public void RemoveTracing(string name)
        {
            TraceInfo traceInfo;
            if(registeredCallbacks.TryGetValue(name, out traceInfo))
            {
                cpu.Log(LogLevel.Info, "Removing trace from function '{0}'.", name);
                cpu.RemoveHook(traceInfo.Begin, traceInfo.BeginCallback);
                if(traceInfo.HasEnd)
                {
                    cpu.RemoveHook(traceInfo.End, traceInfo.EndCallback);
                }
            }
            else
            {
                cpu.Log(LogLevel.Warning, "Hook on function {0} not found, not removing.", name);
            }
        }

        private void EvaluateTraceCallback(ulong pc, string name, IEnumerable<FunctionCallParameter> parameters, Action<TranslationCPU, ulong, string, List<object>> callback)
        {
            var regs = new List<object>();
            var paramList = parameters.ToList();
            //works only for 0-4 parameters!
            for(int i = 0; i < Math.Min(paramList.Count, 4); i++)
            {
                regs.Add(TranslateParameter(cpu.R[i], paramList[i]));
            }
            if(paramList.Count > 4)
            {
                var offset = 0u;
                var sp = cpu.SP;
                for(int i = 4; i < paramList.Count; ++i)
                {
                    regs.Add(TranslateParameter(cpu.Bus.ReadDoubleWord(sp + offset), paramList[i]));
                    offset += 4; //does not support longer data types!
                }
            }
            callback(cpu, pc, name, regs);
        }

        private object TranslateParameter(ulong value, FunctionCallParameter parameter)
        {
            var parameterType = parameter.Type;
            var size = parameter.NumberOfElements;
            switch(parameterType)
            {
            case FunctionCallParameterType.Ignore:
                return null;
            case FunctionCallParameterType.Int64:
                return unchecked((long)value);
            case FunctionCallParameterType.UInt64:
                return value;
            case FunctionCallParameterType.Byte:
                return (byte)value;
            case FunctionCallParameterType.Int32:
                return unchecked((int)value);
            case FunctionCallParameterType.UInt32:
                return (uint)value;
            case FunctionCallParameterType.Int16:
                return unchecked((short)value);
            case FunctionCallParameterType.UInt16:
                return (ushort)value;
            case FunctionCallParameterType.String:
                var done = false;
                var resultString = new StringBuilder();
                while(!done)
                {
                    var readBytes = bus.ReadBytes(value, SizeOfStringBatch, context: cpu);
                    for(var i = 0; i < readBytes.Length; ++i)
                    {
                        var currentByte = readBytes[i];
                        if(currentByte == 0)
                        {
                            done = true;
                            break;
                        }
                        if(currentByte >= 32 && currentByte < 127)
                        {
                            resultString.Append(Convert.ToChar(currentByte));
                        }
                    }
                    value += SizeOfStringBatch;
                }
                return resultString.ToString();
            case FunctionCallParameterType.ByteArray:
                return bus.ReadBytes(value, size, context: cpu);
            case FunctionCallParameterType.Int32Array:
            {
                var intResult = new int[size];
                var byteLength = size * sizeof(int);
                Buffer.BlockCopy(bus.ReadBytes(value, byteLength, context: cpu), 0, intResult, 0, byteLength);
                return intResult;
            }
            case FunctionCallParameterType.UInt32Array:
            {
                var uintResult = new uint[size];
                var byteLength = size * sizeof(uint);
                Buffer.BlockCopy(bus.ReadBytes(value, byteLength, context: cpu), 0, uintResult, 0, byteLength);
                return uintResult;
            }
            default:
                throw new ArgumentOutOfRangeException();
            }
        }

        private readonly Dictionary<string, TraceInfo> registeredCallbacks = new Dictionary<string, TraceInfo>();
        private readonly Arm cpu;
        private readonly IBusController bus;

        private const int SizeOfStringBatch = 100;

        private struct TraceInfo
        {
            public ulong Begin;
            public bool HasEnd;
            public ulong End;
            public Action<ICpuSupportingGdb, ulong> BeginCallback;
            public Action<ICpuSupportingGdb, ulong> EndCallback;
        }
    }
}
