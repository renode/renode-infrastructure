//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class SiFive_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public SiFive_UART(IMachine machine, long inputClockFrequency = 16000000) : base(machine)
        {
            this.inputClockFrequency = inputClockFrequency;

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.TransmitData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, b) =>
                    {
                        if(transmitEnable.Value)
                        {
                            this.TransmitCharacter((byte)b);
                            UpdateInterrupts();
                        }
                        else
                        {
                            var c = (char)b;
                            this.Log(LogLevel.Warning, "Trying to transmit '{1}' (0x{0}), but the transmitter is disabled", b, ' ' <= c ? c.ToString() : "<not a printable character>");
                        }
                    }, name: "DATA")
                    .WithTag("RESERVED", 8, 23)
                    .WithFlag(31, valueProviderCallback: _ => false, name: "FULL")
                },

                {(long)Registers.ReceiveData, new DoubleWordRegister(this)
                    // the "EMPTY" flag MUST be declared before "DATA" value field because 'Count' value
                    // might change as a result of dequeuing a character; otherwise if the queue was of
                    // length 1, the read of this register would return both the character and "EMPTY" flag
                    .WithFlag(31, valueProviderCallback: _ => Count == 0, name: "EMPTY")
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                        }
                        return character;
                    }, name: "DATA")
                    .WithTag("RESERVED", 8, 23)
                 },

                {(long)Registers.TransmitControl, new DoubleWordRegister(this)
                    .WithFlag(0, out transmitEnable, name: "TXEN")
                    .WithFlag(1, out numberOfStopBits, name: "NSTOP")
                    .WithTag("RESERVED", 2, 14)
                    .WithValueField(16, 3, out transmitWatermarkLevel, changeCallback: (_, __) => UpdateInterrupts(), name: "TXCNT")
                    .WithTag("RESERVED", 19, 13)
                },

                {(long)Registers.ReceiveControl, new DoubleWordRegister(this)
                    .WithFlag(0, out receiveEnable, changeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            ClearBuffer();
                        }
                    }, name: "RXEN")
                    .WithTag("RESERVED", 1, 15)
                    .WithValueField(16, 3, out receiveWatermarkLevel, changeCallback: (_, __) => UpdateInterrupts(), name: "RXCNT")
                    .WithTag("RESERVED", 19, 13)
                },

                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out transmitWatermarkInterruptEnable, changeCallback: (_, __) => UpdateInterrupts(), name: "TXWM")
                    .WithFlag(1, out receiveWatermarkInterruptEnable, changeCallback: (_, __) => UpdateInterrupts(), name: "RXWM")
                    .WithTag("RESERVED", 2, 30)
                },

                {(long)Registers.InterruptPending, new DoubleWordRegister(this)
                    .WithFlag(0, out transmitWatermarkInterruptPending, FieldMode.Read, name: "TXWM")
                    .WithFlag(1, out receiveWatermarkInterruptPending, FieldMode.Read, name: "RXWM")
                    .WithTag("RESERVED", 2, 30)
                },

                {(long)Registers.BaudrateDivisor, new DoubleWordRegister(this, 0xFFFF)
                    .WithValueField(0, 16, out baudRateDivisor, name: "DIV")
                    .WithTag("RESERVED", 16, 16)
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
            IRQ = new GPIO();

            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(innerLock)
            {
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(innerLock)
            {
                registers.Write(offset, value);
            }
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x100;
        public GPIO IRQ { get; private set; }
        public override Bits StopBits => numberOfStopBits.Value ? Bits.Two : Bits.One;
        public override Parity ParityBit => Parity.None;
        public override uint BaudRate => (uint)(inputClockFrequency / (uint)(1 + baudRateDivisor.Value));

        protected override void CharWritten()
        {
            if(!receiveEnable.Value)
            {
                ClearBuffer();
            }
            else
            {
                UpdateInterrupts();
            }
        }

        protected override void QueueEmptied()
        {
            if(receiveEnable.Value)
            {
                UpdateInterrupts();
            }
        }

        private void UpdateInterrupts()
        {
            lock(innerLock)
            {
                transmitWatermarkInterruptPending.Value = (transmitWatermarkLevel.Value > 0);
                receiveWatermarkInterruptPending.Value = (Count > (int)receiveWatermarkLevel.Value);

                IRQ.Set(transmitWatermarkInterruptEnable.Value && transmitWatermarkInterruptPending.Value
			|| receiveWatermarkInterruptEnable.Value && receiveWatermarkInterruptPending.Value);
            }
        }

        private readonly IFlagRegisterField transmitEnable;
        private readonly IFlagRegisterField receiveEnable;
        private readonly IFlagRegisterField numberOfStopBits;
        private readonly IValueRegisterField baudRateDivisor;
        private readonly IValueRegisterField transmitWatermarkLevel;
        private readonly IValueRegisterField receiveWatermarkLevel;
        private readonly IFlagRegisterField transmitWatermarkInterruptPending;
        private readonly IFlagRegisterField receiveWatermarkInterruptPending;
        private readonly IFlagRegisterField transmitWatermarkInterruptEnable;
        private readonly IFlagRegisterField receiveWatermarkInterruptEnable;

        private readonly long inputClockFrequency;
        private readonly DoubleWordRegisterCollection registers;

        private enum Registers : long
        {
            TransmitData = 0x0,
            ReceiveData = 0x04,
            TransmitControl = 0x08,
            ReceiveControl = 0x0C,
            InterruptEnable = 0x10,
            InterruptPending = 0x14,
            BaudrateDivisor = 0x18
        }
    }
}
