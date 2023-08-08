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
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    public class CMSDK_APB_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public CMSDK_APB_UART(IMachine machine, uint frequency = 24000000) : base(machine)
        {
            this.frequency = frequency;
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 8, 
                        writeCallback: (_, value) => 
                        {
                            if(!txEnabled.Value)
                            {
                                this.Log(LogLevel.Warning, "Data Register Write Error: Transmitter not enabled");
                                return;
                            }
                            this.TransmitCharacter((byte)value);
                            if(txInterruptEnabled.Value)
                            {
                                txInterruptPending.Value = true;
                                UpdateInterrupts();
                            }
                        },
                        valueProviderCallback: _ => 
                        {
                            if(!rxEnabled.Value)
                            {
                                this.Log(LogLevel.Warning, "Data Register Read Error: Receiver not enabled");
                                return 0;
                            }
                            if(!TryGetCharacter(out var character))
                            {
                                this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO.");
                            }
                            return character;
                        })
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: "TxBufferFull", valueProviderCallback: _ => false)
                    .WithFlag(1, out rxBufferFull, FieldMode.Read, name: "RxBufferFull")
                    .WithTaggedFlag("TxBufferOverrun", 2)
                    .WithTaggedFlag("RxBufferOverrun", 3)
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, out txEnabled, name: "TxEnabled")  
                    .WithFlag(1, out rxEnabled, name: "RxEnabled") 
                    .WithFlag(2, out txInterruptEnabled, name: "TxInterruptEnable", writeCallback: (_, __) => UpdateInterrupts())
                    .WithFlag(3, out rxInterruptEnabled, name: "RxInterruptEnable", writeCallback: (_, __) => UpdateInterrupts())
                    .WithTaggedFlag("TxOverrunInterruptEnable", 4)
                    .WithTaggedFlag("RxOverrunInterruptEnable", 5)
                    .WithTaggedFlag("HighSpeedTestModeTx", 6)
                    .WithReservedBits(7, 25)
                },
                {(long)Registers.Interrupts, new DoubleWordRegister(this)
                    .WithFlag(0, out txInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "TxInterrupt", writeCallback: (_, __) => UpdateInterrupts())
                    .WithFlag(1, out rxInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "RxInterrupt", writeCallback: (_, __) => UpdateInterrupts())
                    .WithTaggedFlag("TxOverrunInterrupt", 2)
                    .WithTaggedFlag("RxOverrunInterrupt", 3)
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.BaudRateDiv, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out baudRateDivValue)
                    .WithReservedBits(8, 24)
                },
            };

            TxInterrupt = new GPIO();
            RxInterrupt = new GPIO();
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        protected override void CharWritten()
        {
            // We're assuming that the size of rx buffer equals 1, so we set rxBufferFull flag when a char is read by uart.
            rxBufferFull.Value = true;
            rxInterruptPending.Value = true;
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            rxBufferFull.Value = false;
        }

        private void UpdateInterrupts()
        {
            bool txInterrupt = txInterruptEnabled.Value && txInterruptPending.Value;
            bool rxInterrupt = rxInterruptEnabled.Value && rxInterruptPending.Value;
            TxInterrupt.Set(txInterrupt);
            RxInterrupt.Set(rxInterrupt);
        }

        public long Size => 0x1000;
        public GPIO TxInterrupt { get; }
        public GPIO RxInterrupt { get; }

        public override uint BaudRate => (baudRateDivValue.Value == 0) ? 0 : (uint)(frequency / baudRateDivValue.Value); 
        public override Bits StopBits => Bits.None;
        public override Parity ParityBit => Parity.None;

        private IFlagRegisterField rxBufferFull;
        private IFlagRegisterField txEnabled; 
        private IFlagRegisterField rxEnabled;
        private IFlagRegisterField txInterruptEnabled; 
        private IFlagRegisterField rxInterruptEnabled; 
        private IFlagRegisterField txInterruptPending; 
        private IFlagRegisterField rxInterruptPending; 
        private IValueRegisterField baudRateDivValue;

        private readonly DoubleWordRegisterCollection registers;
        private readonly ulong frequency;

        private enum Registers
        {
            Data = 0x00,
            Status = 0x04,
            Control = 0x08,
            Interrupts = 0x0c,
            BaudRateDiv = 0x10
        }
    }
}
