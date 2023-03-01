//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ExternalWindowMMU: ExternalMmuBase, IDoubleWordPeripheral, IKnownSize
    {
        public ExternalWindowMMU(ICPUWithExternalMmu cpu, ulong startAddress, ulong windowSize, uint numberOfWindows) : base(cpu, numberOfWindows)
        {
            this.numberOfWindows = numberOfWindows;
            IRQ = new GPIO();
            registers = DefineRegisters();

            cpu.AddHookOnMmuFault((faultAddress, accessType, faultyWindowAddress) =>
            {
                if(faultyWindowAddress != -1 && this.ContainsWindowWithIndex((uint)faultyWindowAddress))
                {
                    this.TriggerInterrupt();
                    throw new CpuAbortException("Mmu fault occured. This must be handled properly");
                }
            });
        }

        public override void Reset()
        {
            registers.Reset();
            base.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void TriggerInterrupt()
        {
            this.Log(LogLevel.Debug, "MMU fault occured. Setting the IRQ");
            IRQ.Set();
        }

        private DoubleWordRegisterCollection DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();

            for(uint i = 0; i < numberOfWindows; i++)
            {
                var index = i;
                registersMap.Add((long)Register.RangeStartBase + index * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"RANGE_START[{index}]", writeCallback: (_, value) =>
                    {
                        SetWindowStart(index, (ulong)value);
                    }, valueProviderCallback: _ =>
                    {
                        return (uint)GetWindowStart(index);
                    }));
                registersMap.Add((long)Register.RangeEndBase + index * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"RANGE_END[{index}]", writeCallback: (_, value) =>
                    {
                        SetWindowEnd(index, (ulong)value);
                    }, valueProviderCallback: _ =>
                    {
                        return (uint)GetWindowEnd(index);
                    }));
                registersMap.Add((long)Register.AddendBase + index * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"ADDEND[{index}]", writeCallback: (_, value) =>
                    {
                        SetWindowAddend(index, (ulong)value);
                    }, valueProviderCallback: _ =>
                    {
                        return (uint)GetWindowAddend(index);
                    }));
                registersMap.Add((long)Register.PrivilegesBase + index * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"PRIVILEGES[{index}]", writeCallback: (_, value) =>
                    {
                        SetWindowPrivileges(index, (uint)value);
                    }, valueProviderCallback: _ =>
                    {
                        return GetWindowPrivileges(index);
                    }));
            }
            return new DoubleWordRegisterCollection(this, registersMap);
        }

        private readonly uint numberOfWindows;
        private readonly DoubleWordRegisterCollection registers;

        private enum Register
        {
            RangeStartBase = 0x0,
            RangeEndBase = 0x400,
            AddendBase = 0x800,
            PrivilegesBase = 0xC00,
        }
    }
}
