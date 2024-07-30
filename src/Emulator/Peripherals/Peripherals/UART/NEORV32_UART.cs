//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    public class NEORV32_UART : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public NEORV32_UART(IMachine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();

            registersMap.Add((long)Registers.Control, new DoubleWordRegister(this)
                .WithTaggedFlag("CTRL_EN", 0) // UART enable
                .WithTaggedFlag("CTRL_SIM_MODE", 1) // Enable simulation mode
                .WithTaggedFlag(name: "HWFC_EN", 2) // Enable RTS/CTS hardware flow-control
                .WithTag("CTRL_PRSC2:CTRL_PRSC0", 3, 3) // Baud rate clock prescaler select
                .WithTag("CTRL_BAUD9:CTRL_BAUD0", 6, 10) // 12-bit Baud value configuration value
                .WithFlag(16, 
                    FieldMode.Read,
                    valueProviderCallback: _ => rxWasNotEmptyOnLastRead,
                    name: "CTRL_RX_NEMPTY") // RX FIFO not empty
                .WithTaggedFlag("CTRL_RX_HALF", 17) // RX FIFO at least half-full
                .WithTaggedFlag("CTRL_RX_FULL", 18) // RX FIFO full
                .WithTaggedFlag("CTRL_TX_EMPTY", 19) // TX FIFO empty
                .WithTaggedFlag("CTRL_TX_NHALF", 20) // TX FIFO not at least half-full
                .WithTaggedFlag("CTRL_TX_FULL", 21) // TX FIFO full
                .WithTaggedFlag("CTRL_IRQ_RX_NEMPTY", 22) // fire IRQ if RX FIFO not empty
                .WithTaggedFlag("CTRL_IRQ_RX_HALF", 23) // fire IRQ if RX FIFO at least half-full
                .WithTaggedFlag("CTRL_IRQ_RX_FULL", 24) // fire IRQ if RX FIFO full
                .WithTaggedFlag("CTRL_IRQ_TX_EMPTY", 25) // fire IRQ if TX FIFO empty
                .WithTaggedFlag("CTRL_IRQ_TX_NHALF", 26) // fire IRQ if TX not at least half full
                .WithReservedBits(27, 1) // Reserved read as zero
                .WithTaggedFlag("CTRL_RX_CLR", 28) // Clear RX FIFO, flag auto-clears
                .WithTaggedFlag("CTRL_TX_CLR", 29) // Clear TX FIFO, flag auto-clears
                .WithTaggedFlag("CTRL_RX_OVER", 30) // RX FIFO overflow; leared by disabling the module
                .WithTaggedFlag("CTRL_TX_BUSY", 31) // TX busy or TX FIFO not empty
                .WithWriteCallback((_, __) => UpdateInterrupts())
            );

            registersMap.Add((long)Registers.Data, new DoubleWordRegister(this)
                .WithValueField(0, 8, name: "RTX_MSB:RTX_LSB", 
                    valueProviderCallback: _ => HandleReceiveData(),
                    writeCallback: (_, v) => HandleTransmitData(v))
                .WithValueField(8, 4, FieldMode.Read, name: "RX_FIFO_SIZE_MSB:RX_FIFO_SIZE_LSB", valueProviderCallback: _ => (ulong)System.Math.Log(FifoSize, 2))
                .WithValueField(12, 4, FieldMode.Read, name: "TX_FIFO_SIZE_MSB:TX_FIFO_SIZE_LSB", valueProviderCallback: _ => (ulong)System.Math.Log(FifoSize, 2))
                // Flag below shouldn't be defined in Data register, but it is in Zephyr.
                // There's PR fixing this: https://github.com/zephyrproject-rtos/zephyr/pull/72385
                .WithFlag(16, 
                    FieldMode.Read,
                    valueProviderCallback: _ => rxWasNotEmptyOnLastRead, 
                    name: "NEORV32_UART_CTRL_RX_NEMPTY")
                .WithReservedBits(17, 15)
            );

            TxInterrupt = new GPIO();
            RxInterrupt = new GPIO();

            RegistersCollection = new DoubleWordRegisterCollection(this, registersMap);
        }

        private uint HandleReceiveData()
        {
            rxWasNotEmptyOnLastRead = false;

            if(receiveQueue.TryDequeue(out var result))
            {
                rxWasNotEmptyOnLastRead = true;
                UpdateInterrupts();
            }
            return result;
        }

        private void HandleTransmitData(ulong value)
        {
            TransmitCharacter((byte)value);

            UpdateInterrupts();
        }

        private void UpdateInterrupts() 
        {
            bool rxInterrupt = receiveQueue.Count > 0;
            // In Renode, the TX happens instantly,
            // and the TX interrupt fires if the TX FIFO is empty.
            bool txInterrupt = true;
            this.Log(LogLevel.Noisy, "Updating interrupts, tx: {0}, rx: {1}", txInterrupt, rxInterrupt);
            
            TxInterrupt.Set(txInterrupt);
            RxInterrupt.Set(rxInterrupt);
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
            receiveQueue.Clear();
            TxInterrupt.Set(false);
            RxInterrupt.Set(false);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint val)
        {
            RegistersCollection.Write(offset, val);
        }

        public override void WriteChar(byte value)
        {
            if(!IsReceiveEnabled)
            {
                this.Log(LogLevel.Warning, "Char was received, but the receiver (or the whole USART) is not enabled. Ignoring.");
                return;
            }
            receiveQueue.Enqueue(value);
            UpdateInterrupts();
        }

        public GPIO TxInterrupt { get; }
        public GPIO RxInterrupt { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        public long Size => 0x8;

        private bool rxWasNotEmptyOnLastRead;

        private readonly Queue<byte> receiveQueue = new Queue<byte>();

        private const int FifoSize = 8;

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private enum Registers
        {
            Control = 0x00,
            Data = 0x04,
        }
    }
}
