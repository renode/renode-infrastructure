//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    abstract public class RenesasRA_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IWordPeripheral, IProvidesRegisterCollection<WordRegisterCollection>, IKnownSize
    {
        public RenesasRA_GPIO(IMachine machine, int portNumber, int numberOfConnections, RenesasRA_GPIOMisc pfsMisc) : base(machine, numberOfConnections)
        {
            RegistersCollection = new WordRegisterCollection(this);
            PinConfigurationRegistersCollection = new ByteRegisterCollection(this);

            pinDirection = new IEnumRegisterField<Direction>[numberOfConnections];
            usedAsIRQ = new IFlagRegisterField[numberOfConnections];

            this.pfsMisc = pfsMisc;
            this.portNumber = portNumber;

            IRQ0 = new GPIO();
            IRQ1 = new GPIO();
            IRQ2 = new GPIO();
            IRQ3 = new GPIO();
            IRQ4 = new GPIO();
            IRQ5 = new GPIO();
            IRQ6 = new GPIO();
            IRQ7 = new GPIO();
            IRQ8 = new GPIO();
            IRQ9 = new GPIO();
            IRQ10 = new GPIO();
            IRQ11 = new GPIO();
            IRQ12 = new GPIO();
            IRQ13 = new GPIO();
            IRQ14 = new GPIO();
            IRQ15 = new GPIO();

            DefineRegisters();
            DefinePinConfigurationRegisters();
        }

        // When read as a DoubleWord, we need to swap the lower and higher half of the
        // register wrt. to the memory layout
        // ex. PCNTR1 is PDR (lower 16) and PODR (higher 16)
        // but in memory PODR has an offset of 0x00 and PDR 0x02
        // This can be thought of as big-endian word-order within the register.
        // We can't use ReadWriteExtensions' `ReadDoubleWordUsingWordBigEndian` because
        // it assumes _byte_ BE, thus swapping the underlying bytes and not only the wods
        public void WriteDoubleWord(long offset, uint value)
        {
            if(!CheckAccessAligned(offset, 4, "Refusing write"))
            {
                return;
            }
            var valLo = value & 0xFFFF;
            var valHi = (value >> 16) & 0xFFFF;
            RegistersCollection.Write(offset, (ushort)valHi);
            RegistersCollection.Write(offset + 2, (ushort)valLo);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(!CheckAccessAligned(offset, 4, "Returning 0x0"))
            {
                return 0x0;
            }
            uint val = 0;
            val |= RegistersCollection.Read(offset);
            val <<= 16;
            val |= RegistersCollection.Read(offset + 2);
            return val;
        }

        public void WriteWord(long offset, ushort value)
        {
            if(!CheckAccessAligned(offset, 2, "Refusing write"))
            {
                return;
            }
            RegistersCollection.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            if(!CheckAccessAligned(offset, 2, "Returning 0x0"))
            {
                return 0x0;
            }
            return RegistersCollection.Read(offset);
        }

        public override void Reset()
        {
            base.Reset();

            IRQ0.Unset();
            IRQ1.Unset();
            IRQ2.Unset();
            IRQ3.Unset();
            IRQ4.Unset();
            IRQ5.Unset();
            IRQ6.Unset();
            IRQ7.Unset();
            IRQ8.Unset();
            IRQ9.Unset();
            IRQ10.Unset();
            IRQ11.Unset();
            IRQ12.Unset();
            IRQ13.Unset();
            IRQ14.Unset();
            IRQ15.Unset();
        }

        [ConnectionRegion("pinConfiguration")]
        public uint ReadDoubleWordFromPinConfiguration(long offset)
        {
            return (uint)HandleReadFromPinConfiguration(offset, bytes: 4);
        }

        [ConnectionRegion("pinConfiguration")]
        public void WriteDoubleWordToPinConfiguration(long offset, uint value)
        {
            HandleWriteToPinConfiguration(offset, value, bytes: 4);
        }

        [ConnectionRegion("pinConfiguration")]
        public ushort ReadWordFromPinConfiguration(long offset)
        {
            return (ushort)HandleReadFromPinConfiguration(offset, bytes: 2);
        }

        [ConnectionRegion("pinConfiguration")]
        public void WriteWordToPinConfiguration(long offset, ushort value)
        {
            HandleWriteToPinConfiguration(offset, value, bytes: 2);
        }

        [ConnectionRegion("pinConfiguration")]
        public void WriteByteToPinConfiguration(long offset, byte value)
        {
            HandleWriteToPinConfiguration(offset, value, bytes: 1);
        }

        [ConnectionRegion("pinConfiguration")]
        public byte ReadByteFromPinConfiguration(long offset)
        {
            return (byte)HandleReadFromPinConfiguration(offset, bytes: 1);
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            base.OnGPIO(number, value);

            if(pinDirection[number].Value != Direction.Input)
            {
                this.Log(LogLevel.Warning, "Writing to an output GPIO pin #{0}", number);
                return;
            }

            if(TryGetInterruptOutput(number, out var irq))
            {
                irq.Set(value);
            }
        }

        public GPIO IRQ0 { get; }
        public GPIO IRQ1 { get; }
        public GPIO IRQ2 { get; }
        public GPIO IRQ3 { get; }
        public GPIO IRQ4 { get; }
        public GPIO IRQ5 { get; }
        public GPIO IRQ6 { get; }
        public GPIO IRQ7 { get; }
        public GPIO IRQ8 { get; }
        public GPIO IRQ9 { get; }
        public GPIO IRQ10 { get; }
        public GPIO IRQ11 { get; }
        public GPIO IRQ12 { get; }
        public GPIO IRQ13 { get; }
        public GPIO IRQ14 { get; }
        public GPIO IRQ15 { get; }

        public WordRegisterCollection RegistersCollection { get; }

        public long Size => 0x20;

        abstract protected List<InterruptOutput>[] PinInterruptOutputs { get; }

        private bool CheckAccessAligned(long offset, uint alignment, string message)
        {
            // PCNTR{1,2,3,4} can be accessed either in a 32-bit unit (the whole register)
            // or 16-bit units (the lower or the upper half)
            if(offset % alignment != 0)
            {
                this.Log(LogLevel.Warning, "Unaligned {0}-bit access of register. {1}", alignment * 8, message);
                return false;
            }
            return true;
        }

        // PFS_BY (the lowest 8 bits) is stored at offset 0x03,
        // PFS_HA (the lowest 16 bits) is stored at offset 0x02,
        // PFS (as a whole) is stored at offset 0x0
        // It can be accessed as a byte @ 0x03, word @ 0x02 or double word @ 0x0
        private bool CheckPFSAccessAligned(long offset, int byteAccessWidth, string message)
        {
            if((offset % 4) + byteAccessWidth != 4)
            {
                this.Log(LogLevel.Warning, "Invalid {0}-bit access of PFS register at offset {1}. {2}", byteAccessWidth * 8, offset, message);
                return false;
            }
            return true;
        }

        private uint HandleReadFromPinConfiguration(long offset, int bytes)
        {
            if(!CheckPFSAccessAligned(offset, bytes, "Returning 0x0"))
            {
                return 0;
            }
            uint val = 0;
            for(var n = 0; n < bytes; n++)
            {
                val <<= 8;
                val |= PinConfigurationRegistersCollection.Read(offset + n);
            }
            return val;
        }

        private void HandleWriteToPinConfiguration(long offset, uint value, int bytes)
        {
            if(!pfsMisc.PFSWriteEnabled)
            {
                this.Log(LogLevel.Warning, "Trying to write to pin configuration registers (PFS) when PFSWE is deasserted");
                return;
            }
            if(!CheckPFSAccessAligned(offset, bytes, "Refusing write"))
            {
                return;
            }
            for(var n = 0; n < bytes; n++)
            {
                // Assuming a max 4-byte write
                PinConfigurationRegistersCollection.Write(offset + 3 - n, (byte)(value & 0xFF));
                value >>= 8;
            }
        }

        private void UpdateIRQOutput()
        {
            for(var i = 0; i < NumberOfConnections; ++i)
            {
                if(pinDirection[i].Value != Direction.Input)
                {
                    continue;
                }

                if(TryGetInterruptOutput(i, out var irq) && irq.IsSet != State[i])
                {
                    irq.Set(State[i]);
                }
            }
        }

        private void DefineRegisters()
        {
            Registers.OutputData.Define(this)
                .WithFlags(0, NumberOfConnections, name: "PODR",
                    valueProviderCallback: (i, _) => Connections[i].IsSet,
                    changeCallback: (i, _, value) => SetOutput(i, value))
            ;
            Registers.DataDirection.Define(this)
                .WithEnumFields(0, 1, NumberOfConnections, out pinDirection, name: "PDR",
                    // Pin direction is shared between this register (PCNTR1) and the PFS/Pin Configuration register
                    valueProviderCallback: (i, _) => pinDirection[i].Value,
                    changeCallback: (i, _, value) => { if(value == Direction.Input) UpdateIRQOutput(); })
            ;

            Registers.EventInputData.Define(this)
                .WithTag("EIDR", 0, NumberOfConnections)
            ;
            Registers.InputData.Define(this)
                .WithFlags(0, NumberOfConnections, FieldMode.Read, name: "PIDR",
                    valueProviderCallback: (i, _) => GetInput(i))
            ;

            Registers.OutputReset.Define(this)
                .WithFlags(0, NumberOfConnections, FieldMode.Write, name: "PORR",
                    writeCallback: (i, _, value) => { if(value) SetOutput(i, false); })
            ;
            Registers.OutputSet.Define(this)
                .WithFlags(0, NumberOfConnections, FieldMode.Write, name: "POSR",
                    writeCallback: (i, _, value) => { if(value) SetOutput(i, true); })
            ;
        }

        private void DefinePinConfigurationRegisters()
        {
            PFSRegisterBytes.PFS3.DefineMany(PinConfigurationRegistersCollection, (uint)NumberOfConnections, (register, idx) =>
            {
                register
                    .WithFlag(0, name: "PODR",
                        valueProviderCallback: _ => Connections[idx].IsSet,
                        changeCallback: (_, value) => SetOutput(idx, value))
                    .WithFlag(1, FieldMode.Read, name: "PIDR",
                        valueProviderCallback: _ => GetInput(idx))
                    .WithEnumField(2, 1, out pinDirection[idx], name: "PDR",
                        valueProviderCallback: _ => pinDirection[idx].Value,
                        changeCallback: (_, value) => { if(value == Direction.Input) UpdateIRQOutput(); })
                    .WithReservedBits(3, 1)
                    .WithFlag(4, name: "PCR")
                    .WithReservedBits(5, 1)
                    .WithFlag(6, name: "NCODR")
                    .WithReservedBits(7, 1)
                ;
            }, stepInBytes: 4);
            PFSRegisterBytes.PFS2.DefineMany(PinConfigurationRegistersCollection, (uint)NumberOfConnections, (register, idx) =>
            {
                register
                    .WithReservedBits(0, 2)
                    .WithTag("DSCR", 2, 2)
                    .WithTag("EOFR", 4, 2)
                    .WithFlag(6, out usedAsIRQ[idx], name: "ISEL",
                        changeCallback: (_, value) => UpdateIRQOutput())
                    .WithTaggedFlag("ASEL", 7)
                ;
            }, stepInBytes: 4);
            PFSRegisterBytes.PFS1.DefineMany(PinConfigurationRegistersCollection, (uint)NumberOfConnections, (register, idx) =>
            {
                register
                    .WithTaggedFlag("PMR", 0)
                    .WithReservedBits(1, 7)
                ;
            }, stepInBytes: 4);
            PFSRegisterBytes.PFS0.DefineMany(PinConfigurationRegistersCollection, (uint)NumberOfConnections, (register, idx) =>
            {
                register
                    .WithTag("PSEL", 0, 5)
                    .WithReservedBits(5, 3)
                ;
            }, stepInBytes: 4);
        }

        private bool GetInput(int index)
        {
            var value = State[index];
            if(pinDirection[index].Value == Direction.Output)
            {
                value |= Connections[index].IsSet;
            }
            return value;
        }

        private void SetOutput(int index, bool value)
        {
            if(pinDirection[index].Value != Direction.Output)
            {
                this.Log(LogLevel.Warning, "Trying to set pin level, but pin is not in output mode, ignoring");
                return;
            }

            Connections[index].Set(value);
        }

        private bool TryGetInterruptOutput(int number, out GPIO irq)
        {
            irq = null;
            if(!usedAsIRQ[number].Value)
            {
                return false;
            }

            var interruptOutput = PinInterruptOutputs[portNumber].SingleOrDefault(e => e.PinNumber == number);
            if(interruptOutput == null)
            {
                this.Log(LogLevel.Warning, "Trying to use pin#{0} as interrupt, but it's not associated with any IRQn output", number);
                return false;
            }

            irq = interruptOutput.IRQ;
            return true;
        }

        private ByteRegisterCollection PinConfigurationRegistersCollection { get; }

        private IEnumRegisterField<Direction>[] pinDirection;

        private readonly RenesasRA_GPIOMisc pfsMisc;
        private readonly int portNumber;
        private readonly IFlagRegisterField[] usedAsIRQ;

        protected class InterruptOutput
        {
            public InterruptOutput(int pinNumber, GPIO irq)
            {
                PinNumber = pinNumber;
                IRQ = irq;
            }

            public int PinNumber { get; }
            public GPIO IRQ { get; }
        }

        private enum Direction
        {
            Input,
            Output,
        }

        private enum Registers
        {
            OutputData = 0x00,
            DataDirection = 0x02,
            EventInputData = 0x04,
            InputData = 0x06,
            OutputReset = 0x08,
            OutputSet = 0x0A,
            EventOutputReset = 0x0C,
            EventOutputSet = 0x0E,
        }

        private enum PFSRegisterBytes
        {
            PFS0 = 0x0,
            PFS1 = 0x1,
            PFS2 = 0x2,
            PFS3 = 0x3
        }
    }
}
