//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Utilities;

using Endianness = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class S32K3XX_SystemIntegrationUnitLite2 : BaseGPIOPort, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize,
        IProvidesRegisterCollection<DoubleWordRegisterCollection>, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public S32K3XX_SystemIntegrationUnitLite2(IMachine machine) : base(machine, MaximumValidPadIndex + 1)
        {
            IRQ1 = new GPIO();
            IRQ2 = new GPIO();
            IRQ3 = new GPIO();
            IRQ4 = new GPIO();

            interruptType = new InterruptType[ExternalInterruptCount];
            interruptPending = new IFlagRegisterField[ExternalInterruptCount];
            interruptEnabled = new IFlagRegisterField[ExternalInterruptCount];

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

        public override void Reset()
        {
            base.Reset();
            doubleWordRegisterCollection.Reset();
            byteRegisterCollection.Reset();
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!validPadIndexes.Contains(number))
            {
                this.Log(LogLevel.Warning, "Tried to set {0} to pad#{1} is not an valid pad index", value, number);
                return;
            }

            var previousState = State[number];
            base.OnGPIO(number, value);

            if(previousState == value)
            {
                return;
            }

            var mapping = eirqToPadMapping
                .Select((Pads, Index) => new { Pads, Index })
                .Where(item => item.Pads.Contains(number))
                .FirstOrDefault();

            if(mapping == null)
            {
                return;
            }

            var externalIRQ = mapping.Index;
            if(interruptType[externalIRQ].HasFlag(InterruptType.RisingEdge) && !previousState)
            {
                interruptPending[externalIRQ].Value = true;
            }
            if(interruptType[externalIRQ].HasFlag(InterruptType.FallingEdge) && previousState)
            {
                interruptPending[externalIRQ].Value = true;
            }

            UpdateInterrupts();
        }

        public int TranslatePinName(string pinName)
        {
            var normalizedPinName = pinName.ToUpper();

            // Pin name is in format PTxy, where x is between A and H, and y can be between 0 and 31
            if(normalizedPinName.Length < 4 || !normalizedPinName.StartsWith("PT"))
            {
                throw new RecoverableException($"{pinName} is invalid pin name. Correct pin names are in format PTxyy");
            }

            var portIndexChar = normalizedPinName[2];
            if(portIndexChar > 'H' || portIndexChar < 'A')
            {
                throw new RecoverableException("Pin name should be in range PTAxx to PTHxx");
            }
            var portIndex = portIndexChar - 'A';

            var pinIndexString = normalizedPinName.Substring(3);
            if(!Int32.TryParse(pinIndexString, out var pinIndex) || pinIndex < 0 || pinIndex > 31)
            {
                throw new RecoverableException("Pin name should be in range PTx00 to PTx31");
            }

            var padIndex = portIndex * 32 + pinIndex;
            if(!validPadIndexes.Contains(padIndex))
            {
                throw new RecoverableException("This pin is unavailable for GPIO");
            }

            return padIndex;
        }

        public long Size => 0x4000;
        public GPIO IRQ1 { get; }
        public GPIO IRQ2 { get; }
        public GPIO IRQ3 { get; }
        public GPIO IRQ4 { get; }

        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => doubleWordRegisterCollection;
        ByteRegisterCollection IProvidesRegisterCollection<ByteRegisterCollection>.RegistersCollection => byteRegisterCollection;

        private void UpdateInterrupts()
        {
            IRQ1.Set(Enumerable.Range(0, 8).Any(irq => interruptEnabled[irq].Value && interruptPending[irq].Value));
            IRQ2.Set(Enumerable.Range(8, 8).Any(irq => interruptEnabled[irq].Value && interruptPending[irq].Value));
            IRQ3.Set(Enumerable.Range(16, 8).Any(irq => interruptEnabled[irq].Value && interruptPending[irq].Value));
            IRQ4.Set(Enumerable.Range(24, 8).Any(irq => interruptEnabled[irq].Value && interruptPending[irq].Value));
        }

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

            for(var i = 0; i < ExternalInterruptCount; ++i)
            {
                var irq = i;

                interruptStatusFlag.WithFlag(irq, out interruptPending[irq], FieldMode.Read | FieldMode.WriteOneToClear, name: $"ExternalInterruptStatusFlag{irq}");
                interruptRequestEnable.WithFlag(irq, out interruptEnabled[irq], name: $"ExternalRequestEnable{irq}");
                interruptRequestSelect.WithTaggedFlag($"RequestSelect{irq}", irq);
                interruptRisingEdgeEventEnable.WithFlag(irq, name: $"EnableRisingEdgeEventsToSetDISR0[{irq}]",
                    valueProviderCallback: _ => interruptType[irq].HasFlag(InterruptType.RisingEdge),
                    changeCallback: (_, value) => interruptType[irq] = value ? (interruptType[irq] | InterruptType.RisingEdge) : (interruptType[irq] & ~InterruptType.RisingEdge)
                );
                interruptFallingEdgeEventEnable.WithFlag(irq, name: $"EnableFallingEdgeEventsToSetDISR0[{irq}]",
                    valueProviderCallback: _ => interruptType[irq].HasFlag(InterruptType.FallingEdge),
                    changeCallback: (_, value) => interruptType[irq] = value ? (interruptType[irq] | InterruptType.FallingEdge) : (interruptType[irq] & ~InterruptType.FallingEdge)
                );
                interruptFilterEnable.WithTaggedFlag($"EnableFilterOnInterruptPad{irq}", irq);
            }

            interruptStatusFlag.WithChangeCallback((_, __) => UpdateInterrupts());
            interruptRequestEnable.WithChangeCallback((_, __) => UpdateInterrupts());
            interruptRisingEdgeEventEnable.WithChangeCallback((_, __) => UpdateInterrupts());
            interruptFallingEdgeEventEnable.WithChangeCallback((_, __) => UpdateInterrupts());

            Registers.InterruptFilterMaximumCounter0.DefineMany(asDoubleWordCollection, ExternalInterruptCount, (register, registerIndex) =>
            {
                register
                    .WithTag("MaximumInterruptFilterCounter", 0, 4)
                    .WithReservedBits(4, 28)
                ;
            });

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

            foreach(var i in validPadIndexes.OrderBy(item => item))
            {
                var index = i;
                var registerOffset = index + 3 - 2 * (index % 4);
                var outputOffset = Registers.GPIOPadDataOutput3 + registerOffset;
                outputOffset.Define(asByteCollection, name: $"GPDO{index}")
                    .WithFlag(0, name: $"PadDataOut{index}",
                        valueProviderCallback: _ => Connections[index].IsSet,
                        changeCallback: (_, value) => Connections[index].Set(value))
                    .WithReservedBits(1, 7)
                ;

                var inputOffset = Registers.GPIOPadDataInput3 + registerOffset;
                inputOffset.Define(asByteCollection, name: $"GPDI{index}")
                    .WithFlag(0, FieldMode.Read, name: $"PadDataInput{index}",
                        valueProviderCallback: _ => State[index])
                    .WithReservedBits(1, 7)
                ;
            }

            // NOTE: As we are managing address translation manually in this peripheral,
            // we have to carefully calculate bit-offsets for 16-bit registers...
            var peripheralEndianness = this.GetEndianness(machine.SystemBus.Endianess);
            Func<int, int, int> getStartPadIndex = (int registerIndex, int byteIndex) =>
            {
                // Registers are 16-bit wide
                var startPadIndex = registerIndex * 16;
                // Bits are in reversed order
                switch(peripheralEndianness)
                {
                    case Endianness.LittleEndian:
                        startPadIndex += (1 - byteIndex) * 8;
                        break;
                    case Endianness.BigEndian:
                        startPadIndex += byteIndex * 8;
                        break;
                }

                return startPadIndex;
            };

            // While the register addresses are one after another...
            for(var registerOffset = 0; registerOffset < PadDataCount; ++registerOffset)
            {
                // ...the register indexes are in reversed order pair-wise, so 1, 0, 3, 2, 5, 4, etc.
                var registerIndex = registerOffset ^ 1;
                var outputOffset = Registers.ParallelGPIOPadDataOut0 + registerOffset * 2;
                var inputOffset = Registers.ParallelGPIOPadDataIn0 + registerOffset * 2;

                for(var byteIndex = 0; byteIndex < 2; ++byteIndex)
                {
                    var startPadIndex = getStartPadIndex(registerIndex, byteIndex);
                    var padRange = Enumerable.Range(startPadIndex, 8).Reverse();

                    (outputOffset + byteIndex).Define(asByteCollection)
                        .WithValueField(0, 8, name: $"ParallelPadDataOutput{registerIndex}.{byteIndex}",
                            valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(padRange.Select(pinIndex => Connections[pinIndex].IsSet)),
                            changeCallback: (previousValue, currentValue) =>
                            {
                                var difference = previousValue ^ currentValue;
                                foreach(var padIndex in BitHelper.GetSetBits(difference).Select(index => 7 - index))
                                {
                                    if(validPadIndexes.Contains(startPadIndex + padIndex))
                                    {
                                        Connections[startPadIndex + padIndex].Toggle();
                                    }
                                }
                            })
                    ;

                    (inputOffset + byteIndex).Define(asByteCollection)
                        .WithValueField(0, 8, FieldMode.Read, name: $"ParallelPadDataInput{registerIndex}.{byteIndex}",
                            valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(padRange.Select(pinIndex => State[pinIndex])))
                    ;
                }
            }

            // Parallel GPIO Pad Data is implemented as two byte registers, instead of a single word register, to simplify implementation.
            // Those registers are accessible using all widths same as Pad Data registers (non-parallel).
            var parallelPadDataReservedFlags = new Dictionary<int, int[]>
            {
                {2, new int [] {8, 9}},
                {8, new int [] {2}},
                {14, new int [] {0, 1, 2}}
            };

            Registers.MaskedParallelGPIOPadDataOut0.DefineMany(asDoubleWordCollection, PadDataCount, (register, registerIndex) =>
            {
                for(var flagIndex = 0; flagIndex < 16; ++flagIndex)
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
            });
        }

        private readonly HashSet<int> validPadIndexes =
            new HashSet<int>(Enumerable.Empty<int>()
                .ConcatRangeFromTo(0, 37)
                .ConcatRangeFromTo(40, 140)
                .ConcatRangeFromTo(142, 236));

        private readonly List<int[]> eirqToPadMapping = new List<int[]> {
            /* EIRQ00 */ new[] { 0, 18, 64, 128, 160 },
            /* EIRQ01 */ new[] { 1, 19, 65, 129, 161 },
            /* EIRQ02 */ new[] { 2, 20, 66, 130, 162 },
            /* EIRQ03 */ new[] { 3, 21, 67, 131, 163 },
            /* EIRQ04 */ new[] { 4, 16, 68, 132, 164 },
            /* EIRQ05 */ new[] { 5, 69, 133, 165 },
            /* EIRQ06 */ new[] { 6, 28, 70, 134, 166 },
            /* EIRQ07 */ new[] { 7, 30, 71, 136, 167 },
            /* EIRQ08 */ new[] { 32, 53, 96, 137, 192 },
            /* EIRQ09 */ new[] { 33, 54, 97, 138, 193 },
            /* EIRQ10 */ new[] { 34, 55, 98, 139, 194 },
            /* EIRQ11 */ new[] { 35, 56, 99, 140, 195 },
            /* EIRQ12 */ new[] { 36, 57, 100, 196 },
            /* EIRQ13 */ new[] { 37, 58, 101, 142, 197 },
            /* EIRQ14 */ new[] { 40, 60, 102, 143, 198 },
            /* EIRQ15 */ new[] { 41, 63, 103, 144, 199 },
            /* EIRQ16 */ new[] { 8, 72, 84, 168, 224 },
            /* EIRQ17 */ new[] { 9, 73, 85, 169, 225 },
            /* EIRQ18 */ new[] { 10, 74, 87, 170, 226 },
            /* EIRQ19 */ new[] { 11, 75, 88, 171, 227 },
            /* EIRQ20 */ new[] { 12, 76, 89, 172, 228 },
            /* EIRQ21 */ new[] { 13, 77, 90, 173, 229 },
            /* EIRQ22 */ new[] { 14, 78, 91, 174, 230 },
            /* EIRQ23 */ new[] { 15, 79, 93, 175, 231 },
            /* EIRQ24 */ new[] { 42, 104, 113, 200, 232 },
            /* EIRQ25 */ new[] { 43, 105, 116, 201, 233 },
            /* EIRQ26 */ new[] { 44, 106, 117, 202, 234 },
            /* EIRQ27 */ new[] { 45, 107, 118, 203, 235 },
            /* EIRQ28 */ new[] { 46, 108, 119, 204, 236 },
            /* EIRQ29 */ new[] { 47, 109, 120, 205, 221 },
            /* EIRQ30 */ new[] { 48, 110, 123, 206, 222 },
            /* EIRQ31 */ new[] { 49, 111, 124, 207, 223 },
        };

        private readonly DoubleWordRegisterCollection doubleWordRegisterCollection;
        private readonly ByteRegisterCollection byteRegisterCollection;
        private readonly IFlagRegisterField[] interruptPending;
        private readonly IFlagRegisterField[] interruptEnabled;

        private const int MaximumValidPadIndex = 236;
        private const uint ExternalInterruptCount = 32;
        private const uint MuxCount = 3;
        private const int PadDataCount = 15;

        private readonly InterruptType[] interruptType;

        [Flags]
        private enum InterruptType
        {
            Disabled,
            RisingEdge,
            FallingEdge,
        }

        private enum Registers
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
