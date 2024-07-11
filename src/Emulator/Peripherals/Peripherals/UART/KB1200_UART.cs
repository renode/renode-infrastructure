//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System;

namespace Antmicro.Renode.Peripherals.UART
{
    public class KB1200_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public KB1200_UART(IMachine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();

            registersMap.Add((long)Registers.Configuration, new DoubleWordRegister(this)
                .WithTaggedFlag("SERIE_RX_ENABLE", 0)
                .WithFlag(1, out serieTxEnable, name: "SERIE_TX_ENABLE")
                .WithTag("Parity", 2, 2)
                .WithReservedBits(6, 10)
                .WithTag("Baud Rate", 16, 16)
            );
            
            registersMap.Add((long)Registers.InterruptEnable, new DoubleWordRegister(this)
                .WithFlag(0, out serieIrqRxEnable, name: "SERIE_IRQ_RX_ENABLE")
                .WithFlag(1, out serieIrqTxEnable, name: "SERIE_IRQ_TX_ENABLE")
                .WithTaggedFlag("SERIE_IRQ_RX_ERROR", 2)
                .WithReservedBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            );
            
            registersMap.Add((long)Registers.PendingFlag, new DoubleWordRegister(this)
                .WithFlag(0, out serpfRxCntFull, 
                    FieldMode.Read | FieldMode.WriteOneToClear, 
                    name: "SERPF_RX_CNT_FULL")
                // In Renode, the TX happens instantly
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "SEPRF_TX_EMPTY")
                .WithTaggedFlag("SERPF_RX_ERROR", 2)
                .WithReservedBits(4, 28)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            );
            
            registersMap.Add((long)Registers.Status, new DoubleWordRegister(this)
                .WithTaggedFlag("TX_FULL", 0)
                .WithTaggedFlag("TX_OVERRUN", 1)
                .WithTaggedFlag("TX_BUSY", 2)
                .WithReservedBits(3, 1)
                .WithFlag(4, FieldMode.Read, name: "RX_EMPTY", 
                    valueProviderCallback: _ => receiveQueue.Count == 0)
                .WithTaggedFlag("RX_OVERRUN", 5)
                .WithTaggedFlag("RX_BUSY", 6)
                .WithTaggedFlag("RX_TIMEOUT", 7)
                .WithTaggedFlag("PARITY_ERROR", 8)
                .WithTaggedFlag("FRAME_ERROR", 9)
                .WithReservedBits(10, 22)
            );
            
            registersMap.Add((long)Registers.RxDataBuffer, new DoubleWordRegister(this)
                .WithValueField(0, 8, 
                    FieldMode.Read, 
                    name: "SERTBUF", 
                    valueProviderCallback: _ => HandleReceiveData())
                .WithReservedBits(8, 24)
            );

            registersMap.Add((long)Registers.TxDataBuffer, new DoubleWordRegister(this)
                .WithValueField(0, 8, 
                    FieldMode.Write, 
                    name: "SERRBUF", 
                    writeCallback: (_, v) => HandleTransmitData((uint)v))
                .WithReservedBits(8, 24)
            );
            
            registersMap.Add((long)Registers.Control, new DoubleWordRegister(this)
                .WithTag("SERCTRL", 0, 3)
                .WithReservedBits(3, 29)
            );

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void WriteChar(byte value)
        {
            if(!IsReceiveEnabled)
            {
                this.Log(LogLevel.Warning, "Char was received, but the receiver (or the whole USART) is not enabled. Ignoring.");
                return;
            }
            // We assume FIFO size is 1
            serpfRxCntFull.Value = true;
            receiveQueue.Enqueue(value);
            UpdateInterrupts();
        }

        public void UpdateInterrupts() 
        {
            bool irqPending = (serieIrqRxEnable.Value && serpfRxCntFull.Value) 
                            || (serieIrqTxEnable.Value);
            this.Log(LogLevel.Noisy, "Setting IRQ: {0}", irqPending);
            IRQ.Set(irqPending);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            receiveQueue.Clear();
            IRQ.Set(false);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint val)
        {
            registers.Write(offset, val);
        }

        public GPIO IRQ { get; } = new GPIO();

        public override Bits StopBits { get; }

        public override Parity ParityBit { get; }

        public override uint BaudRate => 115200;

        public long Size => 0x1C;

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private uint HandleReceiveData()
        {
            if(receiveQueue.TryDequeue(out var result))
            {
                UpdateInterrupts();
            }
            return result;
        }

        private void HandleTransmitData(uint value)
        {
            if(!serieTxEnable.Value)
            {
                this.Log(LogLevel.Warning, "Char was to be sent, but the transmitter (or the whole USART) is not enabled. Ignoring.");
                return;
            }
            TransmitCharacter((byte)value);
        }

        private readonly IFlagRegisterField serieIrqRxEnable;
        private readonly IFlagRegisterField serieTxEnable;
        private readonly IFlagRegisterField serieIrqTxEnable;
        private readonly IFlagRegisterField serpfRxCntFull;

        private readonly Queue<byte> receiveQueue = new Queue<byte>();

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers
        {
            Configuration = 0x00,
            InterruptEnable = 0x04,
            PendingFlag = 0x08,
            Status = 0x0C,
            RxDataBuffer = 0x10,
            TxDataBuffer = 0x14,
            Control = 0x18
        }
    }
}
