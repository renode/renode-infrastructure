//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.UART
{
    public class LPC_USART : UARTBase, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public LPC_USART(IMachine machine, ulong clockFrequency = DefaultClockFrequency) : base(machine)
        {
            this.clockFrequency = clockFrequency;
            RegistersCollection = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            stopBits = false;
            parityMode = ParityMode.NoParity;
            RegistersCollection.Reset();
            base.Reset();
            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public override uint BaudRate => (uint)((clockFrequency / (oversampleSelection.Value + 1)) / (baudDivider.Value + 1));

        public override Bits StopBits => stopBits ? Bits.Two : Bits.One;

        public override Parity ParityBit
        {
            get
            {
                switch(parityMode)
                {
                    case ParityMode.Odd:
                        return Parity.Odd;
                    case ParityMode.Even:
                        return Parity.Even;
                    case ParityMode.NoParity:
                    case ParityMode.Reserved:
                        return Parity.None;
                }
                throw new Exception("Unreachable");
            }
        }

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupts();
        }

        private bool TxLevelInterruptStatus
        {
            get => txFifoLevelInterruptEnable.Value && GetTxFifoTriggerLevelStatus();
        }

        private bool RxLevelInterruptStatus
        {
            get => rxFifoLevelInterruptEnable.Value && GetRxFifoTriggerLevelStatus();
        }

        private void DefineRegisters()
        {
            Registers.Configuration.Define(this)
                .WithTaggedFlag("ENABLE", 0)
                .WithReservedBits(1, 1)
                .WithTag("DATALEN", 2, 2)
                .WithEnumField<DoubleWordRegister, ParityMode>(4, 2, name: "PARITYSEL",
                    valueProviderCallback: _ => parityMode,
                    writeCallback: (_, val) => parityMode = val
                )
                .WithFlag(6, name: "STOPLEN",
                    valueProviderCallback: _ => stopBits,
                    writeCallback: (_, val) => stopBits = val
                )
                .WithTaggedFlag("MODE32K", 7)
                .WithTaggedFlag("LINMODE", 8)
                .WithTaggedFlag("CTSEN", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("SYNCEN", 11)
                .WithTaggedFlag("CLKPOL", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("SYNCMST", 14)
                .WithTaggedFlag("LOOP", 15)
                .WithReservedBits(16, 2)
                .WithTaggedFlag("OETA", 18)
                .WithTaggedFlag("AUTOADDR", 19)
                .WithTaggedFlag("OESEL", 20)
                .WithTaggedFlag("OEPOL", 21)
                .WithTaggedFlag("RXPOL", 22)
                .WithTaggedFlag("TXPOL", 23)
                .WithReservedBits(24, 8);
            
            Registers.Control.Define(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("TXBRKEN", 1)
                .WithTaggedFlag("ADDRDET", 2)
                .WithReservedBits(3, 3)
                .WithFlag(6, out transmitDisable, name: "TXDIS")
                .WithReservedBits(7, 1)
                .WithTaggedFlag("CC", 8)
                .WithTaggedFlag("CLRCCONRX", 9)
                .WithReservedBits(10, 6)
                .WithTaggedFlag("AUTOBAUD", 16)
                .WithReservedBits(17, 15);
            
            Registers.Status.Define(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("RXIDLE", 1)
                .WithReservedBits(2, 1)
                .WithFlag(3, FieldMode.Read, name: "TXIDLE", valueProviderCallback: _ => true)
                .WithFlag(4, FieldMode.Read, name: "CTS - Clear To Send", valueProviderCallback: _ => true)
                .WithTaggedFlag("DELTACTS", 5)
                .WithTaggedFlag("TXDISSTAT", 6)
                .WithReservedBits(7, 2)
                .WithTaggedFlag("RXBRK", 10)
                .WithTaggedFlag("DELTARXBRK", 11)
                .WithTaggedFlag("START", 12)
                .WithTaggedFlag("FRAMERRINT", 13)
                .WithTaggedFlag("PARITYERRINT", 14)
                .WithTaggedFlag("RXNOSEINT", 15)
                .WithTaggedFlag("ABERR", 16)
                .WithReservedBits(17, 15);

            Registers.InterruptEnableReadSet.Define(this)
                .WithReservedBits(0, 3)
                .WithTaggedFlag("TXIDLEEN", 3)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("DELTACTSEN", 5)
                .WithTaggedFlag("TXDISEN", 6)
                .WithReservedBits(7, 4)
                .WithTaggedFlag("DELTARXBRKEN", 11)
                .WithTaggedFlag("STARTEN", 12)
                .WithTaggedFlag("FRAMERREN", 13)
                .WithTaggedFlag("PARITYERREN", 14)
                .WithTaggedFlag("RXNOISEEN", 15)
                .WithTaggedFlag("ABERREN", 16)
                .WithReservedBits(17, 15);

            Registers.InterruptEnableClear.Define(this)
                .WithReservedBits(0, 3)
                .WithTaggedFlag("TXIDLECLR", 3)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("DELTACTSCLR", 5)
                .WithTaggedFlag("TXDISCLR", 6)
                .WithReservedBits(7, 4)
                .WithTaggedFlag("DELTARXBRKCLR", 11)
                .WithTaggedFlag("STARTCLR", 12)
                .WithTaggedFlag("FRAMERRCLR", 13)
                .WithTaggedFlag("PARITYERRCLR", 14)
                .WithTaggedFlag("RXNOISECLR", 15)
                .WithTaggedFlag("ABERRCLR", 16)
                .WithReservedBits(17, 15);

            Registers.BaudRateGenerator.Define(this)
                .WithValueField(0, 16, out baudDivider, name: "BRGVAL")
                .WithReservedBits(16, 16);

            Registers.InterruptStatus.Define(this)
                .WithReservedBits(0, 3)
                .WithTaggedFlag("TXIDLE", 3)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("DELTACTS", 5)
                .WithTaggedFlag("TXDISINT", 6)
                .WithReservedBits(7, 4)
                .WithTaggedFlag("DELTARXBRK", 11)
                .WithTaggedFlag("START", 12)
                .WithTaggedFlag("FRAMERRINT", 13)
                .WithTaggedFlag("PARITYERRINT", 14)
                .WithTaggedFlag("RXNOISEINT", 15)
                .WithTaggedFlag("ABERRINT", 16)
                .WithReservedBits(17, 15);

            Registers.OversampleSelection.Define(this)
                .WithValueField(0, 8, out oversampleSelection, name: "OSRVAL")
                .WithReservedBits(8, 24);

            Registers.AutomaticAddressMatching.Define(this)
                .WithTag("ADDRESS", 0, 8)
                .WithReservedBits(8, 24);

            Registers.FifoConfiguration.Define(this)
                .WithFlag(0, name: "ENABLETX")
                .WithFlag(1, name: "ENABLERX")
                .WithReservedBits(2, 2)
                .WithTag("SIZE", 4, 2)
                .WithReservedBits(6, 6)
                .WithTaggedFlag("DMATX", 12)
                .WithTaggedFlag("DMARX", 13)
                .WithTaggedFlag("WAKETX", 14)
                .WithTaggedFlag("WAKERX", 15)
                .WithTaggedFlag("EMPTYTX", 16)
                .WithFlag(17, FieldMode.WriteOneToClear, name: "EMPTYRX", writeCallback: (_, val) => HandleClearRxQueue(val))
                .WithTaggedFlag("POPDBG", 18)
                .WithReservedBits(19, 13);

            Registers.FifoStatus.Define(this)
                .WithTaggedFlag("TXERR", 0)
                .WithTaggedFlag("RXERR", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("PERINT", 3)
                .WithFlag(4, FieldMode.Read, name: "TXEMPTY", valueProviderCallback: _ => true)
                .WithFlag(5, FieldMode.Read, name: "TXNOTFULL", valueProviderCallback: _ => true)
                .WithFlag(6, FieldMode.Read, name: "RXNOTEMPTY", valueProviderCallback: _ => Count > 0)
                .WithFlag(7, FieldMode.Read, name: "RXFULL", valueProviderCallback: _ => Count == (int)FifoCount.Full)
                .WithValueField(8, 5, FieldMode.Read, name: "TXLVL", valueProviderCallback: _ => 0)
                .WithReservedBits(13, 3)
                .WithValueField(16, 5, FieldMode.Read, name: "RXLVL", valueProviderCallback: _ => (ulong)Count)
                .WithReservedBits(21, 11);

            Registers.FifoTriggerSettings.Define(this)
                .WithFlag(0, out txFifoLevelTriggerEnable, name: "TXLVLENA")
                .WithFlag(1, out rxFifoLevelTriggerEnable, name: "RXLVLENA")
                .WithReservedBits(2, 6)
                .WithValueField(8, 4, out txFifoLevelTriggerPoint, name: "TXLVL")
                .WithReservedBits(12, 4)
                .WithValueField(16, 4, out rxFifoLevelTriggerPoint, name: "RXLVL")
                .WithReservedBits(20, 12)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.FifoInterruptEnable.Define(this)
                .WithTaggedFlag("TXERR", 0)
                .WithTaggedFlag("RXERR", 1)
                .WithFlag(2, out txFifoLevelInterruptEnable, FieldMode.Read | FieldMode.Set, name: "TXLVL")
                .WithFlag(3, out rxFifoLevelInterruptEnable, FieldMode.Read | FieldMode.Set, name: "RXLVL")
                .WithReservedBits(4, 28)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.FifoInterruptClear.Define(this)
                .WithTaggedFlag("TXERR", 0)
                .WithTaggedFlag("RXERR", 1)
                .WithFlag(2, name: "TXLVL",
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        txFifoLevelInterruptEnable.Value = false;
                    },
                    valueProviderCallback: _ => txFifoLevelInterruptEnable.Value)
                .WithFlag(3, name: "RXLVL",
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        rxFifoLevelInterruptEnable.Value = false;
                    },
                    valueProviderCallback: _ => rxFifoLevelInterruptEnable.Value)
                .WithReservedBits(4, 28)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.FifoInterruptStatus.Define(this)
                .WithTaggedFlag("TXERR", 0)
                .WithTaggedFlag("RXERR", 1)
                .WithFlag(2, FieldMode.Read, name: "TXLVL", valueProviderCallback: _ => TxLevelInterruptStatus)
                .WithFlag(3, FieldMode.Read, name: "RXLVL", valueProviderCallback: _ => RxLevelInterruptStatus)
                .WithReservedBits(4, 28);

            Registers.FifoWriteData.Define(this)
                .WithValueField(0, 8, FieldMode.Write, name: "TXDATA",
                    writeCallback: (_, val) =>
                    {
                        if(transmitDisable.Value)
                        {
                            return;
                        }
                        TransmitCharacter((byte)val);
                    })
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.FifoReadData.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "RXDATA",
                    valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var character))
                        {
                            return 0;
                        }
                        return character;
                    })
                .WithReservedBits(8, 24)
                .WithReadCallback((_, __) => UpdateInterrupts());

            Registers.FifoDataReadNoFifoPop.Define(this)
                .WithTag("RXDATA", 0, 9)
                .WithReservedBits(9, 4)
                .WithTaggedFlag("FRAMERR", 13)
                .WithTaggedFlag("PARITYERR", 14)
                .WithTaggedFlag("RXNOISE", 15)
                .WithReservedBits(16, 16);

            Registers.FifoSize.Define(this)
                .WithTag("FIFOSIZE", 0, 5)
                .WithReservedBits(5, 27);

            Registers.PeripheralIdentification.Define(this)
                .WithTag("APRETURE", 0, 8)
                .WithTag("MINOR_REV", 8, 4)
                .WithTag("MAJOR_REV", 12, 4)
                .WithTag("ID", 16, 16);
        }

        private bool GetTxFifoTriggerLevelStatus()
        {
            switch(txFifoLevelTriggerPoint.Value)
            {
                case 0b0000:
                    return Count == (int)FifoCount.Empty;
                case 0b0001:
                    return Count == (int)FifoCount.OneEntry;
                case 0b1111:
                    return Count <= (int)FifoCount.NoLongerFull;
                default:
                    this.Log(LogLevel.Warning, "Encountered unexpected TX FIFO trigger level point.");
                    return false;
            }
        }

        private bool GetRxFifoTriggerLevelStatus()
        {
            switch(rxFifoLevelTriggerPoint.Value)
            {
                case 0b0000:
                    return Count >= (int)FifoCount.OneEntry;
                case 0b0001:
                    return Count >= (int)FifoCount.TwoEntries;
                case 0b1111:
                    return Count == (int)FifoCount.Full;
                default:
                    this.Log(LogLevel.Warning, "Encountered unexpected RX FIFO trigger level point.");
                    return false;
            }
        }

        private void UpdateInterrupts()
        {
            var status = TxLevelInterruptStatus || RxLevelInterruptStatus;
            this.Log(LogLevel.Noisy, "IRQ set to {0}.", status);
            IRQ.Set(status);
        }

        private void HandleClearRxQueue(bool value)
        {
            if(value)
            {
                ClearBuffer();
                UpdateInterrupts();
            }
        }

        private bool stopBits;
        private ParityMode parityMode;

        private readonly ulong clockFrequency;

        private const ulong DefaultClockFrequency = 10000000;

        private IFlagRegisterField transmitDisable;
        private IFlagRegisterField txFifoLevelInterruptEnable;
        private IFlagRegisterField rxFifoLevelInterruptEnable;
        private IFlagRegisterField txFifoLevelTriggerEnable;
        private IFlagRegisterField rxFifoLevelTriggerEnable;
        private IValueRegisterField txFifoLevelTriggerPoint;
        private IValueRegisterField rxFifoLevelTriggerPoint;
        private IValueRegisterField baudDivider;
        private IValueRegisterField oversampleSelection;

        private enum ParityMode
        {
            NoParity = 0b00,
            Reserved = 0b01,
            Even = 0b10,
            Odd = 0b11,
        }

        private enum FifoCount : int
        {
            Full = 16,
            NoLongerFull = 15,
            TwoEntries = 2,
            OneEntry = 1,
            Empty = 0
        }

        private enum Registers
        {
            Configuration               = 0x0,
            Control                     = 0x4,
            Status                      = 0x8,
            InterruptEnableReadSet      = 0xC,
            InterruptEnableClear        = 0x10,
            BaudRateGenerator           = 0x20,
            InterruptStatus             = 0x24,
            OversampleSelection         = 0x28,
            AutomaticAddressMatching    = 0x2C,
            FifoConfiguration           = 0xE00,
            FifoStatus                  = 0xE04,
            FifoTriggerSettings         = 0xE08,
            FifoInterruptEnable         = 0xE10,
            FifoInterruptClear          = 0xE14,
            FifoInterruptStatus         = 0xE18,
            FifoWriteData               = 0xE20,
            FifoReadData                = 0xE30,
            FifoDataReadNoFifoPop       = 0xE40,
            FifoSize                    = 0xE48,
            PeripheralIdentification    = 0xFFC
        }
    }
}
