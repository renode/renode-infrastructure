//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class SI32_USART : UARTBase, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public SI32_USART(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();
            DefineRegisters();

            Reset();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            base.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        // IRQ is not supported yet
        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x100;

        public override uint BaudRate => 0;
        public override Parity ParityBit => Parity.None;
        public override Bits StopBits => Bits.None;

        protected override void CharWritten()
        {
        }

        protected override void QueueEmptied()
        {
        }

        private void DefineRegisters()
        {
            // in the current implementation we rely on the callbacks ordering
            Registers.Data.Define(this)
                .WithValueField(0, 8, name: "DATA",
                    valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var c))
                        {
                            this.Log(LogLevel.Warning, "Tried to read an empty data register");
                        }
                        return c;
                    },
                    writeCallback: (_, value) => TransmitCharacter((byte)value))
                .WithReservedBits(8, 24);
        }

        private enum Registers
        {
            Config = 0x0,
            ConfigSet = 0x4,
            ConfigClr = 0x8,
            Reserved0 = 0xc,
            Mode = 0x10,
            ModeSet = 0x14,
            ModeClr = 0x18,
            Reserved1 = 0x1c,
            Flowcn = 0x20,
            FlowcnSet = 0x24,
            FlowcnClr = 0x28,
            Reserved2 = 0x2c,
            Control = 0x30,
            ControlSet = 0x34,
            ControlClr = 0x38,
            Reserved3 = 0x3c,
            IPDelay = 0x40,
            Reserved4 = 0x44,
            Reserved5 = 0x48,
            Reserved6 = 0x4c,
            Baudrate = 0x50,
            Reserved7 = 0x54,
            Reserved8 = 0x58,
            Reserved9 = 0x5c,
            FIFOCn = 0x60,
            FIFOCnSet = 0x64,
            FIFOCnClr = 0x68,
            Reserved10 = 0x6c,
            Data = 0x70,
            Reserved11 = 0x74,
            Reserved12 = 0x78,
            Reserved13 = 0x7c,
        }
    }
}

