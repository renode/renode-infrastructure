//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class LowPower_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public LowPower_UART(Machine machine, long frequency = 8000000) : base(machine)
        {
            this.frequency = frequency;

            IRQ = new GPIO();

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.BaudRate, new DoubleWordRegister(this)
                    .WithValueField(0, 13, out baudRateModuloDivisor, name: "SBR / Baud Rate Modulo Divisor")
                    .WithFlag(13, out stopBitNumberSelect, name: "SBNS / Stop Bit Number Select")
                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => true, name: "TC / Transmission Complete Flag")
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => true, name: "TDRE / Transmission Data Register Empty Flag")
                    .WithValueField(24, 5, out oversamplingRatio, name: "OSR / Oversampling Ratio")
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => Count > 0, name: "RDRF / Receive Data Register Full")
                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => true, name: "TC / Transmission Complete Flag")
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => true, name: "TDRE / Transmission Data Register Empty Flag")
                },

                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, out parityType, name: "PT / Parity Type")
                    .WithFlag(21, out receiverInterruptEnabled, name: "RIE / Receiver Interrupt Enable")
                    .WithFlag(22, name: "TCIE / Transmission Complete Interrupt Enable")
                    .WithFlag(23, out transmitterInterruptEnabled, name: "TIE / Transmission Interrupt Enable")
                    .WithWriteCallback((_, __) => UpdateInterrupt())
                },

                {(long)Registers.Fifo, new DoubleWordRegister(this)
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "RXFE / Receive FIFO Enable")
                    .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "TXFE / Transmit FIFO Enable")

                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => Count == 0, name: "RXEMPT / Receive Buffer Empty")
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => true, name: "TXEMPT / Transmit Buffer Empty")
                },

                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 9, valueProviderCallback: _ =>
                        {
                            if(!this.TryGetCharacter(out var b))
                            {
                                this.Log(LogLevel.Warning, "Trying to read form an empty fifo");
                            }

                            UpdateInterrupt();
                            return b;
                        },
                        writeCallback: (_, val) =>
                        {
                            TransmitCharacter((byte)val);
                            UpdateInterrupt();
                        })
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            UpdateInterrupt();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x30;

        public override Bits StopBits => stopBitNumberSelect.Value ? Bits.Two : Bits.One;

        public override Parity ParityBit => parityType.Value ? Parity.Odd : Parity.Even;

        public override uint BaudRate => (baudRateModuloDivisor.Value == 0)
            ? 0
            : (uint)(frequency / ((oversamplingRatio.Value + 1) * baudRateModuloDivisor.Value));

        public GPIO IRQ { get; set; }

        protected override void CharWritten()
        {
            UpdateInterrupt();
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupt();
        }

        private void UpdateInterrupt()
        {
            var irqState = false;

            if(transmitterInterruptEnabled.Value)
            {
                // TDRE is always 1, so this interrupt is always on
                irqState |= true;
            }

            if(receiverInterruptEnabled.Value)
            {
                irqState |= (Count > 0);
            }

            IRQ.Set(irqState);
            this.Log(LogLevel.Noisy, "Setting IRQ to {0}", irqState);
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly IFlagRegisterField transmitterInterruptEnabled;
        private readonly IFlagRegisterField receiverInterruptEnabled;
        private readonly IFlagRegisterField stopBitNumberSelect;
        private readonly IFlagRegisterField parityType;
        private readonly IValueRegisterField oversamplingRatio;
        private readonly IValueRegisterField baudRateModuloDivisor;
        private readonly long frequency;

        private enum Registers
        {
            BaudRate = 0x10,
            Status = 0x14,
            Control = 0x18,
            Data = 0x1c,
            Fifo = 0x28,
        }
    }
}
