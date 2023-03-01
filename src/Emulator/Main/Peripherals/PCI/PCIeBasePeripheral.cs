//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.PCI.BAR;
using Antmicro.Renode.Peripherals.PCI.Capabilities;
using Antmicro.Renode.Utilities;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.PCI
{
    public abstract class PCIeBasePeripheral : IPCIePeripheral
    {
        protected PCIeBasePeripheral(IPCIeRouter parent, HeaderType headerType)
        {
            if(!IsHeaderAcceptable(headerType))
            {
                throw new ConstructionException($"Currently only devices of type {HeaderType.Bridge} or {HeaderType.Endpoint} are supported.");
            }
            this.parent = parent;
            this.HeaderType = headerType;
            this.baseAddressRegisters = new BaseAddressRegister[headerType.MaxNumberOfBARs()];
            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.DeviceAndVendorId, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => VendorId)
                    .WithValueField(16, 16, FieldMode.Read, valueProviderCallback: _ => DeviceId)
                },
                {(long)Registers.StatusAndConfiguration, new DoubleWordRegister(this) //unsupported fields do not have to be implemented. Maybe we should move it to inheriting classes?
                    //First 16 bits: command register. RW. Writing 0 to these fields should effectively disable all accesses but the configuration accesses
                    .WithTaggedFlag("I/O Space", 0)
                    .WithTaggedFlag("Memory Space", 1)
                    .WithTaggedFlag("Bus Master", 2)
                    .WithTaggedFlag("Special Cycles", 3)
                    .WithTaggedFlag("Memory Write and Invalidate Enable", 4)
                    .WithTaggedFlag("VGA Palette Snoop", 5)
                    .WithTaggedFlag("Parity Error Response", 6)
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("SERR# Enable", 8)
                    .WithTaggedFlag("Fast Back-to-Back Enable", 9)
                    .WithTaggedFlag("Interrupt Disable", 10)
                    .WithReservedBits(11, 8)
                    //Second 16 bits: status register. W1C.
                    .WithTaggedFlag("Interrupt Status", 19)
                    .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => capabilities.Any(), name: "Capabilities List")
                    .WithTaggedFlag("66 MHz Capabale", 21)
                    .WithReservedBits(22, 1)
                    .WithTaggedFlag("Fast Back-to-Back capable", 23)
                    .WithTaggedFlag("Master Data Parity Error", 24)
                    .WithTag("DEVSEL Timing", 25, 2)
                    .WithTaggedFlag("Signaled Target Abort", 27)
                    .WithTaggedFlag("Received Target Abort", 28)
                    .WithTaggedFlag("Received Master Abort", 29)
                    .WithTaggedFlag("Signaled System Error", 30)
                    .WithTaggedFlag("Detected Parity Error", 31)
                },
                {(long)Registers.ClassCode, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => RevisionId)
                    .WithValueField(16, 16, FieldMode.Read, valueProviderCallback: _ => ClassCode)
                },
                {(long)Registers.Header, new DoubleWordRegister(this)
                    .WithTag("Cacheline Size", 0, 8)
                    .WithTag("Latency Timer", 8, 8)
                    .WithEnumField(16, 8, FieldMode.Read, valueProviderCallback: (HeaderType _) => headerType)
                    .WithTag("BIST", 24, 8)
                },
                {(long)Registers.Capabilities, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => capabilities.FirstOrDefault().Key)
                },
            };
            registers = new DoubleWordRegisterCollection(this, registerMap);
            AddCapability(0x80, new PCIeCapability(this));
        }

        public virtual uint ConfigurationReadDoubleWord(long offset)
        {
            var value = registers.Read(offset);
            this.Log(LogLevel.Noisy, "Accessing Configuration space, reading from {0} (0x{1:X}), read 0x{2:X}.", (Registers)offset, offset, value);
            return value;
        }

        public virtual void Reset()
        {
            registers.Reset();
        }

        public virtual void ConfigurationWriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Noisy, "Accessing Configuration space, writing to {0} (0x{1:X}), value {2:X}.", (Registers)offset, offset, value);
            registers.Write(offset, value);
        }

        public uint MemoryReadDoubleWord(uint bar, long offset)
        {
            if(bar >= baseAddressRegisters.Length || baseAddressRegisters[bar] == null)
            {
                this.Log(LogLevel.Warning, "Trying to read from unimplemented BAR {0}, offset 0x{1:X}.", bar, offset);
                return 0;
            }
            if(baseAddressRegisters[bar].RequestedSize < offset)
            {
                this.Log(LogLevel.Error, "Trying to read outside the limits of BAR {0}, offset 0x{1:X}. The BAR requested 0x{2:X} bytes. This may indicate a problem in PCIe routing.", bar, offset, baseAddressRegisters[bar].RequestedSize);
                return 0;
            }
            return ReadDoubleWordFromBar(bar, offset);
        }

        public void MemoryWriteDoubleWord(uint bar, long offset, uint value)
        {
            if(bar >= baseAddressRegisters.Length || baseAddressRegisters[bar] == null)
            {
                this.Log(LogLevel.Warning, "Trying to write to unimplemented BAR {0}, offset 0x{1:X}, value 0x{2:X}.", bar, offset, value);
                return;
            }
            if(baseAddressRegisters[bar].RequestedSize < offset)
            {
                this.Log(LogLevel.Error, "Trying to write outside the limits of BAR {0}, offset 0x{1:X}, value 0x{2:X}. The BAR requested 0x{3:X} bytes. This may indicate a problem in PCIe routing.", bar, offset, value, baseAddressRegisters[bar].RequestedSize);
                return;
            }
            WriteDoubleWordToBar(bar, offset, value);
        }

        public ushort DeviceId { get; set; }
        public ushort VendorId { get; set; }
        public byte RevisionId { get; set; }
        public uint ClassCode { get; set; }
        public HeaderType HeaderType { get; }

        protected virtual void WriteDoubleWordToBar(uint bar, long offset, uint value)
        {
            this.Log(LogLevel.Warning, "Unhandled write to BAR {0}, offset 0x{1:X}, value 0x{2:X}.", bar, offset, value);
        }

        protected virtual uint ReadDoubleWordFromBar(uint bar, long offset)
        {
            this.Log(LogLevel.Warning, "Unhandled read from BAR {0}, offset 0x{1:X}.", bar, offset);
            return 0;
        }

        protected void AddBaseAddressRegister(uint i, BaseAddressRegister register)
        {
            if(i > HeaderType.MaxNumberOfBARs())
            {
                throw new ConstructionException($"Cannot add Base address register {i} as it exceeds the maximum amount of these registers for this type of peripheral.");
            }

            if(baseAddressRegisters[i] != null)
            {
                throw new ConstructionException($"Base address register number at {i} is already registered.");
            }
            baseAddressRegisters[i] = register;
            registers.AddRegister((long)Registers.BaseAddressRegister0 + 4 * i, new DoubleWordRegister(this)
                    .WithValueField(0, 32, changeCallback: (_, value) => baseAddressRegisters[i].Value = (uint)value,
                        valueProviderCallback: _ => baseAddressRegisters[i].Value, name: $"BAR{i}"))
                    .WithWriteCallback((_, value) => parent.RegisterBar(new Range(baseAddressRegisters[i].BaseAddress, baseAddressRegisters[i].RequestedSize), this, i));
        }

        protected void AddCapability(byte offset, Capability capability)
        {
            if(offset < CapabilitiesOffset)
            {
                throw new ConstructionException($"Capability offset (0x{offset:X}) is below the minimal address (0x{CapabilitiesOffset:X})");
            }
            if(capabilities.ContainsKey(offset))
            {
                throw new ConstructionException($"Capability already registered at 0x{offset}");
            }
            if(capabilities.Any())
            {
                var lastCapability = capabilities.Last();
                lastCapability.Value.NextCapability = offset;
            }
            capabilities.Add(offset, capability);
            foreach(var register in capability.Registers)
            {
                registers.AddRegister(offset, register);
                offset += 4;
            }
        }

        protected readonly DoubleWordRegisterCollection registers;

        private bool IsHeaderAcceptable(HeaderType header)
        {
            var headerWithoutMultiFunctionFlag = header & ~HeaderType.MultiFunctionDevice;
            //we do not check "HasFlag, because a) Endpoint == 0, b) they are mutually exclusive
            return headerWithoutMultiFunctionFlag == HeaderType.Bridge || headerWithoutMultiFunctionFlag == HeaderType.Endpoint;
        }

        private readonly IPCIeRouter parent;
        private readonly BaseAddressRegister[] baseAddressRegisters;
        private readonly Dictionary<byte, Capability> capabilities = new Dictionary<byte, Capability>();

        private const byte CapabilitiesOffset = 0x40;

        private enum Registers
        {
            DeviceAndVendorId = 0x0,
            StatusAndConfiguration = 0x4,
            ClassCode = 0x8,
            Header = 0xc,
            BaseAddressRegister0 = 0x10,
            BaseAddressRegister1 = 0x14,
            //missing offsets are type-specific
            Capabilities = 0x34,
        }
    }
}

