//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class S32K3XX_SystemIntegrationUnitLite2 : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize,
        IProvidesRegisterCollection<DoubleWordRegisterCollection>, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public S32K3XX_SystemIntegrationUnitLite2()
        {
            doubleWordRegisterCollection = new DoubleWordRegisterCollection(this);
            byteRegisterCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            if(!doubleWordRegisterCollection.TryRead(offset, out var value))
            {
                return this.ReadDoubleWordUsingByte(offset);
            }
            return value;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(!doubleWordRegisterCollection.TryWrite(offset, value))
            {
                this.WriteDoubleWordUsingByte(offset, value);
            }
        }

        public ushort ReadWord(long offset)
        {
            return this.ReadWordUsingByte(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            this.WriteWordUsingByte(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return byteRegisterCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            byteRegisterCollection.Write(offset, value);
        }

        public void Reset()
        {
            doubleWordRegisterCollection.Reset();
            byteRegisterCollection.Reset();
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; } = new GPIO();

        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => doubleWordRegisterCollection;
        ByteRegisterCollection IProvidesRegisterCollection<ByteRegisterCollection>.RegistersCollection => byteRegisterCollection;

        private void DefineRegisters()
        {
            IProvidesRegisterCollection<DoubleWordRegisterCollection> asDoubleWordCollection = this;

            Registers.MCUIDRegister1.Define(asDoubleWordCollection)
                .WithTag("MinorMaskRevision", 0, 4)
                .WithTag("MajorMaskRevision", 4, 4)
                .WithReservedBits(8, 8)
                .WithTag("MCUPartNumber", 16, 10)
                .WithTag("ProductLineLetter", 26, 6)
            ;

            Registers.MCUIDRegister2.Define(asDoubleWordCollection)
                .WithTag("FlashSizeCode0", 0, 7)
                .WithTag("FlashSizeData0", 8, 4)
                .WithTag("FlashData0", 12, 2)
                .WithTag("FlashCode0", 14, 2)
                .WithTag("Frequency0", 16, 4)
                .WithTag("Package0", 20, 6)
                .WithTag("Temperature0", 26, 3)
                .WithTag("Technology0", 29, 3)
            ;

            var interruptStatusFlag = Registers.DMAInterruptStatusFlag0.Define(asDoubleWordCollection);
            var interruptRequestEnable = Registers.DMAInterruptRequestEnable0.Define(asDoubleWordCollection);
            var interruptRequestSelect = Registers.DMAInterruptRequestSelect0.Define(asDoubleWordCollection);
            var interruptRisingEdgeEventEnable = Registers.InterruptRisingEdgeEventEnable0.Define(asDoubleWordCollection);
            var interruptFallingEdgeEventEnable = Registers.InterruptFallingEdgeEventEnable0.Define(asDoubleWordCollection);
            var interruptFilterEnable = Registers.InterruptFilterEnable0.Define(asDoubleWordCollection);

            foreach(var irq in Enumerable.Range(0, (int)ExternalInterruptCount))
            {
                interruptStatusFlag.WithTaggedFlag($"ExternalInterruptStatusFlag{irq}", irq);
                interruptRequestEnable.WithTaggedFlag($"ExternalRequestEnable{irq}", irq);
                interruptRequestSelect.WithTaggedFlag($"RequestSelect{irq}", irq);
                interruptRisingEdgeEventEnable.WithTaggedFlag($"EnableRisingEdgeEventsToSetDISR0[{irq}]", irq);
                interruptFallingEdgeEventEnable.WithTaggedFlag($"EnableFallingEdgeEventsToSetDISR0[{irq}]", irq);
                interruptFilterEnable.WithTaggedFlag($"EnableFilterOnInterruptPad{irq}", irq);
            }

            Registers.InterruptFilterMaximumCounter0.DefineMany(asDoubleWordCollection, ExternalInterruptCount, (reg, index) => reg
                .WithTag("MaximumInterruptFilterCounter", 0, 4)
                .WithReservedBits(4, 28)
            );

            Registers.InterruptFilterClockPrescaler.Define(asDoubleWordCollection)
                .WithTag("InterruptFilterClockPrescaler", 0, 4)
                .WithReservedBits(4, 28)
            ;

            Registers.MUX0EMIOSEnable1.DefineMany(asDoubleWordCollection, MuxCount, (register, registerIndex) =>
            {
                var lowerFlags = Enumerable.Range(0, 8).ToDictionary(x => x, x => x + 16);
                var upperFlags = Enumerable.Range(16, 16).ToDictionary(x => x, x => x - 16);

                foreach(var flag in upperFlags.Concat(lowerFlags))
                {
                    register.WithTaggedFlag($"EMIOS0OutputFlag{flag.Value}MonitorEnable", flag.Key);
                }
                register.WithReservedBits(8, 8);
            }, stepInBytes: Registers.MUX1EMIOSEnable - Registers.MUX0EMIOSEnable1);

            Registers.MCUIDRegister3.Define(asDoubleWordCollection)
                .WithTag("SystemRAMSize", 0, 6)
                .WithReservedBits(6, 4)
                .WithTag("PartNumberSuffix", 10, 6)
                .WithTag("ProductFamilyNumber", 16, 10)
                .WithTag("ProductFamilyLetter", 26, 6)
            ;

            Registers.MCUIDRegister4.Define(asDoubleWordCollection)
                .WithTag("CorePlatformOptionsFeature", 0, 3)
                .WithTag("EthernetFeature", 3, 2)
                .WithTag("SecurityFeature", 5, 2)
                .WithReservedBits(7, 7)
                .WithTag("CorePlatformOptionsFeature", 14, 2)
                .WithReservedBits(16, 16)
            ;

            var multiplexedSignalConfigurationIndexes = Enumerable.Empty<int>()
                .ConcatRangeFromTo(0, 37)
                .ConcatRangeFromTo(40, 140)
                .ConcatRangeFromTo(142, 236);

            var multiplexedSignalConfigurationResets = new Dictionary<int, uint>
            {
                {4, 0x82827},
                {10, 0x127},
                {12, 0x3},
                {68, 0x82000},
                {69, 0x82800},
                {76, 0x4000},
                {80, 0x4000},
                {66, 0x4000},
                {67, 0x4000},
                {101, 0x4000},
                {102, 0x4000},
                {103, 0x4000},
                {106, 0x4000},
                {107, 0x4000},
                {108, 0x4000},
                {136, 0x4000},
            };

            foreach(var index in multiplexedSignalConfigurationIndexes)
            {
                var offset = Registers.MultiplexedSignalConfiguration0 + index * 4;
                if(!multiplexedSignalConfigurationResets.TryGetValue(index, out var resetValue))
                {
                    resetValue = 0;
                }

                offset.Define(asDoubleWordCollection, resetValue, name: $"MSCR{index}")
                    .WithTag("SourceSignalSelect", 0, 4)
                    .WithReservedBits(4, 1)
                    .WithTaggedFlag("SafeModeControl", 5)
                    .WithTaggedFlag("InputFilterEnable", 6)
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("DriveStrengthEnable", 8)
                    .WithReservedBits(9, 2)
                    .WithTaggedFlag("PullSelect", 11)
                    .WithReservedBits(12, 1)
                    .WithTaggedFlag("PullEnable", 13)
                    .WithTaggedFlag("SlewRateControl", 14)
                    .WithReservedBits(15, 1)
                    .WithTaggedFlag("PadKeepingEnable", 16)
                    .WithTaggedFlag("Invert", 17)
                    .WithReservedBits(18, 1)
                    .WithTaggedFlag("InputBufferEnable", 19)
                    .WithReservedBits(20, 1)
                    .WithTaggedFlag("GPIOOutputBufferEnable", 21)
                    .WithReservedBits(22, 10)
                ;
            }

            var inputMultiplexedSignalConfigurationRanges = Enumerable.Empty<int>()
                .ConcatRangeFromTo(0, 5)
                .ConcatRangeFromTo(16, 71)
                .ConcatRangeFromTo(80, 103)
                .ConcatRangeFromTo(112, 135)
                .ConcatRangeFromTo(144, 149)
                .ConcatRangeFromTo(152, 202)
                .ConcatRangeFromTo(211, 268)
                .ConcatRangeFromTo(289, 309)
                .ConcatRangeFromTo(315, 325)
                .ConcatRangeFromTo(343, 370)
                .ConcatRangeFromTo(373, 378)
                .ConcatRangeFromTo(389, 389)
                .ConcatRangeFromTo(398, 399)
                .ConcatRangeFromTo(409, 418)
                .ConcatRangeFromTo(440, 440)
                .ConcatRangeFromTo(448, 469);

            foreach(var index in inputMultiplexedSignalConfigurationRanges)
            {
                var offset = Registers.InputMultiplexedSignalConfiguration0 + index * 4;
                offset.Define(asDoubleWordCollection, name: $"IMCR{index}")
                    .WithTag($"SourceSignalSelect{index}", 0, 4)
                    .WithReservedBits(4, 28)
                ;
            }

            IProvidesRegisterCollection<ByteRegisterCollection> asByteCollection = this;
            var padDataIndexes = Enumerable.Empty<int>()
                .ConcatRangeFromTo(0, 37)
                .ConcatRangeFromTo(40, 140)
                .ConcatRangeFromTo(142, 236);
            foreach(var index in padDataIndexes)
            {
                var registerOffset = index + 3 - 2 * (index % 4);
                var outputOffset = Registers.GPIOPadDataOutput3 + registerOffset;
                outputOffset.Define(asByteCollection, name: $"GPDO{index}")
                    .WithTaggedFlag($"PadDataOut{index}", 0)
                    .WithReservedBits(1, 7)
                ;

                var inputOffset = Registers.GPIOPadDataInput3 + registerOffset;
                inputOffset.Define(asByteCollection, name: $"GPDI{index}")
                    .WithTaggedFlag($"PadDataInput{index}", 0)
                    .WithReservedBits(1, 7)
                ;
            }

            // Parallel GPIO Pad Data is implemented as two byte registers, instead of a single word register, to simplify implementation.
            // Those registers are accessible using all widths same as Pad Data registers (non-parallel). 
            var parallelPadDataReservedFlags = new Dictionary<int, int[]>
            {
                {2, new int [] {8, 9}},
                {8, new int [] {2}},
                {14, new int [] {0, 1, 2}}
            };
            foreach(var registerIndex in Enumerable.Range(0, PadDataCount))
            {
                var registerOffset = 2 * registerIndex + 2 - 4 * (registerIndex % 2);
                var outputOffset = Registers.ParallelGPIOPadDataOut0 + registerOffset;
                var inputOffset = Registers.ParallelGPIOPadDataIn0 + registerOffset;
                foreach(var byteIndex in Enumerable.Range(0, 2))
                {
                    var outputRegister = (outputOffset + byteIndex).Define(asByteCollection);
                    var inputRegister = (inputOffset + byteIndex).Define(asByteCollection);
                    foreach(var bitIndex in Enumerable.Range(0, 8))
                    {
                        var flagIndex = 8 * byteIndex + bitIndex;
                        if(parallelPadDataReservedFlags.TryGetValue(registerIndex, out var reservedFlagIndexes)
                            && reservedFlagIndexes.Contains(flagIndex))
                        {
                            outputRegister.WithReservedBits(bitIndex, 1);
                            inputRegister.WithReservedBits(bitIndex, 1);
                            continue;
                        }
                        outputRegister.WithTaggedFlag($"ParallelPadDataOutput{flagIndex}", bitIndex);
                        inputRegister.WithTaggedFlag($"ParallelPadDataInput{flagIndex}", bitIndex);
                    }
                }
            }

            Registers.MaskedParallelGPIOPadDataOut0.DefineMany(asDoubleWordCollection, PadDataCount, (register, registerIndex) =>
                {
                    foreach(var flagIndex in Enumerable.Range(0, 16))
                    {
                        var maskFlagIndex = 16 + flagIndex;
                        if(parallelPadDataReservedFlags.TryGetValue(registerIndex, out var reservedFlagIndexes)
                            && reservedFlagIndexes.Contains(flagIndex))
                        {
                            register.WithReservedBits(maskFlagIndex, 1);
                            register.WithReservedBits(flagIndex, 1);
                            continue;
                        }
                        register.WithTaggedFlag($"MaskField{flagIndex}", maskFlagIndex);
                        register.WithTaggedFlag($"MaskedParallelPadDataOut{flagIndex}", flagIndex);
                    }
                }
            );
        }

        private readonly DoubleWordRegisterCollection doubleWordRegisterCollection;
        private readonly ByteRegisterCollection byteRegisterCollection;
        private const uint ExternalInterruptCount = 32;
        private const uint MuxCount = 3;
        private const int PadDataCount = 15;

        public enum Registers
        {
            MCUIDRegister1 = 0x4, // MIDR1
            MCUIDRegister2 = 0x8, // MIDR2
            DMAInterruptStatusFlag0 = 0x10, // DISR0
            DMAInterruptRequestEnable0 = 0x18, // DIRER0
            DMAInterruptRequestSelect0 = 0x20, // DIRSR0
            InterruptRisingEdgeEventEnable0 = 0x28, // IREER0
            InterruptFallingEdgeEventEnable0 = 0x30, // IFEER0
            InterruptFilterEnable0 = 0x38, // IFER0
            InterruptFilterMaximumCounter0 = 0x40, // IFMCR0
            InterruptFilterMaximumCounter31 = 0xBC, // IFMCR31
            InterruptFilterClockPrescaler = 0xC0, // IFCPR
            MUX0EMIOSEnable1 = 0x100, // MUX0_EMIOS_EN1
            MUX0MISCEnable = 0x104, // MUX0_MISC_EN
            MUX1EMIOSEnable = 0x108, // MUX1_EMIOS_EN
            MUX1MISCEnable = 0x10C, // MUX1_MISC_EN
            MUX2EMIOSEnable = 0x110, // MUX2_EMIOS_EN
            MUX2MISCEnable = 0x114, // MUX2_MISC_EN
            MCUIDRegister3 = 0x200, // MIDR3
            MCUIDRegister4 = 0x204, // MIDR4
            MultiplexedSignalConfiguration0 = 0x240, // MSCR0
            MultiplexedSignalConfiguration236 = 0x5F0, // MSCR236
            InputMultiplexedSignalConfiguration0 = 0xA40, // IMCR0
            InputMultiplexedSignalConfiguration469 = 0x1194, // IMCR469
            GPIOPadDataOutput3 = 0x1300, // GPDO3
            GPIOPadDataOutput236 = 0x13EF, // GPDO236
            GPIOPadDataInput3 = 0x1500, // GPDI3
            GPIOPadDataInput236 = 0x15EF, // GPDI236
            ParallelGPIOPadDataOut0 = 0x1700, // PGPDO0
            ParallelGPIOPadDataOut14 = 0x171E, // PGPDO14
            ParallelGPIOPadDataIn0 = 0x1740, // PGPDI0
            ParallelGPIOPadDataIn14 = 0x175E, // PGPDI14
            MaskedParallelGPIOPadDataOut0 = 0x1780, // MPGPDO0
            MaskedParallelGPIOPadDataOut14 = 0x17B8 // MPGPDO14
        }
    }
}
